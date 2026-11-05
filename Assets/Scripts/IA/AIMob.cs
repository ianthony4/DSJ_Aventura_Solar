using UnityEngine;

/// <summary>
/// Enemigo con IA. Un solo componente con varios modos (para no romper lo ya configurado en escenas y prefabs):
///   * SEEK_ONLY / WANDER_ONLY / COMBINED : duende clásico con steering (Seek, Arrive, Wander).
///   * MORTAR                              : artillería que patrulla nidos de tiro y bombardea con física predictiva.
///   * TACTICO                             : duende con percepción (cono de visión + raycast), pathfinding (NavMesh + A*),
///                                           flanqueo en escuadra y búsqueda de la última posición conocida.
///
/// El código está repartido en archivos parciales de la MISMA clase:
///   AIMob.cs          núcleo: movimiento con CharacterController, steering, salud
///   AIMob.Mortero.cs  máquina de estados y balística del mortero
///   AIMob.Tactico.cs  máquina de estados del duende táctico
///   AIMob.Gizmos.cs   dibujos de depuración en la vista Scene
/// </summary>
[RequireComponent(typeof(CharacterController))]
public partial class AIMob : MonoBehaviour, IDamageable
{
    // NOTA: los valores nuevos se añaden AL FINAL de cada enum para no alterar los índices ya serializados en escenas/prefabs.
    public enum BehaviorMode
    {
        SEEK_ONLY,
        WANDER_ONLY,
        COMBINED,
        MORTAR,     // Artillería: patrulla entre nidos de tiro y bombardea con puntería predictiva
        TACTICO     // Duende táctico: patrulla por waypoints, ve, persigue en escuadra y busca
    }

    public enum State
    {
        WANDER,
        SEEK,       // Persecución (si toca al jugador en este estado, Game Over)
        PATROL,     // Mortero: va entre nidos / Táctico: recorre el grafo de waypoints
        ALERT,      // Mortero: detectó al jugador, apunta y mide su velocidad (aviso visible)
        ATTACK,     // Mortero: dispara la andanada con puntería predictiva
        SEARCH      // Táctico: perdió al jugador y revisa su última posición y los waypoints cercanos
    }

    public enum NestPattern
    {
        PING_PONG,      // 0,1,2,3,2,1,0,...
        CIRCULAR,       // 0,1,2,3,0,...  (módulo n)
        CONDITIONAL,    // el siguiente nido depende de la posición del jugador (distancia de tiro válida)
        TACTICO         // cada nido recibe un puntaje (distancia, altura, visión, tiro posible, recorrido...) y gana el mejor
    }

    [Header("Behavior Settings")]
    public BehaviorMode mode = BehaviorMode.COMBINED;

    [Header("Mob Properties")]
    public float maxSpeed = 5f;
    public float maxForce = 15f;
    public float detectionRadius = 15f;
    public float slowRadius = 5f; // Radio para frenar (Arrive)

    [Header("Wander Properties")]
    public float wanderDistance = 4f;
    public float wanderRadius = 2f;
    public float wanderJitter = 360f; // Grados por segundo

    [Header("Physics")]
    public float gravity = 20f;

    [Header("Modelo 3D")]
    [Tooltip("Giro (grados) para que el modelo mire hacia donde camina. El duende antiguo mira hacia -Z (180). Un modelo que mira hacia +Z necesita 0. El menú Solar Adventure > Enemigos lo calcula solo.")]
    public float yawModelo = 180f;
    [Tooltip("Al morir, el cuerpo cae de lado. Desactívalo si el modelo trae su propia animación de muerte.")]
    public bool girarAlMorir = true;

    [Header("Sonido (opcional)")]
    [Tooltip("Sonido propio de movimiento (pasos o motor) de este enemigo. Si está vacío se usa el de Solar Adventure > Sonido > Abrir ajustes de sonido.")]
    public AudioCue sonidoMovimientoPropio;

    [Header("Salud (fuego amigo y granadas)")]
    [Tooltip("Vida del enemigo. Un proyectil de mortero hace 25 de daño (por el multiplicador del planeta).")]
    public float saludMaxima = 25f;

