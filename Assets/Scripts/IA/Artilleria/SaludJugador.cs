using UnityEngine;

/// <summary>
/// Salud del jugador (la nave). Es OPCIONAL: si el jugador no tiene este componente, una explosión de mortero
/// que lo alcance llama directamente a GameManager.GameOver(), igual que ya hace el duende al tocarlo.
///
/// Se añade al mismo GameObject que lleva el tag "Player". Tras recibir daño hay un breve periodo de
/// invulnerabilidad para que una andanada no te mate con dos impactos casi simultáneos.
/// </summary>
public class SaludJugador : MonoBehaviour, IDamageable
{
    [Header("Salud")]
    [SerializeField] private float saludMaxima = 100f;
    [SerializeField] private float segundosInvulnerable = 0.6f;

    [Header("Debug")]
    [SerializeField] private float saludActual;

    private float finInvulnerabilidad = -1f;
    private bool muerto;

    public float SaludActual { get { return saludActual; } }
    public float SaludMaxima { get { return saludMaxima; } }
    public float Porcentaje { get { return saludMaxima > 0f ? Mathf.Clamp01(saludActual / saludMaxima) : 0f; } }

    /// <summary>Se dispara al recibir daño (el HUD hace un destello rojo).</summary>
    public event System.Action<float> Danado;

    private void Awake()
    {
        saludActual = saludMaxima;
    }

    public void TakeDamage(float amount)
    {
        if (muerto || amount <= 0f) return;
        if (Time.time < finInvulnerabilidad) return;

        saludActual = Mathf.Max(0f, saludActual - amount);
        finInvulnerabilidad = Time.time + segundosInvulnerable;
        if (Danado != null) Danado(amount);
        Debug.Log("Nave alcanzada: -" + amount + " | salud " + saludActual + "/" + saludMaxima);

        if (saludActual <= 0f)
        {
            muerto = true;
            if (GameManager.instancia != null)
            {
                GameManager.instancia.GameOver("la salud de la nave llegó a 0");
            }
            else
            {
                Debug.Log("La nave fue destruida. Faltó el GameManager para lanzar GameOver.");
            }
        }
    }
}
