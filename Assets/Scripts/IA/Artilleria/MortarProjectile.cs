using System.Collections.Generic;
using UnityEngine;

/// <summary>Datos con los que el mortero lanza un proyectil.</summary>
public struct MortarLaunchData
{
    public Vector3 velocity;                 // velocidad inicial v0
    public float gravity;                    // módulo de la gravedad de artillería
    public Vector3 windAcceleration;         // viento (aceleración horizontal constante)
    public float expectedFlightTime;         // ti previsto (para el silbido de caída)
    public float maxLifetime;                // vida máxima de seguridad
    public float blastRadiusMultiplier;      // del perfil del planeta
    public float damageMultiplier;           // del perfil del planeta
    public ImpactIndicator indicator;        // aviso en el suelo que se retira al explotar (puede ser null)

    // Opcional: reemplaza las capas del prefab (las granadas del jugador chocan con enemigos y solo dañan enemigos)
    public bool fromPlayer;                  // lanzado por el jugador (granada): para distinguir el fuego amigo
    public bool overrideMasks;
    public LayerMask collisionMask;
    public LayerMask damageMask;
}

/// <summary>Información de una explosión, para sistemas externos (cámara, audio, HUD...).</summary>
public struct MortarImpactInfo
{
    public Vector3 position;
    public float blastRadius;
    public float damage;
    public bool hitPlayer;
    public float distanceToPlayer;           // -1 si no hay jugador en escena
    public float flightTime;
}

/// <summary>
/// Proyectil de mortero. Se mueve con la misma fórmula cerrada que usa BallisticSolver
/// (p(t) = p0 + v0·t + ½·a·t², con gravedad y viento), así que llega exactamente donde el cañón calculó.
/// No necesita Rigidbody ni Collider: detecta el suelo y al jugador barriendo con un SphereCast
/// el tramo recorrido en cada frame (no atraviesa colisionadores aunque vaya rápido).
/// Al explotar aplica daño en un radio (blastRadius).
///
/// GAME FEEL - puntos de enganche:
///   * Eventos estáticos AnyLaunched / AnyExploded (la cámara, el HUD o el audio se suscriben sin referencias).
///   * AudioSource de silbido (whistleSource) que arranca poco antes del impacto y sube de tono al caer.
///   * AudioCue de explosión y prefabs de explosión / marca de quemado en el suelo.
/// </summary>
public class MortarProjectile : MonoBehaviour
{
    /// <summary>Se dispara al lanzar cualquier proyectil.</summary>
    public static event System.Action<MortarProjectile> AnyLaunched;
    /// <summary>Se dispara al explotar cualquier proyectil (no al desvanecerse sin explosión).</summary>
    public static event System.Action<MortarImpactInfo> AnyExploded;

    [Header("Daño en área")]
    [SerializeField] private float blastRadius = 4f;
    [SerializeField] private float damage = 25f;

    [Header("Colisiones (usar Layers, ver guía)")]
    [Tooltip("Capas que detienen el proyectil: Suelo + Jugador. NO incluir la capa del propio mortero.")]
    [SerializeField] private LayerMask collisionMask;
    [Tooltip("Capas que reciben daño al explotar: normalmente solo Jugador.")]
    [SerializeField] private LayerMask damageMask;
    [SerializeField] private float collisionRadius = 0.2f;

    [Header("Game Feel - Audio")]
    [Tooltip("AudioSource hijo del proyectil con el clip de silbido (Loop desactivado, Play On Awake desactivado).")]
    [SerializeField] private AudioSource whistleSource;
    [Tooltip("Segundos antes del impacto en que empieza a sonar el silbido.")]
    [SerializeField] private float whistleLeadTime = 1.2f;
    [Tooltip("Tono al empezar y al llegar al suelo (el silbido 'cae').")]
    [SerializeField] private Vector2 whistlePitch = new Vector2(0.9f, 1.4f);
    [SerializeField] private AudioCue explosionCue;

