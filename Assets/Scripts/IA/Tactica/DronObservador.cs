using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dron observador: vuela patrullando el grafo de waypoints con un foco que muestra su cono de visión.
/// Cuando ve al jugador lo "marca" (láser rojo) y publica su posición en la RedTactica:
///   * los morteros con "Requiere Vision" pueden dispararle aunque una colina les tape la vista (fuego indirecto),
///   * los duendes cercanos acuden.
/// Si lo destruyes (granada, o un mortero que falla cerca), los morteros se quedan "ciegos".
///
/// No necesita modelo: el menú  Solar Adventure > IA Táctica > 3 · Crear dron observador  lo arma con primitivas.
/// </summary>
public class DronObservador : MonoBehaviour, IDamageable
{
    public enum Estado { PATRULLA, OBSERVANDO, BUSCANDO }

    [Header("Vuelo")]
    [Tooltip("Altura de vuelo sobre el suelo (m).")]
    public float altura = 9f;
    public float velocidad = 6f;
    [Tooltip("Distancia (m) que mantiene con el jugador mientras lo observa.")]
    public float distanciaObservacion = 14f;
    [Tooltip("Sin grafo de waypoints: radio de la órbita alrededor del punto de inicio.")]
    public float radioOrbita = 20f;
    [Tooltip("Con grafo: patrulla los waypoints a menos de esta distancia (m) de donde empieza. 0 = todo el mapa.")]
    public float radioPatrulla = 60f;

    [Header("Visión")]
    public float rangoVision = 35f;
    [Tooltip("Ángulo TOTAL del cono (grados).")]
    public float anguloVision = 70f;
    [Tooltip("Inclinación hacia abajo del cono mientras patrulla (grados).")]
    public float inclinacion = 35f;
    [Tooltip("Capas que tapan la vista (Suelo).")]
    public LayerMask obstaculos;
    [Tooltip("Segundos que busca al jugador tras perderlo antes de volver a patrullar.")]
    public float memoria = 5f;

    [Header("Salud")]
    public float salud = 30f;

    [Header("Referencias visuales (opcionales)")]
    [Tooltip("Luz tipo Spot que muestra hacia dónde mira. Cambia a rojo al detectar.")]
    public Light foco;
    public Color colorNormal = new Color(0.75f, 0.9f, 1f);
    public Color colorAlerta = new Color(1f, 0.15f, 0.1f);
    [Tooltip("Material del láser que une el dron con el jugador observado (p. ej. Mat_AnilloAviso).")]
    public Material materialLaser;
    [Tooltip("Partículas al ser destruido (opcional).")]
    public GameObject explosionPrefab;
    [Tooltip("Sonido de alarma al detectar al jugador (opcional). Si está vacío se usa el de Solar Adventure > Sonido.")]
    public AudioCue alarmaCue;
    [Tooltip("Zumbido mientras patrulla, se repite (opcional). Si está vacío se usa el de Solar Adventure > Sonido.")]
    public AudioCue patrullaCue;

    [Header("Debug")]
    public Estado estado = Estado.PATRULLA;

    private Transform jugador;
    private Vector3 inicio;
    private Vector3 mirada = Vector3.forward;
    private float anguloOrbita;
    private float tiempoSinVer;
    private Vector3 ultimaPosicion;
    private bool muerto;
    private LineRenderer laser;
    private AudioSource zumbido;
    private AudioCue cueZumbido;
    private float volumenZumbido;

    private WaypointNode nodoActual;
    private WaypointNode nodoAnterior;

    public bool VeAlJugador { get; private set; }

    private void Start()
    {
        inicio = transform.position;
        anguloOrbita = Random.Range(0f, 360f);
        GameObject go = GameObject.FindGameObjectWithTag("Player");
        if (go != null) jugador = go.transform;

        GrafoWaypoints grafo = GrafoWaypoints.Actual;
        if (grafo != null) nodoActual = grafo.MasCercano(transform.position);

        CrearLaser();
        AplicarColor(false);
        CrearZumbido();
    }

    // Zumbido en bucle: suena en PATRULLA y BUSCANDO, y se apaga mientras te observa (ahí suena la alarma).
    private void CrearZumbido()
    {
        cueZumbido = patrullaCue;
        if (cueZumbido == null || !cueZumbido.HasClip)
        {
            SonidosJuego sonidos = SonidosJuego.Actual;
            cueZumbido = sonidos != null ? sonidos.dronPatrulla : null;
        }
        if (cueZumbido == null || !cueZumbido.HasClip) return;

        zumbido = gameObject.AddComponent<AudioSource>();
        cueZumbido.Aplicar(zumbido, true);
        zumbido.volume = 0f;
        if (zumbido.clip == null) { zumbido = null; return; }
        zumbido.Play();
    }

