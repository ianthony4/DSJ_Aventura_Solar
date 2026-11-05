using UnityEngine;

// Mortero (mode = MORTAR). El resto de modos (duende) no se ve afectado por nada de este archivo.
//
// Ciclo:  PATRULLA (va a un nido por el NavMesh) -> ALERTA (apunta, aviso visible) -> ATAQUE (andanada) -> se reubica
//
// Física predictiva:
//   * Intercepción contra la posición FUTURA del jugador (PredictorJugador: línea recta + ruta probable hacia su objetivo).
//   * Varios arcos posibles (velocidad horizontal x factor): si el arco bajo choca con una colina, usa uno más alto.
//   * Patrones de andanada (horquillado, barrera, anillo, cuello de botella) elegidos según cómo se mueve el jugador.
// Waypoints / pathfinding:
//   * Los nidos son waypoints; el modo TACTICO los puntúa (distancia ideal, altura, visión, tiro posible, recorrido).
//   * Si no hay nidos asignados, usa los nodos del grafo de waypoints cercanos.
//   * Se desplaza entre nidos con rutas del NavMesh (rodea rocas y cráteres).
// Cooperación:
//   * Con "Requiere Vision" solo dispara si lo ve él o si un observador (dron) lo está viendo; si la información es vieja
//     hace fuego de supresión sobre la última posición conocida.
public partial class AIMob
{
    public enum PatronAndanada
    {
        DIRECTO,            // todos los disparos a la intercepción prevista
        HORQUILLADO,        // 1º largo, 2º corto, el resto directo (artillería clásica: "encuadrar" el blanco)
        BARRERA,            // línea de impactos perpendicular a la marcha del jugador: le corta el paso
        ANILLO,             // impactos alrededor del jugador y el último al centro (castiga quedarse quieto)
        CUELLO_DE_BOTELLA,  // el primer disparo cae en el punto estrecho de su ruta justo cuando él llega
        ADAPTATIVO          // elige uno de los anteriores en cada andanada según la situación
    }

    #region Campos del Mortero
    // ------------------------------------------------------------------------------------------
    // Solo se usan con mode = MORTAR.
    // ------------------------------------------------------------------------------------------

    [Header("Mortero - Nidos de tiro")]
    [Tooltip("Patrulla como el dron: recorre los waypoints (el grafo) a menos de Radio Patrulla Mortero de donde lo coloques, parándose en cada uno y disparando si estás a tiro. Con esto activado se IGNORA la lista Firing Nests. Desactívalo para usar nidos puestos a mano.")]
    public bool patrullarPorWaypoints = true;
    [Tooltip("Radio (m) alrededor de donde lo colocas dentro del cual patrulla los waypoints. Con waypoints cada ~30 m, 60 m da unos 13 puntos de parada.")]
    public float radioPatrullaMortero = 60f;
    [Tooltip("Nidos puestos a mano. Solo se usan con Patrullar Por Waypoints DESACTIVADO. Deben estar FUERA de la jerarquía del mortero (si no, se mueven con él). Si lo dejas vacío usa los nodos del grafo de waypoints cercanos.")]
    public Transform[] firingNests;
    [Tooltip("Regla para elegir el siguiente nido. TACTICO puntúa cada nido y elige el mejor (recomendado).")]
    public NestPattern nestPattern = NestPattern.CONDITIONAL;
    [Tooltip("Distancia horizontal (m) a la que se considera que llegó al nido.")]
    public float nestArrivalThreshold = 1.5f;
    [Tooltip("Segundos que espera en un nido antes de pasar al siguiente si no hay jugador a tiro.")]
    public float nestWaitTime = 2f;
    [Tooltip("Si no llega a un nido en estos segundos (atascado), lo da por alcanzado.")]
    public float nestTravelTimeout = 30f;
    [Tooltip("Sin nidos asignados: radio (m) alrededor de su posición inicial en el que toma nodos del grafo como nidos.")]
    public float radioNidosDelGrafo = 45f;

    [Header("Mortero - Cañón y munición")]
    [Tooltip("Objeto vacío que gira (su eje Z azul apunta por donde sale el proyectil). Hijo del mortero.")]
    public Transform cannonPivot;
    [Tooltip("Punta del cañón: desde aquí sale el proyectil. Hijo de Cannon Pivot.")]
    public Transform firePoint;
    public MortarProjectile projectilePrefab;
    [Tooltip("Aviso en el suelo (opcional pero recomendado).")]
    public ImpactIndicator indicatorPrefab;
    [Tooltip("Velocidad de giro del cañón (Slerp exponencial). Más alto = más ágil.")]
    public float cannonTurnSpeed = 6f;
    [Tooltip("Error angular máximo (grados) para poder disparar.")]
    public float aimToleranceDegrees = 3f;
    [Tooltip("Velocidad con la que el chasis gira hacia donde camina.")]
    public float bodyTurnSpeed = 5f;
    [Tooltip("Corrección de yaw del modelo (el duende necesita 180; un modelo que mira a +Z necesita 0).")]
    public float bodyYawOffset = 0f;

    [Header("Mortero - Balística")]
    [Tooltip("Velocidad horizontal fija del proyectil (m/s). Debe superar la velocidad máxima del jugador para poder alcanzarlo.")]
    public float shellHorizontalSpeed = 12f;
    [Tooltip("Velocidad de lanzamiento máxima: limita el alcance físico del mortero.")]
    public float maxLaunchSpeed = 40f;
    [Tooltip("Tiempo de vuelo mínimo (s): garantiza un aviso legible al jugador.")]
    public float minFlightTime = 1.2f;
    public float maxFlightTime = 8f;
    [Tooltip("Si está activo usa Physics.gravity (ConfiguracionPlaneta ya la ajusta por planeta).")]
    public bool useWorldGravity = true;
    public float customGravity = 3.7f;
    [Tooltip("Capas del suelo y obstáculos (SOLO Suelo). Nunca incluir Jugador ni Enemigo. También bloquean la visión.")]
    public LayerMask groundMask;
    [Tooltip("Altura sobre el punto consultado desde la que se lanza el rayo hacia el suelo.")]
    public float groundProbeUp = 50f;
    [Tooltip("Longitud del rayo hacia el suelo.")]
    public float groundProbeDistance = 500f;
    [Tooltip("Repeticiones para afinar la altura del suelo en el punto de impacto predicho (previsualización en el editor).")]
    public int leadRefineIterations = 2;
    [Tooltip("Segundos extra de vida del proyectil sobre el tiempo de vuelo calculado.")]
    public float fuseMargin = 1.5f;

