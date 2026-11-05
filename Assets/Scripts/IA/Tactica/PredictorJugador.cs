using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Predicción por INTENCIÓN del jugador (no solo en línea recta).
///
/// 1. Mide su velocidad (suavizada).
/// 2. Adivina su objetivo: de las monedas y la zona de extracción, el que mejor coincide con la dirección en que
///    camina (y la nave cuando ya tiene las monedas necesarias). La "confianza" sube mientras siga alineado.
/// 3. Calcula su ruta probable con el NavMesh hasta ese objetivo.
/// 4. Predecir(t) = mezcla entre la extrapolación lineal (pos + vel·t) y el punto de la ruta al que habrá llegado
///    caminando a su rapidez actual, ponderada por la confianza.
///
/// Además busca CUELLOS DE BOTELLA en esa ruta (puntos estrechos donde no puede esquivar hacia los lados)
/// para que el mortero dispare ahí.
///
/// No hay que ponerlo en la escena: se crea solo la primera vez que un enemigo lo necesita.
/// </summary>
[DefaultExecutionOrder(-50)]
public class PredictorJugador : MonoBehaviour
{
    [Header("Seguimiento")]
    [Tooltip("Suavizado de la velocidad medida (más alto = reacciona antes, más ruido).")]
    public float suavizadoVelocidad = 8f;

    [Header("Intención")]
    [Tooltip("Coseno mínimo entre la dirección del jugador y la del objetivo para considerarlo (0.6 ≈ 53°).")]
    public float alineacionMinima = 0.6f;
    [Tooltip("Por debajo de esta rapidez (m/s) se considera que está quieto.")]
    public float rapidezMinima = 1.2f;
    [Tooltip("Confianza ganada por segundo mientras sigue yendo hacia el mismo objetivo.")]
    public float subidaConfianza = 0.8f;
    [Tooltip("Confianza perdida por segundo cuando deja de ir hacia él.")]
    public float bajadaConfianza = 2f;
    public float distanciaMaximaObjetivo = 150f;
    public float intervaloRuta = 0.4f;
    public float intervaloObjetivos = 1f;

    [Header("Cuellos de botella")]
    [Tooltip("Un punto de la ruta es 'cuello de botella' si el borde caminable más cercano está a menos de esto (m).")]
    public float anchoMaximoCuello = 4f;

    [Header("Debug")]
    public bool dibujarGizmos = true;

    private static PredictorJugador instancia;
    private static bool cerrando;

    private Transform jugador;
    private CharacterController controladorJugador;
    private Rigidbody cuerpoJugador;
    private Vector3 posicion;
    private Vector3 posicionAnterior;
    private bool hayPosicionAnterior;
    private Vector3 velocidad;

    private readonly List<Transform> candidatos = new List<Transform>();
    private float temporizadorObjetivos;
    private Transform objetivo;
    private float confianza;

    private NavMeshPath ruta;
    private readonly Vector3[] esquinas = new Vector3[64];
    private int numEsquinas;
    private float temporizadorRuta;

    /// <summary>Instancia de la escena. Se crea sola si no existe (solo en Play).</summary>
    public static PredictorJugador Instancia
    {
        get
        {
            if (instancia == null && Application.isPlaying && !cerrando)
            {
                instancia = FindFirstObjectByType<PredictorJugador>();
                if (instancia == null) instancia = new GameObject("_PredictorJugador").AddComponent<PredictorJugador>();
            }
            return instancia;
        }
    }

    public Transform Jugador { get { return jugador; } }
    public Vector3 Posicion { get { return posicion; } }
    /// <summary>Velocidad horizontal suavizada.</summary>
    public Vector3 Velocidad { get { return velocidad; } }
    public float Rapidez { get { return velocidad.magnitude; } }
    public Transform ObjetivoProbable { get { return objetivo; } }
    public float Confianza { get { return confianza; } }
    public bool TieneRuta { get { return numEsquinas > 1 && objetivo != null; } }

    private void Awake()
    {
        if (instancia != null && instancia != this)
        {
            Destroy(this);
            return;
        }
        instancia = this;
        ruta = new NavMeshPath();
    }

    private void OnDestroy()
    {
        if (instancia == this) instancia = null;
    }

    private void OnApplicationQuit()
    {
        cerrando = true;
    }

