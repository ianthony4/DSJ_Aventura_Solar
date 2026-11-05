using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Duende táctico (mode = TACTICO).
//
//   Percibir : cono de visión + raycast (no ve a través de colinas) y oído a corta distancia.
//   Decidir  : FSM  PATRULLA -> PERSECUCIÓN (State.SEEK) -> BÚSQUEDA -> PATRULLA
//   Moverse  : grafo de waypoints (A*) para decidir por dónde ir + NavMesh para el detalle, recorrido con steering.
//   Cooperar : publica lo que ve en la RedTactica; acude a las alertas de otros (dron, mortero, duendes);
//              en persecución en grupo cada uno toma un puesto de flanqueo para rodear al jugador.
//   Adaptarse: al perderlo predice hacia dónde iba y revisa primero los waypoints en esa dirección.
public partial class AIMob
{
    [Header("Táctico - Percepción")]
    [Tooltip("Distancia máxima de visión (m).")]
    public float rangoVision = 22f;
    [Tooltip("Ángulo TOTAL del cono de visión (grados).")]
    public float anguloVision = 110f;
    [Tooltip("A esta distancia te oye aunque no te vea (pasos).")]
    public float radioOido = 4f;
    [Tooltip("Altura de los ojos sobre el pivote.")]
    public float alturaOjos = 1.5f;

    [Header("Táctico - Movimiento")]
    [Tooltip("Patrulla los waypoints a menos de esta distancia (m) de donde empieza. 0 = todo el mapa.")]
    public float radioPatrulla = 40f;
    [Range(0.1f, 1f)] public float factorVelocidadPatrulla = 0.45f;
    [Range(0.1f, 1f)] public float factorVelocidadBusqueda = 0.75f;

    [Header("Táctico - Escuadra y búsqueda")]
    [Tooltip("En persecución en grupo, los duendes extra se abren a los lados del jugador para cortarle el paso.")]
    public bool usarFlanqueo = true;
    [Tooltip("Distancia al jugador de los puestos de flanqueo (m).")]
    public float radioFlanqueo = 7f;
    [Tooltip("Acude a investigar alertas de otros aliados que estén a menos de esta distancia (m).")]
    public float radioRespuestaAlerta = 30f;
    [Tooltip("Segundos que busca antes de volver a patrullar.")]
    public float duracionBusqueda = 12f;
    [Tooltip("Radio (m) alrededor de la última posición conocida en el que revisa waypoints.")]
    public float radioBusqueda = 25f;
    [Tooltip("Segundos que queda aturdido al recibir daño sin morir.")]
    public float tiempoAturdido = 0.6f;

    private static readonly float[] AngulosFlanqueo = { 0f, 75f, -75f, 150f, -150f };

    private readonly NavPathFollower rutaTactica = new NavPathFollower();
    private readonly List<WaypointNode> rutaGrafo = new List<WaypointNode>();
    private readonly List<WaypointNode> pendientesBusqueda = new List<WaypointNode>();
    private readonly List<WaypointNode> auxiliarNodos = new List<WaypointNode>();
    private int indiceRutaGrafo;
    private WaypointNode nodoActual;
    private WaypointNode nodoAnterior;
    private float tiempoHaciaNodo;

    private Vector3 puntoInvestigar;
    private bool faseInvestigar;
    private float tiempoBusqueda;
    private float tiempoSinVer;
    private int alertaProcesada;
    private float aturdidoHasta;
    private bool veJugador;
    private Vector3 destinoTactico;

    private Vector3 MiradaDuende
    {
        get { return transform.rotation * Quaternion.Euler(0f, -yawModelo, 0f) * Vector3.forward; }
    }

    private void InitTactico()
    {
        currentState = State.PATROL;
        homePosition = transform.position;
        alertaProcesada = RedTactica.ContadorAlertas;
        if (predictor == null) predictor = PredictorJugador.Instancia;

        GrafoWaypoints grafo = GrafoWaypoints.Actual;
        if (grafo != null) nodoActual = grafo.MasCercano(transform.position);
    }

    private Vector3 UpdateTactico(float dt)
    {
        if (Time.time < aturdidoHasta) return Brake();

        veJugador = VeAlJugador();
        if (veJugador)
        {
            tiempoSinVer = 0f;
            Vector3 v = predictor != null ? predictor.Velocidad : Vector3.zero;
            RedTactica.ReportarAvistamiento(player.position, v, this);
            alertaProcesada = RedTactica.ContadorAlertas; // su propia alerta no le hace "ir a investigar"
            if (currentState != State.SEEK) EntrarPersecucion();
        }
        else
        {
            tiempoSinVer += dt;
        }

        switch (currentState)
        {
            case State.SEEK:
            {
                // Visión compartida: mientras un aliado lo vea, sigue persiguiendo aunque él no lo vea.
                bool infoCompartida = RedTactica.HayAvistamiento(0.5f);
                if (!veJugador && !infoCompartida && tiempoSinVer > 1f)
                {
                    EntrarBusqueda(RedTactica.UltimaPosicion, RedTactica.UltimaVelocidad);
                    return TickBusqueda(dt);
                }
                return TickPersecucion(dt);
            }

            case State.SEARCH:
                ResponderAAlertas();
                return TickBusqueda(dt);

            default:
                currentState = State.PATROL;
                ResponderAAlertas();
                if (currentState == State.SEARCH) return TickBusqueda(dt);
                return TickPatrullaTactica(dt);
        }
    }