    [Header("Mortero - Física predictiva")]
    [Tooltip("Predice hacia dónde va el jugador (moneda o nave) usando su ruta en el NavMesh, no solo en línea recta.")]
    public bool usarPrediccionPorRuta = true;
    [Tooltip("Arcos a probar, como factor de la velocidad horizontal: 1 = arco normal; menor = arco más alto y lento. Se usa el primero que no choca.")]
    public float[] arcSpeedFactors = { 1f, 0.75f, 0.55f };
    [Tooltip("Comprueba que la parábola no choque con colinas o rocas antes de disparar.")]
    public bool comprobarTrayectoria = true;
    [Tooltip("Cómo reparte los disparos de cada andanada.")]
    public PatronAndanada patronAndanada = PatronAndanada.ADAPTATIVO;

    [Header("Mortero - Combate")]
    [Tooltip("Distancia horizontal mínima de tiro (cerca del mortero se está a salvo). Mantenla >= Min Flight Time x Shell Horizontal Speed (~14 m con los valores por defecto), o entrará en ALERT sin poder disparar.")]
    public float engageMinRange = 15f;
    [Tooltip("Distancia horizontal máxima de detección y tiro.")]
    public float engageMaxRange = 45f;
    [Tooltip("Segundos de aviso en ALERT (cañón apuntando) antes de disparar.")]
    public float lockOnTime = 1.2f;
    public int salvoSize = 3;
    public float shotInterval = 1f;
    [Tooltip("Pausa tras el último disparo antes de reubicarse.")]
    public float postSalvoDelay = 0.8f;
    [Tooltip("Tiempo sin poder iniciar otra andanada tras terminar una.")]
    public float salvoCooldown = 4f;
    [Tooltip("Suavizado de la velocidad estimada del jugador (más alto = reacciona antes, más ruido).")]
    public float velocitySmoothing = 12f;

    [Header("Mortero - Visión y observadores")]
    [Tooltip("Solo dispara si él mismo ve al jugador (sin colinas en medio) o si un observador lo está viendo. Desactívalo para el comportamiento antiguo (siempre sabe dónde está).")]
    public bool requiereVision = true;
    [Tooltip("Antigüedad máxima (s) de un avistamiento de otro aliado para disparar con puntería predictiva.")]
    public float memoriaObservador = 1f;
    [Tooltip("Si nadie lo ve pero hubo un avistamiento reciente, bombardea la última posición conocida (sin adelanto).")]
    public bool fuegoDeSupresion = true;
    [Tooltip("Antigüedad máxima (s) de la información para el fuego de supresión.")]
    public float memoriaSupresion = 6f;

    [Header("Mortero - Perfil de planeta")]
    [Tooltip("Perfil de artillería propio de este mortero. Si está vacío usa el de ConfiguracionArtilleria (escena). Con un perfil, los valores de balística y combate de arriba se sobrescriben al iniciar.")]
    public PerfilArtilleria profileOverride;

    [Header("Mortero - Game Feel (los rellena el perfil)")]
    [Tooltip("Viento: aceleración horizontal constante que curva el tiro. El mortero lo compensa.")]
    public Vector3 windAcceleration = Vector3.zero;
    [Tooltip("1 = adelanta al jugador perfectamente; 0 = dispara a donde está ahora.")]
    [Range(0f, 1f)] public float leadAccuracy = 1f;
    [Tooltip("Dispersión aleatoria (m) del punto de impacto por disparo.")]
    public float aimScatter = 0f;
    public float blastRadiusMultiplier = 1f;
    public float damageMultiplier = 1f;

    [Header("Mortero - Anclajes de audio y efectos")]
    [Tooltip("Aviso sonoro al detectar al jugador (el cañón empieza a apuntar).")]
    public AudioCue lockOnCue;
    [Tooltip("Sonido de cada disparo.")]
    public AudioCue fireCue;
    [Tooltip("Fogonazo en la boca del cañón; se instancia en Fire Point.")]
    public GameObject muzzleFlashPrefab;
    public float muzzleFlashLifetime = 2f;

    #endregion

    // Estado interno del mortero
    private const float MaxAttackDuration = 12f;
    private bool mortarReady;
    private bool warnedNoGround;
    private Transform[] nidos = new Transform[0];
    private WaypointNode[] nodosPatrulla;                                              // los waypoints de la patrulla (paralelo a 'nidos')
    private System.Collections.Generic.Dictionary<WaypointNode, int> indiceNodo;
    private bool patrullaPorGrafo;
    private readonly System.Collections.Generic.List<int> vecinosCandidatos = new System.Collections.Generic.List<int>();
    private float[] nestScores = new float[0];
    private int currentNest = -1;
    private int previousNest = -1;
    private int nestDirection = 1;
    private bool atNest;
    private float nestWaitTimer;
    private float travelTimer;
    private float stateTimer;
    private float shotTimer;
    private float postSalvoTimer;
    private float cooldownTimer;
    private int shotsFired;
    private Quaternion cannonIdleLocalRotation = Quaternion.identity;
    private Vector3 homePosition;
    private readonly NavPathFollower nestPath = new NavPathFollower();

    // Seguimiento del jugador
    private Rigidbody playerBody;
    private bool playerBodyResolved;
    private Vector3 trackedVelocity;
    private Vector3 lastPlayerPosition;
    private bool hasLastPlayerPosition;
    private PredictorJugador predictor;

    // Información sobre el jugador (propia o de la red táctica)
    private bool seesPlayer;           // línea de visión propia este frame
    private bool hasFreshInfo;         // puede disparar con adelanto (lo ve él o un observador)
    private bool hasStaleInfo;         // solo sabe dónde estuvo (fuego de supresión)
    private Vector3 knownPlayerPosition;

    // Andanada en curso
    private PatronAndanada patronActual = PatronAndanada.DIRECTO;
    private PatronAndanada patronAnterior = PatronAndanada.DIRECTO;
    private bool salvoIsSuppression;
    private Vector3 salvoForward = Vector3.forward;
    private Vector3 salvoSide = Vector3.right;

    // Datos solo para Gizmos
    private BallisticSolver.Solution lastSolution;
    private Vector3 lastSolveOrigin;
    private bool hasLastSolution;
    private float lastArcFactor = 1f;
    private bool lastShotBlocked;
    private Vector3 lastBlockPoint;
    private bool lastWasChokepoint;
    private readonly Vector3[] trajectoryBuffer = new Vector3[40];

    // Dispersión del disparo en curso (se sortea una vez por disparo para que el cañón no tiemble)
    private Vector3 scatterOffset;

    /// <summary>Se dispara cada vez que este mortero lanza un proyectil (aviso en el HUD, audio global, etc.).</summary>
    public event System.Action<MortarProjectile> ShellFired;

    #region Mortero: inicialización y utilidades

    private bool HasNests { get { return nidos != null && nidos.Length > 0; } }
    private bool HasMultipleNests { get { return nidos != null && nidos.Length > 1; } }
    private float BlastRadiusEff { get { return (projectilePrefab != null ? projectilePrefab.BlastRadius : 4f) * blastRadiusMultiplier; } }

