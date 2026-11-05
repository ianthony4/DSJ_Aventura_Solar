using UnityEngine;

/// <summary>
/// Maneja el Animator de un enemigo según lo que hace su IA (AIMob). No decide nada de la IA: solo la refleja.
///   * Caminar: el ciclo de caminar va más rápido cuanto más rápido se mueve; parado, la pose queda congelada.
///   * Atacar: al perseguir y estar cerca del jugador.
///   * Golpe: al recibir daño sin morir.
///   * Morir: al morir (el clip tiene bucle, así que se congela en su pose final).
/// Requiere un Animator Controller con los parámetros y estados de los nombres de abajo.
/// El menú Solar Adventure > Enemigos lo monta solo.
/// </summary>
[RequireComponent(typeof(AIMob))]
public class AnimadorEnemigo : MonoBehaviour
{
    [Header("Animator")]
    [Tooltip("Si está vacío se busca en los hijos.")]
    public Animator animator;
    [Tooltip("Rapidez (m/s) a la que el ciclo de caminar se reproduce a velocidad normal.")]
    public float velocidadReferencia = 3f;
    [Tooltip("Máxima velocidad de reproducción del ciclo de caminar.")]
    public float velocidadMaximaAnimacion = 2f;

    [Header("Ataque")]
    [Tooltip("Distancia al jugador (m) a la que, persiguiendo, lanza el zarpazo.")]
    public float distanciaAtaque = 2.5f;
    public float enfriamientoAtaque = 1.5f;

    [Header("Nombres en el Animator Controller")]
    public string parametroVelocidad = "VelocidadAnim";
    public string disparadorAtacar = "Atacar";
    public string disparadorGolpe = "Golpe";
    public string disparadorMorir = "Morir";
    public string estadoMorir = "Morir";

    private AIMob mob;
    private Transform jugador;
    private float proximoAtaque;
    private bool muerto;
    private int hashVelocidad, hashAtacar, hashGolpe, hashMorir;
    private bool tieneVelocidad, tieneAtacar, tieneGolpe, tieneMorir;

    private void Awake()
    {
        mob = GetComponent<AIMob>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator == null) return;

        animator.applyRootMotion = false; // el movimiento lo hace AIMob, no la animación

        hashVelocidad = Animator.StringToHash(parametroVelocidad);
        hashAtacar = Animator.StringToHash(disparadorAtacar);
        hashGolpe = Animator.StringToHash(disparadorGolpe);
        hashMorir = Animator.StringToHash(disparadorMorir);

        // Solo se usan los parámetros que existen (si no, Unity llena la Console de avisos).
        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.nameHash == hashVelocidad) tieneVelocidad = true;
            else if (p.nameHash == hashAtacar) tieneAtacar = true;
            else if (p.nameHash == hashGolpe) tieneGolpe = true;
            else if (p.nameHash == hashMorir) tieneMorir = true;
        }
    }

    private void OnEnable()
    {
        mob.Danado += AlRecibirDanio;
        mob.Murio += AlMorir;
    }

    private void OnDisable()
    {
        mob.Danado -= AlRecibirDanio;
        mob.Murio -= AlMorir;
    }

    private void Update()
    {
        if (animator == null || Time.timeScale <= 0f) return;

        if (muerto)
        {
            // El clip de morir tiene bucle: al llegar al final se congela en la pose caída.
            AnimatorStateInfo estado = animator.GetCurrentAnimatorStateInfo(0);
            if (estado.IsName(estadoMorir) && estado.normalizedTime >= 0.98f) animator.speed = 0f;
            return;
        }

        if (tieneVelocidad)
        {
            float ritmo = mob.VelocidadHorizontal / Mathf.Max(0.1f, velocidadReferencia);
            animator.SetFloat(hashVelocidad, Mathf.Clamp(ritmo, 0f, velocidadMaximaAnimacion));
        }

        if (tieneAtacar && mob.currentState == AIMob.State.SEEK && Time.time >= proximoAtaque)
        {
            if (jugador == null)
            {
                GameObject go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) jugador = go.transform;
            }
            if (jugador != null)
            {
                Vector3 d = jugador.position - transform.position;
                d.y = 0f;
                if (d.sqrMagnitude <= distanciaAtaque * distanciaAtaque)
                {
                    animator.SetTrigger(hashAtacar);
                    proximoAtaque = Time.time + enfriamientoAtaque;
                }
            }
        }
    }

    private void AlRecibirDanio(AIMob quien)
    {
        if (animator != null && tieneGolpe && !muerto) animator.SetTrigger(hashGolpe);
    }

    private void AlMorir(AIMob quien)
    {
        muerto = true;
        if (animator != null && tieneMorir) animator.SetTrigger(hashMorir);
    }
}