    private bool VeAlJugador()
    {
        if (player == null) return false;

        Vector3 ojo = transform.position + Vector3.up * alturaOjos;
        Vector3 pecho = Percepcion.PuntoPecho(player);

        if ((pecho - ojo).sqrMagnitude <= radioOido * radioOido) return true; // lo oye
        return Percepcion.PuedeVer(ojo, MiradaDuende, pecho, rangoVision, anguloVision, groundMask);
    }

    // Una alerta nueva de otro aliado cerca: va a investigar la posición reportada.
    private void ResponderAAlertas()
    {
        if (RedTactica.ContadorAlertas == alertaProcesada) return;
        alertaProcesada = RedTactica.ContadorAlertas;

        if (PlanarDistance(transform.position, RedTactica.UltimaPosicion) <= radioRespuestaAlerta)
        {
            EntrarBusqueda(RedTactica.UltimaPosicion, RedTactica.UltimaVelocidad);
        }
    }

    // ---------------- PATRULLA ----------------

    private Vector3 TickPatrullaTactica(float dt)
    {
        GrafoWaypoints grafo = GrafoWaypoints.Actual;
        if (grafo == null || grafo.Cantidad == 0)
        {
            return Wander(dt) * factorVelocidadPatrulla; // sin grafo: deambula como el duende clásico
        }

        if (nodoActual == null) nodoActual = grafo.MasCercano(transform.position);
        if (nodoActual == null) return Brake();

        tiempoHaciaNodo += dt;
        if (PlanarDistance(transform.position, nodoActual.Posicion) < 1.8f || tiempoHaciaNodo > 20f)
        {
            WaypointNode siguiente = grafo.VecinoAleatorioEnZona(nodoActual, nodoAnterior, homePosition, radioPatrulla);
            nodoAnterior = nodoActual;
            nodoActual = siguiente;
            tiempoHaciaNodo = 0f;
        }

        destinoTactico = nodoActual.Posicion;
        return SteerAlongPath(rutaTactica, destinoTactico, maxSpeed * factorVelocidadPatrulla, false, dt);
    }

    private void VolverAPatrulla()
    {
        currentState = State.PATROL;
        RedTactica.RetirarPerseguidor(this);
        GrafoWaypoints grafo = GrafoWaypoints.Actual;
        nodoActual = grafo != null ? grafo.MasCercano(transform.position) : null;
        nodoAnterior = null;
        tiempoHaciaNodo = 0f;
        rutaTactica.Reset();
    }

    // ---------------- PERSECUCIÓN ----------------

    private void EntrarPersecucion()
    {
        currentState = State.SEEK;
        RedTactica.RegistrarPerseguidor(this);
        rutaTactica.Reset();
    }

    private Vector3 TickPersecucion(float dt)
    {
        Vector3 objetivo = veJugador && player != null ? player.position : RedTactica.UltimaPosicion;

        // Persecución predictiva (pursuit): va a donde ESTARÁ el jugador en breve, no a donde está.
        float horizonte = Mathf.Clamp(PlanarDistance(transform.position, objetivo) / Mathf.Max(0.1f, maxSpeed), 0f, 1.5f);
        Vector3 futuro = predictor != null && predictor.Jugador != null ? predictor.Predecir(horizonte) : objetivo;
        Vector3 destino = futuro;

        if (usarFlanqueo)
        {
            int puesto = RedTactica.PuestoEnEscuadra(this);
            if (puesto > 0 && PlanarDistance(transform.position, futuro) > radioFlanqueo * 1.2f)
            {
                // Puesto de flanqueo relativo al rumbo del jugador (75° = delante a un lado: le corta el paso).
                Vector3 rumbo = predictor != null ? predictor.Velocidad : Vector3.zero;
                if (rumbo.sqrMagnitude < 1f) rumbo = transform.position - futuro;
                rumbo.y = 0f;
                if (rumbo.sqrMagnitude < 0.0001f) rumbo = Vector3.forward;

                float angulo = AngulosFlanqueo[Mathf.Min(puesto, AngulosFlanqueo.Length - 1)];
                Vector3 puestoPos = futuro + Quaternion.Euler(0f, angulo, 0f) * rumbo.normalized * radioFlanqueo;

                NavMeshHit hit;
                if (NavMesh.SamplePosition(puestoPos, out hit, 4f, NavMesh.AllAreas)) puestoPos = hit.position;
                destino = puestoPos;
            }
        }

        destinoTactico = destino;
        return SteerAlongPath(rutaTactica, destino, maxSpeed, false, dt);
    }

    // ---------------- BÚSQUEDA ----------------

