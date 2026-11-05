using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Granadas del jugador: la MISMA física predictiva que usa el mortero, ahora en manos del jugador.
///
///   * Mantén pulsado el clic izquierdo (o G) para apuntar: se dibuja la parábola y el anillo donde caerá.
///     Cuanto más tiempo lo mantienes, más lejos cae (de Distancia Mínima a Máxima), siempre en la dirección
///     horizontal en la que mira la cámara. La inclinación de la cámara (arriba o abajo) NO cambia el alcance.
///   * Si hay un enemigo (duende, mortero o dron) cerca de esa dirección, la mira se engancha a él y
///     calcula el ADELANTO con BallisticSolver (si se mueve, la granada cae donde estará).
///   * Suelta para lanzar. Las granadas se recargan solas.
///   * El clic derecho queda para mover la cámara (ver AstronautThirdPersonCamera).
///
/// Reutiliza el prefab del proyectil del mortero (Proyectil_Mortero): no hace falta arte nuevo.
/// Se pone en el objeto del jugador (el que tiene el tag Player). El menú
/// Solar Adventure > IA Táctica > 5 · Dar granadas al jugador  lo añade y rellena solo.
/// </summary>
public class LanzadorGranadas : MonoBehaviour
{
    [Header("Proyectil")]
    [Tooltip("Prefab del proyectil (puede ser el mismo del mortero).")]
    public MortarProjectile proyectilPrefab;
    [Tooltip("Capas del suelo (donde explota la granada).")]
    public LayerMask capasSuelo;
    [Tooltip("Capas de los enemigos (choca con ellos y solo a ellos les hace daño).")]
    public LayerMask capasEnemigo;
    [Tooltip("Multiplicador del radio de explosión respecto al del prefab.")]
    public float multiplicadorRadio = 0.8f;
    [Tooltip("Multiplicador del daño respecto al del prefab (25 x 1.2 = 30: mata a un duende y a un dron).")]
    public float multiplicadorDanio = 1.2f;

    [Header("Lanzamiento")]
    public float distanciaMinima = 6f;
    public float distanciaMaxima = 28f;
    [Tooltip("Segundos manteniendo pulsado para pasar de la distancia mínima a la máxima.")]
    public float tiempoCarga = 1.2f;
    [Tooltip("Velocidad horizontal de la granada (m/s).")]
    public float velocidadHorizontal = 11f;
    public float velocidadMaximaLanzamiento = 30f;
    public float alturaSalida = 1.6f;

    [Header("Asistencia de puntería")]
    [Tooltip("Ángulo (grados) alrededor de la mirada en el que se engancha a un enemigo.")]
    public float anguloAsistencia = 18f;
    [Tooltip("Si está activo, adelanta al enemigo según su velocidad (física predictiva).")]
    public bool adelantarObjetivo = true;

    [Header("Munición")]
    public int granadasMaximas = 3;
    [Tooltip("Segundos para recuperar una granada.")]
    public float tiempoRecarga = 8f;

    [Header("Audio (opcional)")]
    [Tooltip("Sonido al lanzar (disparo del arma). Si está vacío se usa el de Solar Adventure > Sonido > espacio \"5 · Jugador\".")]
    public AudioCue lanzarCue;

    [Header("Visual")]
    [Tooltip("Material transparente sin iluminación para la parábola y el anillo (p. ej. Mat_AnilloAviso).")]
    public Material materialLinea;
    public Color colorLibre = new Color(0.3f, 0.9f, 1f, 0.9f);
    public Color colorEnganchado = new Color(1f, 0.35f, 0.1f, 0.95f);
    [Tooltip("Texto opcional del HUD. Si está vacío se muestra un recuadro pequeño en pantalla.")]
    public TextMeshProUGUI textoHUD;

    public int Granadas { get { return granadas; } }
    /// <summary>0..1 hasta recuperar la siguiente granada.</summary>
    public float ProgresoRecarga { get { return tiempoRecarga > 0f ? Mathf.Clamp01(recarga / tiempoRecarga) : 1f; } }

    private int granadas;
    private float recarga;
    private bool apuntando;
    private readonly Collider[] bufferEnemigos = new Collider[48];
    private float carga;
    private Camera camara;
    private LineRenderer arco;
    private LineRenderer anillo;
    private readonly Vector3[] puntosArco = new Vector3[40];
    private readonly Vector3[] puntosAnillo = new Vector3[32];

    private Transform objetivoEnganchado;
    private Vector3 posAnteriorObjetivo;
    private Vector3 velObjetivo;
    private BallisticSolver.Solution solucion;
    private bool haySolucion;