    private void Update()
    {
        if (jugador == null && !BuscarJugador()) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return; // pausa

        MedirVelocidad(dt);

        temporizadorObjetivos -= dt;
        if (temporizadorObjetivos <= 0f)
        {
            temporizadorObjetivos = intervaloObjetivos;
            RecogerCandidatos();
        }

        EvaluarIntencion(dt);

        temporizadorRuta -= dt;
        if (temporizadorRuta <= 0f)
        {
            temporizadorRuta = intervaloRuta;
            CalcularRuta();
        }
    }

    private bool BuscarJugador()
    {
        GameObject go = GameObject.FindGameObjectWithTag("Player");
        if (go == null) return false;
        jugador = go.transform;
        controladorJugador = go.GetComponent<CharacterController>();
        cuerpoJugador = go.GetComponentInParent<Rigidbody>();
        posicion = jugador.position;
        hayPosicionAnterior = false;
        return true;
    }

    private void MedirVelocidad(float dt)
    {
        posicion = jugador.position;

        Vector3 medida;
        if (controladorJugador != null && controladorJugador.enabled) medida = controladorJugador.velocity;
        else if (cuerpoJugador != null) medida = cuerpoJugador.linearVelocity;
        else if (hayPosicionAnterior) medida = (posicion - posicionAnterior) / dt;
        else medida = Vector3.zero;
        medida.y = 0f;

        float k = 1f - Mathf.Exp(-suavizadoVelocidad * dt);
        velocidad = Vector3.Lerp(velocidad, medida, k);

        posicionAnterior = posicion;
        hayPosicionAnterior = true;
    }

    private void RecogerCandidatos()
    {
        candidatos.Clear();
        Moneda[] monedas = FindObjectsByType<Moneda>(FindObjectsSortMode.None);
        for (int i = 0; i < monedas.Length; i++) candidatos.Add(monedas[i].transform);
        ZonaExtraccion[] zonas = FindObjectsByType<ZonaExtraccion>(FindObjectsSortMode.None);
        for (int i = 0; i < zonas.Length; i++) candidatos.Add(zonas[i].transform);

        if (objetivo != null && !candidatos.Contains(objetivo))
        {
            objetivo = null; // la moneda ya se recogió
            confianza = 0f;
        }
    }

    private void EvaluarIntencion(float dt)
    {
        float rapidez = Rapidez;
        if (rapidez < rapidezMinima)
        {
            // Quieto: no sabemos a dónde irá. La confianza baja despacio (puede ser una pausa breve).
            confianza = Mathf.Max(0f, confianza - bajadaConfianza * 0.25f * dt);
            return;
        }

        Vector3 dir = velocidad / rapidez;
        bool tieneMonedas = GameManager.instancia != null && GameManager.instancia.MonedasActuales >= GameManager.instancia.MonedasParaGanar;

        Transform mejor = null;
        float mejorPuntaje = 0f;
        float puntajeActual = 0f;

        for (int i = 0; i < candidatos.Count; i++)
        {
            Transform c = candidatos[i];
            if (c == null) continue;

            Vector3 hacia = c.position - posicion;
            hacia.y = 0f;
            float d = hacia.magnitude;
            if (d < 1f || d > distanciaMaximaObjetivo) continue;

            float alineacion = Vector3.Dot(dir, hacia / d);
            if (alineacion < alineacionMinima) continue;

            float puntaje = alineacion / (1f + d / 40f);
            if (c.GetComponent<ZonaExtraccion>() != null) puntaje *= tieneMonedas ? 2.5f : 0.3f;

            if (c == objetivo) puntajeActual = puntaje;
            if (puntaje > mejorPuntaje)
            {
                mejorPuntaje = puntaje;
                mejor = c;
            }
        }

        if (mejor == null)
        {
            confianza = Mathf.Max(0f, confianza - bajadaConfianza * dt);
            if (confianza <= 0f) objetivo = null;
        }
        else if (mejor == objetivo || (objetivo != null && puntajeActual > 0f && mejorPuntaje < puntajeActual * 1.3f))
        {
            // Sigue yendo hacia el mismo (con histéresis: no cambia por una diferencia pequeña).
            confianza = Mathf.Min(1f, confianza + subidaConfianza * dt);
        }
        else
        {
            objetivo = mejor;
            confianza = 0.15f;
            temporizadorRuta = 0f; // recalcular la ruta ya
        }
    }