    private void ActualizarZumbido()
    {
        if (zumbido == null) return;
        bool suena = !muerto && Time.timeScale > 0f && estado != Estado.OBSERVANDO;
        float objetivo = suena ? cueZumbido.volume : 0f;
        volumenZumbido = Mathf.Lerp(volumenZumbido, objetivo, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
        zumbido.volume = volumenZumbido;
    }

    private void Update()
    {
        ActualizarZumbido();
        if (muerto) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        if (jugador == null)
        {
            GameObject go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) jugador = go.transform;
        }

        Percibir();

        Vector3 destino;
        switch (estado)
        {
            case Estado.OBSERVANDO:
                destino = PuntoDeObservacion();
                break;
            case Estado.BUSCANDO:
                destino = ultimaPosicion;
                tiempoSinVer += dt;
                if (tiempoSinVer > memoria) estado = Estado.PATRULLA;
                break;
            default:
                destino = SiguientePuntoPatrulla(dt);
                break;
        }

        Volar(destino, dt);
        Orientar(dt);
        ActualizarLaser();
    }

    private void Percibir()
    {
        bool antes = VeAlJugador;
        VeAlJugador = false;
        if (jugador == null) return;

        Vector3 pecho = Percepcion.PuntoPecho(jugador);
        VeAlJugador = Percepcion.PuedeVer(transform.position, mirada, pecho, rangoVision, anguloVision, obstaculos);

        if (VeAlJugador)
        {
            PredictorJugador p = PredictorJugador.Instancia;
            RedTactica.ReportarAvistamiento(jugador.position, p != null ? p.Velocidad : Vector3.zero, this);
            ultimaPosicion = jugador.position;
            tiempoSinVer = 0f;
            if (estado != Estado.OBSERVANDO)
            {
                HUDTactico.MensajeUnico("dron_detecta", "¡Un dron te ha detectado! La artillería sabe dónde estás.", colorAlerta, 6f);
                AudioCue alarma = alarmaCue;
                if (alarma == null || !alarma.HasClip)
                {
                    SonidosJuego sonidos = SonidosJuego.Actual;
                    alarma = sonidos != null ? sonidos.dronDetecta : null;
                }
                if (alarma != null) alarma.PlayAt(transform.position);
            }
            estado = Estado.OBSERVANDO;
        }
        else if (estado == Estado.OBSERVANDO)
        {
            estado = Estado.BUSCANDO;
        }

        if (antes != VeAlJugador) AplicarColor(VeAlJugador);
    }

    // Se coloca a distanciaObservacion del jugador, del lado en el que ya está (no se le echa encima).
    private Vector3 PuntoDeObservacion()
    {
        Vector3 desdeJugador = transform.position - jugador.position;
        desdeJugador.y = 0f;
        if (desdeJugador.sqrMagnitude < 0.01f) desdeJugador = Vector3.back;
        return jugador.position + desdeJugador.normalized * distanciaObservacion;
    }

    private Vector3 SiguientePuntoPatrulla(float dt)
    {
        GrafoWaypoints grafo = GrafoWaypoints.Actual;
        if (grafo != null && grafo.Cantidad > 0)
        {
            if (nodoActual == null) nodoActual = grafo.MasCercano(transform.position);
            Vector3 d = nodoActual.Posicion - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 4f)
            {
                WaypointNode siguiente = grafo.VecinoAleatorioEnZona(nodoActual, nodoAnterior, inicio, radioPatrulla);
                nodoAnterior = nodoActual;
                nodoActual = siguiente;
            }
            return nodoActual.Posicion;
        }