    [Header("Game Feel - Efectos visuales")]
    [Tooltip("Partículas de la explosión (se destruyen solas tras Explosion Lifetime).")]
    [SerializeField] private GameObject explosionPrefab;
    [Tooltip("Marca de quemado / cráter pequeño que queda en el suelo (opcional).")]
    [SerializeField] private GameObject scorchPrefab;
    [SerializeField] private float explosionLifetime = 4f;
    [SerializeField] private float scorchLifetime = 20f;

    /// <summary>True mientras se aplica el daño de una explosión lanzada por el jugador (lo leen los que mueren).</summary>
    public static bool ExplosionActualEsDelJugador { get; private set; }

    public float BlastRadius { get { return blastRadius; } }
    public float Damage { get { return damage; } }

    private Vector3 origin;
    private Vector3 launchVelocity;
    private Vector3 windAcceleration;
    private float gravity;
    private float lifetime;
    private float expectedFlightTime;
    private float radiusMultiplier = 1f;
    private float damageMultiplier = 1f;
    private float elapsed;
    private bool launched;
    private bool exploded;
    private bool whistleStarted;
    private ImpactIndicator indicator;
    private bool fromPlayer;

    /// <summary>Lanza el proyectil desde su posición actual.</summary>
    public void Launch(MortarLaunchData data)
    {
        origin = transform.position;
        launchVelocity = data.velocity;
        gravity = data.gravity;
        windAcceleration = data.windAcceleration;
        expectedFlightTime = data.expectedFlightTime;
        lifetime = data.maxLifetime;
        radiusMultiplier = data.blastRadiusMultiplier > 0f ? data.blastRadiusMultiplier : 1f;
        damageMultiplier = data.damageMultiplier > 0f ? data.damageMultiplier : 1f;
        indicator = data.indicator;
        fromPlayer = data.fromPlayer;
        if (data.overrideMasks)
        {
            collisionMask = data.collisionMask;
            damageMask = data.damageMask;
        }
        elapsed = 0f;
        launched = true;
        exploded = false;
        whistleStarted = false;

        if (launchVelocity.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(launchVelocity);
        }

        if (AnyLaunched != null) AnyLaunched(this);
    }

    private void Update()
    {
        if (!launched || exploded) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return; // juego en pausa

        elapsed += dt;
        UpdateWhistle();

        Vector3 previous = transform.position;
        Vector3 next = BallisticSolver.PositionAt(origin, launchVelocity, gravity, windAcceleration, elapsed);
        Vector3 delta = next - previous;
        float distance = delta.magnitude;

        if (distance > 0.0001f)
        {
            Vector3 direction = delta / distance;
            RaycastHit hit;
            if (Physics.SphereCast(previous, collisionRadius, direction, out hit, distance, collisionMask, QueryTriggerInteraction.Ignore))
            {
                transform.position = hit.point;
                Explode(hit.point, hit.normal);
                return;
            }

            transform.position = next;
            transform.rotation = Quaternion.LookRotation(direction);
        }

        if (elapsed >= lifetime)
        {
            Fizzle();
        }
    }

    // Silbido de caída: arranca whistleLeadTime antes del impacto previsto y sube de tono hasta el suelo.
    private void UpdateWhistle()
    {
        if (whistleSource == null || whistleSource.clip == null) return;

        float startAt = Mathf.Max(0f, expectedFlightTime - whistleLeadTime);
        if (!whistleStarted && elapsed >= startAt)
        {
            whistleStarted = true;
            whistleSource.pitch = whistlePitch.x;
            whistleSource.Play();
        }

        if (whistleStarted && whistleSource.isPlaying)
        {
            float span = Mathf.Max(0.05f, expectedFlightTime - startAt);
            float progress = Mathf.Clamp01((elapsed - startAt) / span);
            whistleSource.pitch = Mathf.Lerp(whistlePitch.x, whistlePitch.y, progress);
        }
    }