    private void EntrarBusqueda(Vector3 ultimaPosicion, Vector3 ultimaVelocidad)
    {
        RedTactica.RetirarPerseguidor(this);
        currentState = State.SEARCH;
        tiempoBusqueda = 0f;
        rutaTactica.Reset();
        rutaGrafo.Clear();
        indiceRutaGrafo = 0;

        // 1) Predice hacia dónde siguió el jugador: última posición + velocidad (como mucho 8 m más allá).
        Vector3 extra = ultimaVelocidad;
        extra.y = 0f;
        extra = Vector3.ClampMagnitude(extra * 1.5f, 8f);
        puntoInvestigar = ultimaPosicion + extra;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(puntoInvestigar, out hit, 5f, NavMesh.AllAreas)) puntoInvestigar = hit.position;
        faseInvestigar = true;

        // 2) Elige qué waypoints revisar después: cercanos y, sobre todo, en la dirección en que huía.
        pendientesBusqueda.Clear();
        GrafoWaypoints grafo = GrafoWaypoints.Actual;
        if (grafo == null) return;

        grafo.NodosEnRadio(puntoInvestigar, radioBusqueda, auxiliarNodos);
        Vector3 dirHuida = extra.sqrMagnitude > 0.01f ? extra.normalized : Vector3.zero;
        Vector3 centro = puntoInvestigar;
        auxiliarNodos.Sort((a, b) => PuntajeBusqueda(a, centro, dirHuida).CompareTo(PuntajeBusqueda(b, centro, dirHuida)));
        for (int i = 0; i < auxiliarNodos.Count && pendientesBusqueda.Count < 5; i++) pendientesBusqueda.Add(auxiliarNodos[i]);
    }

    // Menor = se revisa antes.
    private static float PuntajeBusqueda(WaypointNode n, Vector3 centro, Vector3 dirHuida)
    {
        Vector3 d = n.Posicion - centro;
        d.y = 0f;
        float distancia = d.magnitude;
        float alineacion = distancia > 0.01f ? Vector3.Dot(dirHuida, d / distancia) : 0f;
        return distancia - 10f * alineacion;
    }

    private Vector3 TickBusqueda(float dt)
    {
        tiempoBusqueda += dt;
        if (tiempoBusqueda > duracionBusqueda)
        {
            VolverAPatrulla();
            return Brake();
        }

        float speed = maxSpeed * factorVelocidadBusqueda;

        if (faseInvestigar)
        {
            destinoTactico = puntoInvestigar;
            if (PlanarDistance(transform.position, puntoInvestigar) > 1.5f)
            {
                return SteerAlongPath(rutaTactica, puntoInvestigar, speed, true, dt);
            }
            faseInvestigar = false;
        }

        GrafoWaypoints grafo = GrafoWaypoints.Actual;
        if (grafo == null)
        {
            return Wander(dt) * factorVelocidadBusqueda;
        }

        if (indiceRutaGrafo >= rutaGrafo.Count)
        {
            if (pendientesBusqueda.Count == 0)
            {
                VolverAPatrulla();
                return Brake();
            }

            // Siguiente waypoint a revisar: la ruta entre waypoints la calcula NUESTRO A* sobre el grafo.
            WaypointNode meta = pendientesBusqueda[0];
            pendientesBusqueda.RemoveAt(0);
            WaypointNode inicio = grafo.MasCercano(transform.position);
            if (!grafo.BuscarRuta(inicio, meta, rutaGrafo))
            {
                rutaGrafo.Clear();
                rutaGrafo.Add(meta);
            }
            indiceRutaGrafo = 0;
        }

        WaypointNode paso = rutaGrafo[indiceRutaGrafo];
        if (paso == null || PlanarDistance(transform.position, paso.Posicion) < 1.8f)
        {
            indiceRutaGrafo++;
            return Seek(transform.position + new Vector3(velocity.x, 0f, velocity.z), speed);
        }

        destinoTactico = paso.Posicion;
        bool ultimo = indiceRutaGrafo == rutaGrafo.Count - 1;
        return SteerAlongPath(rutaTactica, paso.Posicion, speed, ultimo, dt);
    }

    // ---------------- Daño y rotación ----------------

    private void AlRecibirDanioTactico()
    {
        aturdidoHasta = Time.time + tiempoAturdido;
        // Le han atacado: sabe aproximadamente de dónde viene, aunque no lo vea.
        if (currentState != State.SEEK && player != null) EntrarBusqueda(player.position, Vector3.zero);
    }

    private void RotateTacticoBody(Vector3 horizontalVelocity, float dt)
    {
        Vector3 dir;
        if (currentState == State.SEEK && player != null && PlanarDistance(transform.position, player.position) < 6f)
        {
            dir = player.position - transform.position;
        }
        else if (horizontalVelocity.sqrMagnitude > 0.1f)
        {
            dir = horizontalVelocity;
        }
        else
        {
            return;
        }

        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        Quaternion objetivo = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, yawModelo, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, objetivo, 1f - Mathf.Exp(-8f * dt));
    }
}