    [Header("Debug Info")]
    public State currentState = State.WANDER;

    private Vector3 velocity = Vector3.zero;
    private Vector3 acceleration = Vector3.zero;
    private float currentWanderAngle;

    // References
    private CharacterController controller;
    private Transform player;

    // Salud
    private float saludActual;
    private bool muerto;

    /// <summary>Se dispara cuando este enemigo muere (granada del jugador o fuego amigo del mortero).</summary>
    public event System.Action<AIMob> Murio;
    /// <summary>Se dispara cuando recibe daño y sigue vivo (para la animación de golpe).</summary>
    public event System.Action<AIMob> Danado;

    /// <summary>Rapidez horizontal actual (m/s). La usa AnimadorEnemigo para el ciclo de caminar.</summary>
    public float VelocidadHorizontal { get { return new Vector3(velocity.x, 0f, velocity.z).magnitude; } }

    public bool EstaMuerto { get { return muerto; } }

    void Start()
    {
        // Seguro contra un error del duende antiguo: tenía un segundo AIMob (con su CharacterController) en un hijo
        // (BAG.2) en modo COMBINED, que se movía y perseguía por su cuenta. Solo manda el AIMob del objeto raíz.
        AIMob mandante = transform.parent != null ? transform.parent.GetComponentInParent<AIMob>() : null;
        if (mandante != null)
        {
            Debug.LogWarning("AIMob duplicado en '" + name + "' (hijo de '" + mandante.name + "'): se desactiva; manda el del objeto raíz.", this);
            enabled = false;
            return;
        }

        controller = GetComponent<CharacterController>();
        currentWanderAngle = Random.Range(0f, 360f);
        saludActual = saludMaxima;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }

        if (mode == BehaviorMode.MORTAR)
        {
            InitMortar();
        }
        else if (mode == BehaviorMode.TACTICO)
        {
            InitTactico();
        }