    private void InitMortar()
    {
        currentState = State.PATROL;
        homePosition = transform.position;
        ApplyArtilleryProfile();
        RollScatter();
        mortarReady = ValidateMortarSetup();
        predictor = PredictorJugador.Instancia;

        if (cannonPivot != null) cannonIdleLocalRotation = cannonPivot.localRotation;

        cooldownTimer = 0f;
        nestWaitTimer = 0f;
        travelTimer = 0f;

        nidos = BuildNestList();
        nestScores = new float[nidos.Length];
        currentNest = HasNests ? NearestNestIndex() : -1;
        atNest = (currentNest < 0); // sin nidos válidos funciona como torreta fija
    }

    // Nidos asignados a mano; si no hay, nodos del grafo de waypoints cercanos (primero los marcados como candidatos).
    private Transform[] BuildNestList()
    {
        System.Collections.Generic.List<Transform> lista = new System.Collections.Generic.List<Transform>();
        patrullaPorGrafo = false;
        nodosPatrulla = null;
        indiceNodo = null;

        // Patrulla por waypoints (como el dron): todos los nodos del grafo a menos de radioPatrullaMortero de donde está colocado.
        if (patrullarPorWaypoints)
        {
            GrafoWaypoints grafoPatrulla = GrafoWaypoints.Actual;
            if (grafoPatrulla != null)
            {
                System.Collections.Generic.List<WaypointNode> enRadio = new System.Collections.Generic.List<WaypointNode>();
                grafoPatrulla.NodosEnRadio(homePosition, radioPatrullaMortero, enRadio);
                if (enRadio.Count > 0)
                {
                    nodosPatrulla = enRadio.ToArray();
                    indiceNodo = new System.Collections.Generic.Dictionary<WaypointNode, int>();
                    for (int i = 0; i < nodosPatrulla.Length; i++)
                    {
                        lista.Add(nodosPatrulla[i].transform);
                        indiceNodo[nodosPatrulla[i]] = i;
                    }
                    patrullaPorGrafo = true;
                    Debug.Log(name + ": patrulla " + lista.Count + " waypoints a menos de " + radioPatrullaMortero.ToString("0") + " m de donde está colocado.", this);
                    return lista.ToArray();
                }
            }
            Debug.LogWarning(name + ": no hay waypoints a menos de " + radioPatrullaMortero.ToString("0") + " m. Genera el grafo (Solar Adventure > IA Táctica > 2), acerca el mortero a los waypoints o sube Radio Patrulla Mortero. Mientras tanto usa los nidos a mano o el grafo cercano.", this);
        }

        if (firingNests != null)
        {
            for (int i = 0; i < firingNests.Length; i++) if (firingNests[i] != null) lista.Add(firingNests[i]);
        }
        if (lista.Count > 0) return lista.ToArray();

        GrafoWaypoints grafo = GrafoWaypoints.Actual;
        if (grafo == null) return lista.ToArray();

        System.Collections.Generic.List<WaypointNode> cercanos = new System.Collections.Generic.List<WaypointNode>();
        grafo.NodosEnRadio(homePosition, radioNidosDelGrafo, cercanos);

        for (int i = 0; i < cercanos.Count; i++) if (cercanos[i].candidatoNido) lista.Add(cercanos[i].transform);
        if (lista.Count < 3)
        {
            // Pocos promontorios: se completan con los nodos más cercanos (máximo 8).
            cercanos.Sort((a, b) => PlanarDistance(a.Posicion, homePosition).CompareTo(PlanarDistance(b.Posicion, homePosition)));
            for (int i = 0; i < cercanos.Count && lista.Count < 8; i++)
            {
                if (!lista.Contains(cercanos[i].transform)) lista.Add(cercanos[i].transform);
            }
        }
        if (lista.Count > 0) Debug.Log(name + ": sin nidos asignados, usa " + lista.Count + " nodos del grafo como nidos.", this);
        return lista.ToArray();
    }

    // Copia al mortero los valores del perfil del planeta (el del propio mortero tiene prioridad sobre el de la escena).
    private void ApplyArtilleryProfile()
    {
        PerfilArtilleria profile = profileOverride;
        if (profile == null && ConfiguracionArtilleria.Actual != null) profile = ConfiguracionArtilleria.Actual.perfil;
        if (profile == null) return;

        useWorldGravity = profile.useWorldGravity;
        customGravity = profile.gravity;
        shellHorizontalSpeed = profile.shellHorizontalSpeed;
        maxLaunchSpeed = profile.maxLaunchSpeed;
        minFlightTime = profile.minFlightTime;
        maxFlightTime = profile.maxFlightTime;
        windAcceleration = new Vector3(profile.wind.x, 0f, profile.wind.z);
        blastRadiusMultiplier = profile.blastRadiusMultiplier;
        damageMultiplier = profile.damageMultiplier;
        engageMinRange = profile.engageMinRange;
        engageMaxRange = profile.engageMaxRange;
        lockOnTime = profile.lockOnTime;
        salvoSize = profile.salvoSize;
        shotInterval = profile.shotInterval;
        salvoCooldown = profile.salvoCooldown;
        leadAccuracy = profile.leadAccuracy;
        aimScatter = profile.aimScatter;
    }

    // Sortea la dispersión del próximo disparo (círculo de radio aimScatter alrededor del punto previsto).
    private void RollScatter()
    {
        float radius = salvoIsSuppression ? aimScatter * 2f + 1.5f : aimScatter;
        if (radius <= 0f)
        {
            scatterOffset = Vector3.zero;
            return;
        }

        Vector2 circle = Random.insideUnitCircle * radius;
        scatterOffset = new Vector3(circle.x, 0f, circle.y);
    }

    private bool ValidateMortarSetup()
    {
        bool ok = true;

        if (cannonPivot == null) { Debug.LogError("AIMob (mortero): falta asignar Cannon Pivot.", this); ok = false; }
        if (firePoint == null) { Debug.LogError("AIMob (mortero): falta asignar Fire Point.", this); ok = false; }
        if (projectilePrefab == null) { Debug.LogError("AIMob (mortero): falta asignar Projectile Prefab.", this); ok = false; }

        if (indicatorPrefab == null)
            Debug.LogWarning("AIMob (mortero): sin Indicator Prefab el jugador no verá el aviso de impacto.", this);
        if (groundMask.value == 0)
            Debug.LogWarning("AIMob (mortero): Ground Mask vacío; el mortero apuntará a la altura del jugador y no al suelo, y verá a través de las colinas.", this);

        return ok;
    }

    private float CurrentGravity()
    {
        return useWorldGravity ? Mathf.Abs(Physics.gravity.y) : customGravity;
    }