    private void Explode(Vector3 point, Vector3 surfaceNormal)
    {
        if (exploded) return;
        exploded = true;

        if (indicator != null) indicator.Dismiss();

        float radius = blastRadius * radiusMultiplier;
        float finalDamage = damage * damageMultiplier;
        ExplosionActualEsDelJugador = fromPlayer;
        bool hitPlayer = ApplyBlastDamage(point, radius, finalDamage);
        ExplosionActualEsDelJugador = false;

        // Efectos
        if (explosionPrefab != null)
        {
            GameObject fx = Instantiate(explosionPrefab, point, Quaternion.identity);
            Destroy(fx, explosionLifetime);
        }
        if (scorchPrefab != null)
        {
            Quaternion alignToSurface = Quaternion.FromToRotation(Vector3.up, surfaceNormal);
            GameObject scorch = Instantiate(scorchPrefab, point + surfaceNormal * 0.05f, alignToSurface);
            Destroy(scorch, scorchLifetime);
        }

        // Sonido del impacto: el propio del prefab manda; si no, el de Solar Adventure > Sonido (distinto si lo lanzó el jugador).
        AudioCue impacto = explosionCue;
        if (impacto == null || !impacto.HasClip)
        {
            SonidosJuego sonidos = SonidosJuego.Actual;
            if (sonidos != null) impacto = fromPlayer ? sonidos.impactoJugador : sonidos.impactoMortero;
        }
        if (impacto != null) impacto.PlayAt(point);

        // Aviso a sistemas externos (cámara, HUD...)
        if (AnyExploded != null)
        {
            MortarImpactInfo info = new MortarImpactInfo();
            info.position = point;
            info.blastRadius = radius;
            info.damage = finalDamage;
            info.hitPlayer = hitPlayer;
            info.distanceToPlayer = DistanceToPlayer(point);
            info.flightTime = elapsed;
            AnyExploded(info);
        }

        Destroy(gameObject);
    }

    // El proyectil se perdió (cayó al vacío, etc.): se elimina sin explosión.
    private void Fizzle()
    {
        if (exploded) return;
        exploded = true;
        if (indicator != null) indicator.Dismiss();
        Destroy(gameObject);
    }

    private bool ApplyBlastDamage(Vector3 point, float radius, float amount)
    {
        Collider[] colliders = Physics.OverlapSphere(point, radius, damageMask, QueryTriggerInteraction.Ignore);
        if (colliders.Length == 0) return false;

        HashSet<IDamageable> alreadyDamaged = new HashSet<IDamageable>();
        bool anyHit = false;
        bool playerWithoutHealthHit = false;

        for (int i = 0; i < colliders.Length; i++)
        {
            IDamageable target = colliders[i].GetComponentInParent<IDamageable>();
            if (target != null)
            {
                // Un objetivo con varios colliders solo recibe el daño una vez.
                if (alreadyDamaged.Add(target))
                {
                    target.TakeDamage(amount);
                    anyHit = true;
                }
            }
            else if (HasPlayerTag(colliders[i]))
            {
                playerWithoutHealthHit = true;
            }
        }

        // Compatibilidad con el juego actual: si el jugador no tiene SaludJugador, el impacto es letal.
        if (playerWithoutHealthHit)
        {
            anyHit = true;
            if (GameManager.instancia != null)
            {
                GameManager.instancia.GameOver("te alcanzó una explosión de mortero (el jugador no tiene SaludJugador)");
            }
            else
            {
                Debug.Log("Mortero alcanzó al jugador. Faltó el GameManager para lanzar GameOver.");
            }
        }

        return anyHit;
    }

    private static float DistanceToPlayer(Vector3 point)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? Vector3.Distance(point, player.transform.position) : -1f;
    }

    private static bool HasPlayerTag(Collider c)
    {
        if (c.CompareTag("Player")) return true;
        if (c.attachedRigidbody != null && c.attachedRigidbody.CompareTag("Player")) return true;
        return c.transform.root.CompareTag("Player");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, blastRadius);
    }
}