    private void CalcularRuta()
    {
        numEsquinas = 0;
        if (objetivo == null) return;
        if (!NavPathFollower.TryCalculatePath(posicion, objetivo.position, ruta)) return;
        numEsquinas = ruta.GetCornersNonAlloc(esquinas);
    }

    /// <summary>Dónde estará el jugador dentro de t segundos (mezcla de línea recta y ruta según la confianza).</summary>
    public Vector3 Predecir(float t)
    {
        Vector3 lineal = posicion + velocidad * t;
        float rapidez = Rapidez;
        if (!TieneRuta || confianza <= 0.01f || rapidez < 0.1f) return lineal;

        Vector3 enRuta = AvanzarPorRuta(rapidez * t);
        enRuta.y = lineal.y;
        return Vector3.Lerp(lineal, enRuta, confianza);
    }

    /// <summary>Punto de la ruta probable tras recorrer 'distancia' metros desde la posición actual.</summary>
    public Vector3 AvanzarPorRuta(float distancia)
    {
        if (numEsquinas < 2) return posicion + velocidad.normalized * distancia;

        Vector3 desde = posicion;
        // La esquina 0 es donde estaba el jugador al calcular la ruta: se parte de su posición actual hacia la 1.
        for (int i = 1; i < numEsquinas; i++)
        {
            Vector3 hasta = esquinas[i];
            hasta.y = posicion.y;
            float tramo = Vector3.Distance(desde, hasta);
            if (distancia <= tramo) return Vector3.Lerp(desde, hasta, tramo > 0.0001f ? distancia / tramo : 1f);
            distancia -= tramo;
            desde = hasta;
        }
        return desde; // se detiene en el objetivo
    }

    /// <summary>
    /// Intercepción en un cuello de botella de la ruta probable.
    /// Recorre la ruta en el tiempo; para cada punto calcula la velocidad horizontal que necesitaría el proyectil
    /// para llegar justo cuando llega el jugador. Si esa velocidad cabe en [velMin, velMax] (arcos alto a bajo),
    /// es un tiro posible. Gana el punto más estrecho (menor distancia al borde del NavMesh).
    /// </summary>
    public bool BuscarCuelloDeBotella(Vector3 origen, float velMin, float velMax, float tMin, float tMax,
        out Vector3 punto, out float velHorizontal, out float tiempo)
    {
        punto = Vector3.zero;
        velHorizontal = 0f;
        tiempo = 0f;

        float rapidez = Rapidez;
        if (!TieneRuta || confianza < 0.5f || rapidez < rapidezMinima) return false;

        float mejor = float.MaxValue;
        bool encontrado = false;
        for (float t = tMin; t <= tMax; t += 0.25f)
        {
            Vector3 p = AvanzarPorRuta(rapidez * t);
            Vector3 h = p - origen;
            h.y = 0f;
            float v = h.magnitude / t;
            if (v < velMin || v > velMax) continue;

            NavMeshHit enMalla, borde;
            if (!NavMesh.SamplePosition(p, out enMalla, 3f, NavMesh.AllAreas)) continue;
            if (!NavMesh.FindClosestEdge(enMalla.position, out borde, NavMesh.AllAreas)) continue;
            float ancho = borde.distance;
            if (ancho > anchoMaximoCuello) continue;

            float puntaje = ancho + t * 0.3f; // estrecho y, a igualdad, cuanto antes mejor
            if (puntaje < mejor)
            {
                mejor = puntaje;
                punto = p;
                velHorizontal = v;
                tiempo = t;
                encontrado = true;
            }
        }
        return encontrado;
    }

    private void OnDrawGizmos()
    {
        if (!dibujarGizmos || jugador == null) return;

        // Ruta probable (magenta) y objetivo adivinado
        if (TieneRuta)
        {
            Gizmos.color = new Color(1f, 0.2f, 1f, 0.4f + 0.6f * confianza);
            Vector3 anterior = posicion;
            for (int i = 1; i < numEsquinas; i++)
            {
                Gizmos.DrawLine(anterior + Vector3.up * 0.2f, esquinas[i] + Vector3.up * 0.2f);
                anterior = esquinas[i];
            }
            Gizmos.DrawWireSphere(objetivo.position, 1.2f);
        }

        // Posiciones previstas a 1, 2 y 3 segundos
        Gizmos.color = Color.yellow;
        for (int s = 1; s <= 3; s++) Gizmos.DrawWireSphere(Predecir(s) + Vector3.up * 0.2f, 0.3f);
    }
}