    // Suelo bajo (o sobre) una posición. Devuelve false si el rayo no encuentra nada en groundMask.
    private bool TryGetGround(Vector3 position, out float groundY, out Vector3 normal)
    {
        Vector3 start = new Vector3(position.x, position.y + groundProbeUp, position.z);
        RaycastHit hit;
        if (Physics.Raycast(start, Vector3.down, out hit, groundProbeDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            groundY = hit.point.y;
            normal = hit.normal;
            return true;
        }

        groundY = position.y;
        normal = Vector3.up;
        return false;
    }

    private Vector3 SnapToGround(Vector3 p)
    {
        float y;
        Vector3 n;
        if (TryGetGround(p, out y, out n)) p.y = y;
        else WarnNoGroundOnce();
        return p;
    }

    private void WarnNoGroundOnce()
    {
        if (warnedNoGround) return;
        warnedNoGround = true;
        Debug.LogWarning("AIMob (mortero): no se encontró suelo bajo el objetivo. Revisa que el terreno esté en una capa incluida en Ground Mask.", this);
    }

    private Vector3 MortarEye()
    {
        return firePoint != null ? firePoint.position : transform.position + Vector3.up * 2f;
    }

    // Velocidad del jugador: Rigidbody si lo tiene (exacta), si no por diferencia de posiciones; ambas suavizadas.
    private void TrackPlayer(float dt)
    {
        if (player == null)
        {
            hasLastPlayerPosition = false;
            trackedVelocity = Vector3.zero;
            return;
        }

        if (!playerBodyResolved)
        {
            playerBody = player.GetComponentInParent<Rigidbody>();
            playerBodyResolved = true;
        }

        if (dt <= 0.00001f) return; // juego en pausa

        Vector3 position = player.position;
        Vector3 measured;
        if (playerBody != null) measured = playerBody.linearVelocity;
        else if (hasLastPlayerPosition) measured = (position - lastPlayerPosition) / dt;
        else measured = Vector3.zero;

        float k = 1f - Mathf.Exp(-velocitySmoothing * dt);
        trackedVelocity = Vector3.Lerp(trackedVelocity, measured, k);

        lastPlayerPosition = position;
        hasLastPlayerPosition = true;
    }

    // ¿Qué sabe el mortero del jugador este frame? Visión propia, observadores (red táctica) o recuerdo.
    private void UpdatePlayerKnowledge()
    {
        seesPlayer = false;
        hasFreshInfo = false;
        hasStaleInfo = false;
        if (player == null) return;

        float d = PlanarDistance(transform.position, player.position);
        if (d <= engageMaxRange * 1.15f && Percepcion.HayLineaDeVision(MortarEye(), Percepcion.PuntoPecho(player), groundMask))
        {
            seesPlayer = true;
            Vector3 v = predictor != null ? predictor.Velocidad : trackedVelocity;
            RedTactica.ReportarAvistamiento(player.position, v, this);
        }

        if (seesPlayer || !requiereVision || RedTactica.HayAvistamiento(memoriaObservador))
        {
            hasFreshInfo = true;
            knownPlayerPosition = player.position;
        }
        else if (fuegoDeSupresion && RedTactica.HayAvistamiento(memoriaSupresion))
        {
            hasStaleInfo = true;
            knownPlayerPosition = RedTactica.UltimaPosicion;
        }
    }

    #endregion

    #region Mortero: FSM (Patrulla / Alerta / Atacar)

    private Vector3 UpdateMortar(float dt)
    {
        TrackPlayer(dt);
        UpdatePlayerKnowledge();

        if (!mortarReady)
        {
            currentState = State.PATROL;
            return Brake();
        }

        float playerDistance = (hasFreshInfo || hasStaleInfo) ? PlanarDistance(transform.position, knownPlayerPosition) : float.MaxValue;

        switch (currentState)
        {
            case State.ALERT:  return TickAlert(dt, playerDistance);
            case State.ATTACK: return TickAttack(dt, playerDistance);
            default:           return TickPatrol(dt, playerDistance);
        }
    }

    // PATRULLA: camina hasta el nido actual; allí espera. Solo abre fuego cuando está parado en un nido y el jugador está a tiro.
    private Vector3 TickPatrol(float dt, float playerDistance)
    {
        currentState = State.PATROL;
        cooldownTimer -= dt;
        ReturnCannonToIdle(dt);

        if (!atNest)
        {
            Transform nest = CurrentNest();
            if (nest == null)
            {
                atNest = true;
                nestWaitTimer = 0f;
                return Brake();
            }

            travelTimer += dt;
            if (PlanarDistance(transform.position, nest.position) <= nestArrivalThreshold || travelTimer >= nestTravelTimeout)
            {
                atNest = true;
                nestWaitTimer = 0f;
                return Brake();
            }

            return SteerAlongPath(nestPath, nest.position, maxSpeed, true, dt);
        }

        nestWaitTimer += dt;

        bool playerInBand = playerDistance >= engageMinRange && playerDistance <= engageMaxRange;
        if (playerInBand && cooldownTimer <= 0f)
        {
            EnterAlert();
            return Brake();
        }

        // Sabe dónde está el jugador pero no está a tiro desde aquí: no espera, busca un nido mejor.
        bool knowsButOutOfBand = (hasFreshInfo || hasStaleInfo) && !playerInBand;
        float wait = knowsButOutOfBand ? nestWaitTime * 0.5f : nestWaitTime;
        if (HasMultipleNests && nestWaitTimer >= wait)
        {
            GoToNextNest();
        }

        return Brake();
    }

    private void EnterAlert()
    {
        currentState = State.ALERT;
        stateTimer = 0f;
        salvoIsSuppression = !hasFreshInfo;
        RollScatter();
        if (lockOnCue != null) lockOnCue.PlayAt(transform.position);

        Color aviso = new Color(1f, 0.75f, 0.15f);
        if (salvoIsSuppression) HUDTactico.MensajeUnico("mortero_supresion", "Fuego de supresión sobre tu última posición", aviso, 8f);
        else if (!seesPlayer) HUDTactico.MensajeUnico("mortero_guiado", "Un mortero dispara guiado por el dron", aviso, 8f);
        else HUDTactico.MensajeUnico("mortero_apunta", "¡Un mortero te apunta!", aviso, 8f);
    }

    // ALERTA: el cañón apunta al punto de intercepción ya predicho (el jugador ve hacia dónde va a disparar).
    private Vector3 TickAlert(float dt, float playerDistance)
    {
        currentState = State.ALERT;
        stateTimer += dt;

        if ((!hasFreshInfo && !hasStaleInfo) || playerDistance > engageMaxRange * 1.15f)
        {
            // Perdió al jugador: vuelve a patrullar sin penalización.
            currentState = State.PATROL;
            stateTimer = 0f;
            nestWaitTimer = 0f;
            return Brake();
        }

        // Si mientras apunta un observador lo ve, pasa de supresión a fuego predictivo.
        if (hasFreshInfo) salvoIsSuppression = false;

        BallisticSolver.Solution solution;
        bool solved = TrySolveShot(0, out solution);

        if (solved)
        {
            AimCannonAt(solution.launchVelocity, dt);
        }
        else
        {
            Vector3 toPlayer = knownPlayerPosition - transform.position;
            toPlayer.y = 0f;
            AimCannonAt(toPlayer.normalized + Vector3.up * 0.7f, dt);
        }

        if (stateTimer >= lockOnTime)
        {
            if (solved && playerDistance >= engageMinRange) EnterAttack();
            else FinishSalvo(); // sin solución (demasiado cerca/rápido/lejos o el arco choca): se reubica
        }

        return Brake();
    }

    private void EnterAttack()
    {
        currentState = State.ATTACK;
        stateTimer = 0f;
        shotsFired = 0;
        shotTimer = 0f;
        postSalvoTimer = 0f;

        Vector3 forward = knownPlayerPosition - transform.position;
        forward.y = 0f;
        salvoForward = forward.sqrMagnitude > 0.01f ? forward.normalized : transform.forward;
        salvoSide = Vector3.Cross(Vector3.up, salvoForward);

        patronAnterior = patronActual;
        patronActual = salvoIsSuppression ? PatronAndanada.DIRECTO : ChoosePattern();
    }

    // ATACAR: por cada disparo recalcula la solución, gira el cañón con Slerp y dispara cuando está alineado.
    private Vector3 TickAttack(float dt, float playerDistance)
    {
        currentState = State.ATTACK;
        stateTimer += dt;

        if ((!hasFreshInfo && !hasStaleInfo) || stateTimer > MaxAttackDuration)
        {
            FinishSalvo();
            return Brake();
        }

        // La supresión es corta: dos disparos como mucho.
        int shotsThisSalvo = salvoIsSuppression ? Mathf.Min(2, salvoSize) : salvoSize;

        if (shotsFired < shotsThisSalvo)
        {
            BallisticSolver.Solution solution;
            bool inBand = playerDistance >= engageMinRange && playerDistance <= engageMaxRange * 1.15f;
            if (!inBand || !TrySolveShot(shotsFired, out solution))
            {
                FinishSalvo();
                return Brake();
            }

            AimCannonAt(solution.launchVelocity, dt);

            shotTimer -= dt;
            if (shotTimer <= 0f && IsCannonAligned(solution.launchVelocity))
            {
                FireShell(solution);
                shotsFired++;
                shotTimer = shotInterval;
                RollScatter();
            }
        }
        else
        {
            postSalvoTimer += dt;
            if (postSalvoTimer >= postSalvoDelay) FinishSalvo();
        }

        return Brake();
    }

    // Termina la andanada (o la abandona) y se reubica: "dispara y cambia de posición".
    private void FinishSalvo()
    {
        cooldownTimer = salvoCooldown;
        currentState = State.PATROL;
        stateTimer = 0f;

        if (HasMultipleNests)
        {
            GoToNextNest();
        }
        else
        {
            atNest = true; // un solo nido (o ninguno): se queda y espera el enfriamiento
            nestWaitTimer = 0f;
        }
    }

    #endregion

    #region Mortero: nidos de tiro (waypoints)

    private Transform CurrentNest()
    {
        if (!HasNests || currentNest < 0 || currentNest >= nidos.Length) return null;
        return nidos[currentNest];
    }

    private int NearestNestIndex()
    {
        int best = -1;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < nidos.Length; i++)
        {
            if (nidos[i] == null) continue;
            float d = PlanarDistance(transform.position, nidos[i].position);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = i;
            }
        }
        return best;
    }

    private void GoToNextNest()
    {
        nestWaitTimer = 0f;

        int next = ChooseNextNest();
        if (next < 0 || next >= nidos.Length || nidos[next] == null || next == currentNest) return;

        previousNest = currentNest;
        currentNest = next;
        atNest = false;
        travelTimer = 0f;
        nestPath.Reset();
    }

    private int ChooseNextNest()
    {
        int count = nidos.Length;
        if (count <= 1) return currentNest;

        if (patrullaPorGrafo) return ChooseNeighborWaypoint();

        switch (nestPattern)
        {
            case NestPattern.CIRCULAR:
                return (currentNest + 1) % count;

            case NestPattern.CONDITIONAL:
            {
                int conditional = ChooseConditionalNest();
                return conditional >= 0 ? conditional : NextPingPong(count);
            }

            case NestPattern.TACTICO:
                return ChooseTacticalNest();

            default:
                return NextPingPong(count);
        }
    }

    // Recorre 0..n-1 y vuelve, invirtiendo el sentido en los extremos.
    private int NextPingPong(int count)
    {
        int next = currentNest + nestDirection;
        if (next >= count || next < 0)
        {
            nestDirection = -nestDirection;
            next = currentNest + nestDirection;
        }
        return Mathf.Clamp(next, 0, count - 1);
    }

    // Patrulla por waypoints: el siguiente punto es un VECINO del waypoint actual (dentro del radio de patrulla).
    // Si sabe dónde estás (lo ve él, un observador o lo recuerda), de los vecinos elige el mejor para disparar;
    // si no, pasea al azar sin volver atrás cuando hay otra opción.
    private int ChooseNeighborWaypoint()
    {
        vecinosCandidatos.Clear();
        WaypointNode actual = (currentNest >= 0 && currentNest < nodosPatrulla.Length) ? nodosPatrulla[currentNest] : null;
        if (actual != null)
        {
            for (int k = 0; k < actual.vecinos.Count; k++)
            {
                int idx;
                if (actual.vecinos[k] != null && indiceNodo.TryGetValue(actual.vecinos[k], out idx) && idx != currentNest)
                {
                    vecinosCandidatos.Add(idx);
                }
            }
        }

        if (vecinosCandidatos.Count == 0)
        {
            // Sin vecinos dentro del radio: va al waypoint más cercano que no sea el actual.
            int cercano = -1;
            float mejorD = float.MaxValue;
            for (int i = 0; i < nidos.Length; i++)
            {
                if (i == currentNest || nidos[i] == null) continue;
                float d = PlanarDistance(transform.position, nidos[i].position);
                if (d < mejorD) { mejorD = d; cercano = i; }
            }
            return cercano;
        }

        if (hasFreshInfo || hasStaleInfo)
        {
            int mejor = vecinosCandidatos[0];
            float mejorPuntaje = float.MinValue;
            for (int k = 0; k < vecinosCandidatos.Count; k++)
            {
                int i = vecinosCandidatos[k];
                nestScores[i] = ScoreNest(i);
                if (nestScores[i] > mejorPuntaje) { mejorPuntaje = nestScores[i]; mejor = i; }
            }
            return mejor;
        }

        if (vecinosCandidatos.Count > 1) vecinosCandidatos.Remove(previousNest);
        return vecinosCandidatos[Random.Range(0, vecinosCandidatos.Count)];
    }

    // Elige el nido (distinto del actual) cuya distancia al jugador cae en la banda de tiro,
    // prefiriendo una distancia cómoda (60 % del rango). Si ninguno cumple, devuelve -1 y se usa ping-pong.
    private int ChooseConditionalNest()
    {
        if (player == null) return -1;

        float ideal = Mathf.Lerp(engageMinRange, engageMaxRange, 0.6f);
        int best = -1;
        float bestScore = float.MaxValue;

        for (int i = 0; i < nidos.Length; i++)
        {
            if (i == currentNest || nidos[i] == null) continue;

            float d = PlanarDistance(nidos[i].position, player.position);
            if (d < engageMinRange || d > engageMaxRange) continue;

            float score = Mathf.Abs(d - ideal);
            if (score < bestScore)
            {
                bestScore = score;
                best = i;
            }
        }
        return best;
    }

    // TACTICO: puntúa todos los nidos y elige el mejor. Los puntajes se ven en la Scene con el mortero seleccionado.
    private int ChooseTacticalNest()
    {
        int best = -1;
        float bestScore = float.MinValue;
        for (int i = 0; i < nidos.Length; i++)
        {
            if (nidos[i] == null) continue;
            nestScores[i] = (i == currentNest) ? float.MinValue : ScoreNest(i);
            if (nestScores[i] > bestScore)
            {
                bestScore = nestScores[i];
                best = i;
            }
        }
        return best;
    }

    /// <summary>
    /// Puntaje táctico de un nido (más alto = mejor):
    ///   + banda de tiro: cerca de la distancia ideal al jugador (60 % del rango)
    ///   + altura sobre el jugador (más alcance y mejor ángulo)
    ///   + línea de visión hacia el jugador (no necesita observador)
    ///   + existe un tiro posible desde ahí (balística + trayectoria libre)
    ///   - recorrido a pie por el NavMesh (exponerse caminando)
    ///   - haberlo usado hace poco (ser impredecible)
    ///   - estar dentro de un cráter reciente
    /// Sin información del jugador: patrulla prefiriendo nidos no usados y no muy lejanos.
    /// </summary>
    private float ScoreNest(int i)
    {
        Vector3 nest = nidos[i].position;
        float score = 0f;

        float travel = NavPathFollower.PathLength(transform.position, nest);
        score -= 0.6f * travel / Mathf.Max(1f, engageMaxRange);
        if (i == previousNest) score -= 0.8f;
        if (CraterNavMesh.Contiene(nest, 1f)) score -= 2f;

        if (!hasFreshInfo && !hasStaleInfo)
        {
            return score + Random.Range(0f, 0.3f); // patrulla con algo de variedad
        }

        Vector3 target = knownPlayerPosition;
        if (hasFreshInfo && predictor != null) target = predictor.Predecir(2f); // donde estará cuando lleguemos aprox.

        float d = PlanarDistance(nest, target);
        float ideal = Mathf.Lerp(engageMinRange, engageMaxRange, 0.6f);
        float halfBand = Mathf.Max(1f, (engageMaxRange - engageMinRange) * 0.5f);
        float band = 1f - Mathf.Abs(d - ideal) / halfBand;          // 1 en la distancia ideal, 0 en los bordes
        if (d < engageMinRange || d > engageMaxRange) band -= 1.5f;  // fuera de la banda: muy malo
        score += 2f * band;

        score += 0.8f * Mathf.Clamp((nest.y - target.y) / 10f, -1f, 1f);

        Vector3 eye = nest + Vector3.up * 2f;
        if (Percepcion.HayLineaDeVision(eye, target + Vector3.up * Percepcion.AlturaPecho, groundMask)) score += 0.7f;

        // ¿Hay tiro desde ahí? (objetivo quieto, arco normal y alto)
        Vector3 groundTarget = SnapToGround(target);
        bool feasible = false;
        float g = CurrentGravity();
        for (int k = 0; k < ArcCount && !feasible; k++)
        {
            float speed = shellHorizontalSpeed * ArcFactor(k);
            BallisticSolver.Solution s = BallisticSolver.SolveLeadShot(eye, groundTarget, Vector3.zero, speed, g,
                windAcceleration, minFlightTime, maxFlightTime, maxLaunchSpeed);
            if (!s.IsValid) continue;
            Vector3 block = Vector3.zero;
            feasible = !comprobarTrayectoria || BallisticSolver.IsTrajectoryClear(eye, s.launchVelocity, g, windAcceleration,
                s.flightTime, s.aimPoint, groundMask, 0.25f, 12, BlastRadiusEff, out block);
        }
        score += feasible ? 1f : -1f;

        return score;
    }

    #endregion

    #region Mortero: puntería y disparo

    private int ArcCount { get { return (arcSpeedFactors != null && arcSpeedFactors.Length > 0) ? arcSpeedFactors.Length : 1; } }

    private float ArcFactor(int k)
    {
        if (arcSpeedFactors == null || arcSpeedFactors.Length == 0) return 1f;
        return Mathf.Clamp(arcSpeedFactors[k], 0.2f, 2f);
    }

    // Elige el patrón de la andanada según cómo se mueve el jugador.
    private PatronAndanada ChoosePattern()
    {
        if (patronAndanada != PatronAndanada.ADAPTATIVO) return patronAndanada;

        Vector3 vel = predictor != null ? predictor.Velocidad : new Vector3(trackedVelocity.x, 0f, trackedVelocity.z);
        float speed = vel.magnitude;

        PatronAndanada elegido;
        BallisticSolver.Solution unused;
        if (speed < 1.5f)
        {
            elegido = PatronAndanada.ANILLO;            // quieto o casi: lo rodeamos
        }
        else if (usarPrediccionPorRuta && TrySolveChokepoint(MortarEye(), out unused))
        {
            elegido = PatronAndanada.CUELLO_DE_BOTELLA; // sabemos a dónde va y pasa por un sitio estrecho
        }
        else if (Mathf.Abs(Vector3.Dot(vel / speed, salvoSide)) > 0.6f)
        {
            elegido = PatronAndanada.BARRERA;           // cruza delante del mortero: le cortamos el paso
        }
        else
        {
            elegido = PatronAndanada.HORQUILLADO;       // viene o se va en línea: encuadramos
        }

        // Variedad: no repetir siempre lo mismo.
        if (elegido == patronAnterior && elegido != PatronAndanada.CUELLO_DE_BOTELLA && Random.value < 0.35f)
        {
            elegido = elegido == PatronAndanada.HORQUILLADO ? PatronAndanada.BARRERA : PatronAndanada.HORQUILLADO;
        }
        return elegido;
    }

    // Desplazamiento del disparo i dentro del patrón actual (en metros, sobre el suelo).
    private Vector3 PatternOffset(int shotIndex)
    {
        int n = Mathf.Max(1, salvoSize);
        float blast = BlastRadiusEff;

        switch (patronActual)
        {
            case PatronAndanada.HORQUILLADO:
                if (shotIndex == 0) return salvoForward * (blast * 1.6f);   // largo
                if (shotIndex == 1) return -salvoForward * (blast * 1.0f);  // corto
                return Vector3.zero;                                        // centrado

            case PatronAndanada.BARRERA:
            {
                Vector3 vel = predictor != null ? predictor.Velocidad : trackedVelocity;
                vel.y = 0f;
                Vector3 across = vel.sqrMagnitude > 0.25f ? Vector3.Cross(Vector3.up, vel.normalized) : salvoSide;
                float k = shotIndex - (n - 1) * 0.5f;
                return across * (k * blast * 1.7f);
            }

            case PatronAndanada.ANILLO:
            {
                if (n == 1 || shotIndex >= n - 1) return Vector3.zero;     // el último, al centro
                float angle = (Mathf.PI * 2f) * shotIndex / (n - 1);
                return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (blast * 1.8f);
            }

            default:
                return Vector3.zero;
        }
    }

    // Posición futura del jugador en t segundos (con ruta probable si está disponible).
    private Vector3 PredictPlayer(float t)
    {
        if (usarPrediccionPorRuta && predictor != null && predictor.Jugador != null) return predictor.Predecir(t);
        Vector3 v = predictor != null ? predictor.Velocidad : new Vector3(trackedVelocity.x, 0f, trackedVelocity.z);
        return player.position + v * t;
    }

    /// <summary>
    /// Intercepción por punto fijo: t = distancia(origen, P(t)) / velocidad. Se itera porque P(t) (la posición futura)
    /// depende de t. Converge mientras el jugador sea más lento que el proyectil.
    /// leadAccuracy mezcla entre "donde está ahora" (0) y "donde estará" (1).
    /// </summary>
    private Vector3 PredictIntercept(Vector3 origin, float horizontalSpeed)
    {
        Vector3 now = player.position;
        Vector3 p = now;
        float t = PlanarDistance(now, origin) / horizontalSpeed;
        for (int k = 0; k < 6; k++)
        {
            p = Vector3.Lerp(now, PredictPlayer(t), leadAccuracy);
            float nt = PlanarDistance(p, origin) / horizontalSpeed;
            if (Mathf.Abs(nt - t) < 0.01f) break;
            t = nt;
        }
        return p;
    }

    /// <summary>
    /// Solución del disparo número shotIndex de la andanada actual. Prueba los arcos (de bajo a alto) y se queda
    /// con el primero cuya parábola no choca. Devuelve false si ninguno sirve.
    /// </summary>
    private bool TrySolveShot(int shotIndex, out BallisticSolver.Solution solution)
    {
        solution = default(BallisticSolver.Solution);
        if (firePoint == null) return false;
        if (player == null && !salvoIsSuppression) return false;

        Vector3 origin = firePoint.position;
        lastSolveOrigin = origin;
        lastWasChokepoint = false;

        if (!salvoIsSuppression && patronActual == PatronAndanada.CUELLO_DE_BOTELLA && shotIndex == 0)
        {
            if (TrySolveChokepoint(origin, out solution))
            {
                lastWasChokepoint = true;
                lastSolution = solution;
                hasLastSolution = true;
                return true;
            }
        }

        float g = CurrentGravity();
        Vector3 offset = salvoIsSuppression ? Vector3.zero : PatternOffset(shotIndex);
        BallisticSolver.Solution firstValid = default(BallisticSolver.Solution);
        bool anyValid = false;
        lastShotBlocked = false;

        float playerSpeed = predictor != null ? predictor.Rapidez : new Vector3(trackedVelocity.x, 0f, trackedVelocity.z).magnitude;

        for (int k = 0; k < ArcCount; k++)
        {
            float speed = shellHorizontalSpeed * ArcFactor(k);
            // Un arco más lento que el jugador no puede alcanzarlo (la intercepción no converge).
            if (!salvoIsSuppression && playerSpeed >= speed * 0.95f) continue;

            Vector3 aim = salvoIsSuppression ? knownPlayerPosition : PredictIntercept(origin, speed);
            Vector3 target = SnapToGround(aim + offset + scatterOffset);

            BallisticSolver.Solution s = BallisticSolver.SolveLeadShot(origin, target, Vector3.zero, speed, g,
                windAcceleration, minFlightTime, maxFlightTime, maxLaunchSpeed);
            if (!s.IsValid)
            {
                if (!anyValid) firstValid = s; // guarda el motivo para los Gizmos
                continue;
            }

            Vector3 block = Vector3.zero;
            bool clear = !comprobarTrayectoria || BallisticSolver.IsTrajectoryClear(origin, s.launchVelocity, g,
                windAcceleration, s.flightTime, s.aimPoint, groundMask, 0.25f, 16, BlastRadiusEff, out block);

            if (clear)
            {
                solution = s;
                lastArcFactor = ArcFactor(k);
                lastSolution = s;
                hasLastSolution = true;
                return true;
            }

            if (!anyValid)
            {
                anyValid = true;
                firstValid = s;
                lastBlockPoint = block;
            }
        }

        // Ningún arco libre: no dispara contra la colina (se reubicará).
        lastShotBlocked = anyValid;
        lastSolution = firstValid;
        hasLastSolution = true;
        solution = firstValid;
        return false;
    }

    // Cuello de botella: el proyectil llega al punto estrecho de la ruta del jugador justo cuando él pasa.
    private bool TrySolveChokepoint(Vector3 origin, out BallisticSolver.Solution solution)
    {
        solution = default(BallisticSolver.Solution);
        if (predictor == null) return false;

        float minFactor = 1f, maxFactor = 1f;
        for (int k = 0; k < ArcCount; k++)
        {
            minFactor = Mathf.Min(minFactor, ArcFactor(k));
            maxFactor = Mathf.Max(maxFactor, ArcFactor(k));
        }

        Vector3 point;
        float horizontalSpeed, time;
        if (!predictor.BuscarCuelloDeBotella(origin, shellHorizontalSpeed * minFactor, shellHorizontalSpeed * maxFactor,
            minFlightTime, maxFlightTime, out point, out horizontalSpeed, out time)) return false;

        Vector3 target = SnapToGround(point);
        float g = CurrentGravity();
        BallisticSolver.Solution s = BallisticSolver.SolveLeadShot(origin, target, Vector3.zero, horizontalSpeed, g,
            windAcceleration, minFlightTime, maxFlightTime, maxLaunchSpeed);
        if (!s.IsValid) return false;

        Vector3 block = Vector3.zero;
        if (comprobarTrayectoria && !BallisticSolver.IsTrajectoryClear(origin, s.launchVelocity, g, windAcceleration,
            s.flightTime, s.aimPoint, groundMask, 0.25f, 16, BlastRadiusEff, out block)) return false;

        lastArcFactor = horizontalSpeed / Mathf.Max(0.01f, shellHorizontalSpeed);
        solution = s;
        return true;
    }

    // Lead targeting lineal contra la PROYECCIÓN DEL JUGADOR EN EL SUELO (se usa en la previsualización del editor).
    // La altura del suelo en el punto predicho se afina en pocas iteraciones (el terreno no es plano).
    private BallisticSolver.Solution SolveShot(Vector3 origin, Vector3 targetPosition, Vector3 targetVelocity)
    {
        float g = CurrentGravity();
        // leadAccuracy < 1 reduce cuánto se adelanta al jugador (planetas iniciales más permisivos).
        Vector3 groundVelocity = new Vector3(targetVelocity.x, 0f, targetVelocity.z) * leadAccuracy;

        // scatterOffset desplaza el punto de impacto: fallos "por poco" que dan tensión sin ser injustos.
        Vector3 groundTarget = targetPosition + scatterOffset;
        float groundY;
        Vector3 unusedNormal;
        if (TryGetGround(groundTarget, out groundY, out unusedNormal)) groundTarget.y = groundY;
        else WarnNoGroundOnce();

        BallisticSolver.Solution solution = default(BallisticSolver.Solution);
        int iterations = Mathf.Max(1, leadRefineIterations);

        for (int i = 0; i < iterations; i++)
        {
            solution = BallisticSolver.SolveLeadShot(
                origin, groundTarget, groundVelocity,
                shellHorizontalSpeed, g, windAcceleration,
                minFlightTime, maxFlightTime, maxLaunchSpeed);

            if (!solution.IsValid || i == iterations - 1) break;

            float predictedGroundY;
            if (TryGetGround(solution.aimPoint, out predictedGroundY, out unusedNormal)) groundTarget.y = predictedGroundY;
            else break;
        }

        return solution;
    }

    // Rotación suave del cañón: LookRotation hacia la dirección deseada + Slerp (independiente del framerate).
    private void AimCannonAt(Vector3 worldDirection, float dt)
    {
        if (cannonPivot == null || worldDirection.sqrMagnitude < 0.000001f) return;

        Quaternion target = Quaternion.LookRotation(worldDirection.normalized, Vector3.up);
        float t = 1f - Mathf.Exp(-cannonTurnSpeed * dt);
        cannonPivot.rotation = Quaternion.Slerp(cannonPivot.rotation, target, t);
    }

    private bool IsCannonAligned(Vector3 worldDirection)
    {
        if (cannonPivot == null || worldDirection.sqrMagnitude < 0.000001f) return false;
        Quaternion target = Quaternion.LookRotation(worldDirection.normalized, Vector3.up);
        return Quaternion.Angle(cannonPivot.rotation, target) <= aimToleranceDegrees;
    }

    private void ReturnCannonToIdle(float dt)
    {
        if (cannonPivot == null) return;
        float t = 1f - Mathf.Exp(-cannonTurnSpeed * dt);
        cannonPivot.localRotation = Quaternion.Slerp(cannonPivot.localRotation, cannonIdleLocalRotation, t);
    }

    // El chasis solo gira hacia donde camina; en ALERT/ATTACK queda quieto y gira únicamente el cañón.
    private void RotateMortarBody(Vector3 horizontalVelocity, float dt)
    {
        if (currentState != State.PATROL) return;
        if (horizontalVelocity.sqrMagnitude <= 0.1f) return;

        Quaternion target = Quaternion.LookRotation(horizontalVelocity) * Quaternion.Euler(0f, bodyYawOffset, 0f);
        float t = 1f - Mathf.Exp(-bodyTurnSpeed * dt);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, t);
    }

    // Instancia proyectil + indicador. ti y pi salen de la cuadrática de impacto (BallisticSolver.PredictImpact).
    private void FireShell(BallisticSolver.Solution solution)
    {
        float g = CurrentGravity();
        // Mismo origen con el que se resolvió el tiro: la trayectoria real coincide exactamente con la calculada
        // (la diferencia con la punta del cañón es de milímetros).
        Vector3 origin = lastSolveOrigin;

        MortarProjectile projectile = Instantiate(projectilePrefab, origin, Quaternion.LookRotation(solution.launchVelocity));

        float ti;
        Vector3 pi;
        bool analytic = BallisticSolver.PredictImpact(origin, solution.launchVelocity, g, windAcceleration, solution.aimPoint.y, out ti, out pi);
        if (!analytic || Mathf.Abs(ti - solution.flightTime) > 0.25f)
        {
            // Caso raro (objetivo en una cuesta mucho más alta que el cañón): el proyectil cruza esa altura subiendo.
            ti = solution.flightTime;
            pi = solution.aimPoint;
        }

        ImpactIndicator indicator = null;
        if (indicatorPrefab != null)
        {
            Vector3 normal = Vector3.up;
            float groundY;
            Vector3 groundNormal;
            if (TryGetGround(pi, out groundY, out groundNormal))
            {
                pi.y = groundY;
                normal = groundNormal;
            }

            indicator = Instantiate(indicatorPrefab);
            indicator.Show(pi, normal, projectile.BlastRadius * blastRadiusMultiplier, ti);
        }

        MortarLaunchData data = new MortarLaunchData();
        data.velocity = solution.launchVelocity;
        data.gravity = g;
        data.windAcceleration = windAcceleration;
        data.expectedFlightTime = ti;
        data.maxLifetime = ti + fuseMargin;
        data.blastRadiusMultiplier = blastRadiusMultiplier;
        data.damageMultiplier = damageMultiplier;
        data.indicator = indicator;
        projectile.Launch(data);

        // Game Feel: fogonazo, sonido de disparo y evento para quien quiera reaccionar (HUD, audio global...)
        if (firePoint != null)
        {
            if (muzzleFlashPrefab != null)
            {
                GameObject flash = Instantiate(muzzleFlashPrefab, firePoint.position, firePoint.rotation);
                Destroy(flash, muzzleFlashLifetime);
            }
            // Disparo: el sonido propio de este mortero manda; si no, el de Solar Adventure > Sonido.
            AudioCue disparo = fireCue;
            if (disparo == null || !disparo.HasClip)
            {
                SonidosJuego sonidos = SonidosJuego.Actual;
                disparo = sonidos != null ? sonidos.disparoMortero : null;
            }
            if (disparo != null) disparo.PlayAt(firePoint.position);
        }
        if (ShellFired != null) ShellFired(projectile);
    }

    #endregion
}