        // Sin grafo: órbita alrededor del punto de inicio.
        anguloOrbita += velocidad / Mathf.Max(1f, radioOrbita) * Mathf.Rad2Deg * dt;
        float a = anguloOrbita * Mathf.Deg2Rad;
        return inicio + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radioOrbita;
    }

    // Vuelo simple: avanza en horizontal hacia el destino y mantiene la altura sobre el suelo, con un leve balanceo.
    private void Volar(Vector3 destino, float dt)
    {
        Vector3 pos = transform.position;
        Vector3 h = destino - pos;
        h.y = 0f;
        Vector3 paso = Vector3.ClampMagnitude(h, velocidad * dt);
        if (h.sqrMagnitude > 0.01f && estado != Estado.OBSERVANDO) mirada = Vector3.Slerp(mirada, AbajoAdelante(h.normalized), 1f - Mathf.Exp(-3f * dt));

        float suelo = pos.y - altura;
        RaycastHit hit;
        if (Physics.Raycast(pos + Vector3.up * 20f, Vector3.down, out hit, 200f, obstaculos, QueryTriggerInteraction.Ignore))
        {
            suelo = hit.point.y;
        }
        float alturaObjetivo = suelo + altura + Mathf.Sin(Time.time * 2f) * 0.3f;
        float y = Mathf.Lerp(pos.y, alturaObjetivo, 1f - Mathf.Exp(-2f * dt));

        transform.position = new Vector3(pos.x + paso.x, y, pos.z + paso.z);
    }

    private Vector3 AbajoAdelante(Vector3 horizontal)
    {
        Vector3 eje = Vector3.Cross(Vector3.up, horizontal);
        return Quaternion.AngleAxis(inclinacion, eje) * horizontal;
    }

    private void Orientar(float dt)
    {
        if (estado == Estado.OBSERVANDO && jugador != null)
        {
            Vector3 hacia = Percepcion.PuntoPecho(jugador) - transform.position;
            if (hacia.sqrMagnitude > 0.01f) mirada = Vector3.Slerp(mirada, hacia.normalized, 1f - Mathf.Exp(-6f * dt));
        }

        if (mirada.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(mirada), 1f - Mathf.Exp(-5f * dt));
        }
    }

    private void AplicarColor(bool alerta)
    {
        if (foco != null) foco.color = alerta ? colorAlerta : colorNormal;
    }

    private void CrearLaser()
    {
        GameObject go = new GameObject("Laser");
        go.transform.SetParent(transform, false);
        laser = go.AddComponent<LineRenderer>();
        laser.positionCount = 2;
        laser.widthMultiplier = 0.06f;
        laser.useWorldSpace = true;
        laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        laser.receiveShadows = false;
        Material m = materialLaser;
        if (m == null)
        {
            Shader s = Shader.Find("Sprites/Default");
            if (s != null) m = new Material(s);
        }
        laser.sharedMaterial = m;
        laser.startColor = new Color(1f, 0.1f, 0.05f, 0.9f);
        laser.endColor = new Color(1f, 0.1f, 0.05f, 0.2f);
        laser.enabled = false;
    }

    private void ActualizarLaser()
    {
        if (laser == null) return;
        laser.enabled = VeAlJugador && jugador != null;
        if (!laser.enabled) return;
        laser.SetPosition(0, transform.position);
        laser.SetPosition(1, Percepcion.PuntoPecho(jugador));
    }

    public void TakeDamage(float amount)
    {
        if (muerto || amount <= 0f) return;
        salud -= amount;
        if (salud <= 0f) StartCoroutine(Caer());
    }

    // Sin animaciones: cae girando y explota al tocar el suelo.
    private System.Collections.IEnumerator Caer()
    {
        muerto = true;
        VeAlJugador = false;
        if (zumbido != null) zumbido.Stop();
        if (laser != null) laser.enabled = false;
        if (foco != null) foco.enabled = false;
        bool porJugador = MortarProjectile.ExplosionActualEsDelJugador;
        HUDTactico.Mensaje((porJugador ? "Dron derribado" : "Fuego amigo: el mortero derribó al dron") +
            ". La artillería pierde su vista aérea.", new Color(0.4f, 1f, 0.5f));

        float vy = 0f;
        float t = 0f;
        while (t < 3f)
        {
            t += Time.deltaTime;
            vy -= 15f * Time.deltaTime;
            Vector3 paso = new Vector3(0f, vy * Time.deltaTime, 0f);
            if (Physics.Raycast(transform.position, Vector3.down, -paso.y + 0.3f, obstaculos, QueryTriggerInteraction.Ignore)) break;
            transform.position += paso;
            transform.Rotate(0f, 540f * Time.deltaTime, 200f * Time.deltaTime, Space.Self);
            yield return null;
        }

        if (explosionPrefab != null) Destroy(Instantiate(explosionPrefab, transform.position, Quaternion.identity), 4f);
        Destroy(gameObject);
    }

    private void OnDrawGizmos()
    {
        Vector3 dir = Application.isPlaying ? mirada : AbajoAdelante(transform.forward);
        Color c = VeAlJugador ? new Color(1f, 0.15f, 0.1f, 0.9f) : new Color(0.6f, 0.85f, 1f, 0.7f);
        Percepcion.DibujarCono(transform.position, dir, rangoVision, anguloVision, c);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 1.5f, "Dron · " + estado);
#endif
    }
}