        IniciarSonidoMovimiento();
    }

    // Pasos (enemigos) o motor (mortero) en bucle mientras se mueve. Sin sonido asignado no hace nada.
    private void IniciarSonidoMovimiento()
    {
        AudioCue cue = null;
        if (sonidoMovimientoPropio != null && sonidoMovimientoPropio.HasClip)
        {
            cue = sonidoMovimientoPropio;
        }
        else
        {
            SonidosJuego sonidos = SonidosJuego.Actual;
            if (sonidos != null) cue = (mode == BehaviorMode.MORTAR) ? sonidos.movimientoMortero : sonidos.pasosEnemigo;
        }
        if (cue == null || !cue.HasClip) return;

        // Referencia de tono: el mortero va lento (3 m/s); los enemigos, a ritmo de patrulla.
        float referencia = (mode == BehaviorMode.MORTAR) ? Mathf.Max(0.5f, maxSpeed) : Mathf.Max(1f, maxSpeed * 0.45f);
        gameObject.AddComponent<SonidoMovimiento>().Iniciar(this, cue, referencia);
    }

    void Update()
    {
        if (muerto) return;

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        float distanceToPlayer = float.MaxValue;
        if (player != null)
        {
            distanceToPlayer = Vector3.Distance(transform.position, player.position);
        }

        // Selección de comportamiento según el modo
        if (mode == BehaviorMode.MORTAR)
        {
            acceleration = UpdateMortar(Time.deltaTime);
        }
        else if (mode == BehaviorMode.TACTICO)
        {
            acceleration = UpdateTactico(Time.deltaTime);
        }
        else if (mode == BehaviorMode.SEEK_ONLY)
        {
            currentState = State.SEEK;
            acceleration = (player != null) ? SeekArrive(player.position) : Vector3.zero;
        }
        else if (mode == BehaviorMode.WANDER_ONLY)
        {
            currentState = State.WANDER;
            acceleration = Wander(Time.deltaTime);
        }
        else if (mode == BehaviorMode.COMBINED)
        {
            if (distanceToPlayer <= detectionRadius)
            {
                currentState = State.SEEK;
                acceleration = SeekArrive(player.position);
            }
            else
            {
                currentState = State.WANDER;
                acceleration = Wander(Time.deltaTime);
            }
        }

        // Integración de Euler
        acceleration.y = 0;
        velocity += acceleration * Time.deltaTime;

        // Limitar velocidad horizontal
        Vector3 horizontalVelocity = new Vector3(velocity.x, 0, velocity.z);
        if (horizontalVelocity.sqrMagnitude > maxSpeed * maxSpeed)
        {
            horizontalVelocity = horizontalVelocity.normalized * maxSpeed;
            velocity.x = horizontalVelocity.x;
            velocity.z = horizontalVelocity.z;
        }

        // Terreno / Gravedad
        if (controller.isGrounded)
        {
            if (velocity.y < 0)
            {
                velocity.y = -2f;
            }
        }
        else
        {
            velocity.y -= gravity * Time.deltaTime;
        }

        // Rotación
        if (mode == BehaviorMode.MORTAR)
        {
            RotateMortarBody(horizontalVelocity, Time.deltaTime);
        }
        else if (mode == BehaviorMode.TACTICO)
        {
            RotateTacticoBody(horizontalVelocity, Time.deltaTime);
        }
        else if (currentState == State.SEEK && player != null)
        {
            // Usar LookAt directo hacia el jugador cuando lo persigue
            Vector3 lookPos = player.position;
            lookPos.y = transform.position.y;
            transform.LookAt(lookPos);
            // Corrección del modelo 3D (el duende antiguo está de espaldas: 180 grados)
            transform.Rotate(0, yawModelo, 0);
        }
        else if (horizontalVelocity.sqrMagnitude > 0.1f)
        {
            // Rotación suave estándar cuando está deambulando
            Quaternion baseRotation = Quaternion.LookRotation(horizontalVelocity);
            // Añadimos el giro del modelo al final (180 grados en el duende antiguo)
            Quaternion lookRotation = baseRotation * Quaternion.Euler(0, yawModelo, 0);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
        }

        controller.Move(velocity * Time.deltaTime);
    }

    #region Steering

    private Vector3 Seek(Vector3 target)
    {
        return Seek(target, maxSpeed);
    }

    private Vector3 Seek(Vector3 target, float speed)
    {
        Vector3 desired = (target - transform.position);
        desired.y = 0;

        if (desired.sqrMagnitude > 0)
        {
            desired = desired.normalized * speed;
        }

        Vector3 steer = desired - new Vector3(velocity.x, 0, velocity.z);
        steer = Vector3.ClampMagnitude(steer, maxForce);

        return steer;
    }

    private Vector3 SeekArrive(Vector3 target)
    {
        return SeekArrive(target, maxSpeed);
    }

    private Vector3 SeekArrive(Vector3 target, float maxArriveSpeed)
    {
        Vector3 desired = (target - transform.position);
        desired.y = 0;

        float distance = desired.magnitude;

        if (distance < 0.1f)
        {
            Vector3 stopSteer = -new Vector3(velocity.x, 0, velocity.z);
            return Vector3.ClampMagnitude(stopSteer, maxForce);
        }

        float speed = maxArriveSpeed;
        if (distance < slowRadius)
        {
            speed = maxArriveSpeed * (distance / slowRadius);
        }

        desired = desired.normalized * speed;

        Vector3 steer = desired - new Vector3(velocity.x, 0, velocity.z);
        steer = Vector3.ClampMagnitude(steer, maxForce);

        return steer;
    }

    private Vector3 Wander(float dt)
    {
        currentWanderAngle += Random.Range(-wanderJitter, wanderJitter) * dt;

        Vector3 forward = new Vector3(velocity.x, 0, velocity.z).normalized;
        if (forward.sqrMagnitude < 0.1f) forward = transform.forward;

        Vector3 circleCenter = transform.position + forward * wanderDistance;
        circleCenter.y = transform.position.y;

        float angleRad = currentWanderAngle * Mathf.Deg2Rad;
        Vector3 displacement = new Vector3(Mathf.Cos(angleRad), 0, Mathf.Sin(angleRad)) * wanderRadius;

        Vector3 target = circleCenter + displacement;

        return Seek(target);
    }

    // Frena hasta detenerse (el mortero dispara siempre desde parado).
    private Vector3 Brake()
    {
        Vector3 stopSteer = -new Vector3(velocity.x, 0, velocity.z);
        return Vector3.ClampMagnitude(stopSteer, maxForce);
    }

    /// <summary>
    /// Pathfinding + steering: el NavMesh calcula la ruta (A* interno de Unity) y el steering la recorre esquina por esquina.
    /// Sin NavMesh horneado se comporta como un Seek en línea recta (el juego sigue funcionando).
    /// </summary>
    private Vector3 SteerAlongPath(NavPathFollower follower, Vector3 destination, float speed, bool arriveAtEnd, float dt)
    {
        bool isLast;
        Vector3 next = follower.NextCorner(transform.position, destination, dt, out isLast);
        if (isLast && arriveAtEnd) return SeekArrive(next, speed);
        return Seek(next, speed);
    }

    #endregion

    #region Contacto con el jugador

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        // Si tocamos al jugador mientras lo estamos buscando/persiguiendo
        if (hit.gameObject.CompareTag("Player") && currentState == State.SEEK && !muerto)
        {
            if (GameManager.instancia != null)
            {
                GameManager.instancia.GameOver("un duende te atrapó (" + name + ")");
            }
            else
            {
                Debug.Log("GOBLIN atrapó al jugador. Faltó el GameManager para lanzar GameOver.");
            }
        }
    }

    // Por si en el futuro se añade un trigger  para un hitbox más grande
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && currentState == State.SEEK && !muerto)
        {
            if (GameManager.instancia != null)
            {
                GameManager.instancia.GameOver("un duende te atrapó (" + name + ")");
            }
        }
    }

    #endregion

    #region Salud

    public void TakeDamage(float amount)
    {
        if (muerto || amount <= 0f) return;

        saludActual -= amount;
        if (saludActual <= 0f)
        {
            Morir();
        }
        else
        {
            if (Danado != null) Danado(this);
            if (mode == BehaviorMode.TACTICO) AlRecibirDanioTactico();
        }
    }

    // Sin animaciones nuevas: el enemigo cae de lado, se hunde y desaparece.
    private void Morir()
    {
        muerto = true;
        RedTactica.RetirarPerseguidor(this);

        bool porJugador = MortarProjectile.ExplosionActualEsDelJugador;
        string quien = mode == BehaviorMode.MORTAR ? "Mortero destruido" : "Duende eliminado";
        if (!porJugador && mode != BehaviorMode.MORTAR) quien = "Fuego amigo: el mortero eliminó a un duende";
        HUDTactico.Mensaje(quien, new Color(0.4f, 1f, 0.5f));
        if (Murio != null) Murio(this);

        if (controller != null) controller.enabled = false;
        StartCoroutine(AnimacionMuerte());
    }

    private System.Collections.IEnumerator AnimacionMuerte()
    {
        float t = 0f;
        if (girarAlMorir)
        {
            Quaternion inicio = transform.rotation;
            Quaternion caido = inicio * Quaternion.Euler(0f, 0f, 90f);
            while (t < 1f)
            {
                t += Time.deltaTime / 0.35f;
                transform.rotation = Quaternion.Slerp(inicio, caido, t);
                yield return null;
            }
        }

        // Sin giro propio, el modelo usa su animación de muerte: se le da más tiempo antes de hundirse.
        yield return new WaitForSeconds(girarAlMorir ? 1.2f : 2f);

        Vector3 desde = transform.position;
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 1f;
            transform.position = desde + Vector3.down * (1.5f * t);
            yield return null;
        }
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        RedTactica.RetirarPerseguidor(this);
    }

    #endregion

    #region Utilidades comunes

    private static float PlanarDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    #endregion
}