    private void Awake()
    {
        granadas = granadasMaximas;
        arco = CrearLinea("ArcoGranada", puntosArco.Length, false);
        anillo = CrearLinea("AnilloGranada", puntosAnillo.Length, true);
        MostrarGuia(false);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) // pausa
        {
            apuntando = false; // sin esto, al reanudar se lanzaría una granada sin querer
            MostrarGuia(false);
            return;
        }

        Recargar(dt);

        bool pulsado = (Mouse.current != null && Mouse.current.leftButton.isPressed) ||
                       (Keyboard.current != null && Keyboard.current.gKey.isPressed);

        if (pulsado && granadas > 0)
        {
            if (!apuntando)
            {
                apuntando = true;
                carga = 0f;
                objetivoEnganchado = null;
            }
            carga = Mathf.Min(1f, carga + dt / Mathf.Max(0.05f, tiempoCarga));
            Apuntar(dt);
        }
        else if (apuntando)
        {
            apuntando = false;
            MostrarGuia(false);
            if (haySolucion && granadas > 0) Lanzar();
        }

        ActualizarHUD();
    }

    private void Recargar(float dt)
    {
        if (granadas >= granadasMaximas)
        {
            recarga = 0f;
            return;
        }
        recarga += dt;
        if (recarga >= tiempoRecarga)
        {
            recarga = 0f;
            granadas++;
        }
    }

    private Vector3 Origen { get { return transform.position + Vector3.up * alturaSalida + DireccionDeMira * 0.4f; } }

    // Cámara del jugador (la hija del astronauta o, si no hay, la principal).
    private Camera Camara
    {
        get
        {
            if (camara == null) camara = GetComponentInChildren<Camera>();
            if (camara == null) camara = Camera.main;
            return camara;
        }
    }

    // Hacia dónde mira la cámara, en horizontal.
    private Vector3 DireccionDeMira
    {
        get
        {
            Camera c = Camara;
            Vector3 f = c != null ? c.transform.forward : transform.forward;
            f.y = 0f;
            return f.sqrMagnitude > 0.0001f ? f.normalized : transform.forward;
        }
    }

    // Punto del suelo a 'distancia' metros del jugador en la dirección horizontal de la cámara.
    // No depende de la inclinación de la cámara: el alcance lo fija solo el tiempo que se mantiene pulsado.
    private Vector3 PuntoDeCarga(float distancia)
    {
        Vector3 punto = transform.position + DireccionDeMira * distancia;
        RaycastHit suelo;
        if (Physics.Raycast(punto + Vector3.up * 60f, Vector3.down, out suelo, 200f, capasSuelo, QueryTriggerInteraction.Ignore))
        {
            punto = suelo.point;
        }
        return punto;
    }

    private void Apuntar(float dt)
    {
        Vector3 origen = Origen;
        float g = Mathf.Abs(Physics.gravity.y);

        float distancia = Mathf.Lerp(distanciaMinima, distanciaMaxima, carga);
        Vector3 puntoMira = PuntoDeCarga(distancia);
        Transform enganche = BuscarObjetivo(puntoMira);
        if (enganche != objetivoEnganchado)
        {
            objetivoEnganchado = enganche;
            velObjetivo = Vector3.zero;
            if (enganche != null) posAnteriorObjetivo = enganche.position;
        }

        Vector3 objetivo;
        Vector3 velocidadObjetivo = Vector3.zero;
        if (objetivoEnganchado != null)
        {
            Vector3 medida = (objetivoEnganchado.position - posAnteriorObjetivo) / dt;
            posAnteriorObjetivo = objetivoEnganchado.position;
            velObjetivo = Vector3.Lerp(velObjetivo, medida, 1f - Mathf.Exp(-10f * dt));
            objetivo = objetivoEnganchado.position + Vector3.up * 0.8f;
            if (adelantarObjetivo) velocidadObjetivo = velObjetivo;
        }
        else
        {
            objetivo = puntoMira;
        }

        solucion = BallisticSolver.SolveLeadShot(origen, objetivo, velocidadObjetivo, velocidadHorizontal, g,
            0.2f, 6f, velocidadMaximaLanzamiento);
        haySolucion = solucion.IsValid;

        if (!haySolucion)
        {
            MostrarGuia(false);
            return;
        }

        BallisticSolver.FillTrajectory(puntosArco, origen, solucion.launchVelocity, g, solucion.flightTime);
        arco.SetPositions(puntosArco);

        float radio = (proyectilPrefab != null ? proyectilPrefab.BlastRadius : 4f) * multiplicadorRadio;
        for (int i = 0; i < puntosAnillo.Length; i++)
        {
            float a = Mathf.PI * 2f * i / puntosAnillo.Length;
            puntosAnillo[i] = solucion.aimPoint + new Vector3(Mathf.Cos(a) * radio, 0.15f, Mathf.Sin(a) * radio);
        }
        anillo.SetPositions(puntosAnillo);

        Color c = objetivoEnganchado != null ? colorEnganchado : colorLibre;
        arco.startColor = c;
        arco.endColor = c;
        anillo.startColor = c;
        anillo.endColor = c;
        MostrarGuia(true);
    }

    // Enemigo vivo más alineado con el punto de mira, dentro del alcance y del ángulo de asistencia.
    private Transform BuscarObjetivo(Vector3 puntoMira)
    {
        if (capasEnemigo.value == 0) return null;

        Vector3 haciaMira = puntoMira - transform.position;
        haciaMira.y = 0f;
        if (haciaMira.sqrMagnitude < 0.0001f) haciaMira = DireccionDeMira;

        int total = Physics.OverlapSphereNonAlloc(transform.position, distanciaMaxima, bufferEnemigos, capasEnemigo, QueryTriggerInteraction.Ignore);
        Transform mejor = null;
        float mejorAngulo = anguloAsistencia;
        for (int i = 0; i < total; i++)
        {
            IDamageable d = bufferEnemigos[i].GetComponentInParent<IDamageable>();
            if (d == null) continue;
            Transform t = ((Component)d).transform;

            AIMob mob = t.GetComponent<AIMob>();
            if (mob != null && mob.EstaMuerto) continue;

            Vector3 hacia = t.position - transform.position;
            hacia.y = 0f;
            if (hacia.magnitude < distanciaMinima * 0.5f) continue;
            float angulo = Vector3.Angle(haciaMira, hacia);
            if (angulo < mejorAngulo)
            {
                mejorAngulo = angulo;
                mejor = t;
            }
        }
        return mejor;
    }

    private void Lanzar()
    {
        if (proyectilPrefab == null)
        {
            Debug.LogWarning("LanzadorGranadas: falta asignar Proyectil Prefab.", this);
            return;
        }

        granadas--;

        // El astronauta se gira hacia donde lanza.
        Vector3 haciaLanzamiento = new Vector3(solucion.launchVelocity.x, 0f, solucion.launchVelocity.z);
        if (haciaLanzamiento.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(haciaLanzamiento.normalized, Vector3.up);

        Vector3 origen = Origen;
        float g = Mathf.Abs(Physics.gravity.y);

        MortarProjectile p = Instantiate(proyectilPrefab, origen, Quaternion.LookRotation(solucion.launchVelocity));
        MortarLaunchData data = new MortarLaunchData();
        data.velocity = solucion.launchVelocity;
        data.gravity = g;
        data.windAcceleration = Vector3.zero;
        data.expectedFlightTime = solucion.flightTime;
        data.maxLifetime = solucion.flightTime + 3f;
        data.blastRadiusMultiplier = multiplicadorRadio;
        data.damageMultiplier = multiplicadorDanio;
        data.fromPlayer = true;
        data.overrideMasks = true;
        data.collisionMask = capasSuelo | capasEnemigo;
        data.damageMask = capasEnemigo;
        p.Launch(data);

        AudioCue disparo = lanzarCue;
        if (disparo == null || !disparo.HasClip)
        {
            SonidosJuego sonidos = SonidosJuego.Actual;
            disparo = sonidos != null ? sonidos.disparoJugador : null;
        }
        if (disparo != null) disparo.PlayAt(origen);
    }

    private LineRenderer CrearLinea(string nombre, int puntos, bool cerrada)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(transform, false);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = cerrada;
        lr.positionCount = puntos;
        lr.widthMultiplier = 0.08f;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        Material m = materialLinea;
        if (m == null)
        {
            Shader s = Shader.Find("Sprites/Default");
            if (s != null) m = new Material(s);
        }
        lr.sharedMaterial = m;
        return lr;
    }

    private void MostrarGuia(bool visible)
    {
        if (arco != null) arco.enabled = visible;
        if (anillo != null) anillo.enabled = visible;
    }

    private void ActualizarHUD()
    {
        if (textoHUD == null) return;
        textoHUD.text = "Granadas: " + granadas + " / " + granadasMaximas;
    }

    private void OnGUI()
    {
        if (textoHUD != null || HUDTactico.Activo || Time.timeScale <= 0f) return;
        string texto = "Granadas: " + granadas + "/" + granadasMaximas;
        if (granadas < granadasMaximas) texto += "  (+1 en " + Mathf.CeilToInt(tiempoRecarga - recarga) + " s)";
        texto += "\n[Clic izq. / G] apuntar y lanzar";
        GUI.Box(new Rect(Screen.width - 250, Screen.height - 64, 240, 54), texto);
    }
}
