using UnityEngine;

/// <summary>
/// Sacudida de cámara basada en "trauma" (0..1): la intensidad crece con el cuadrado del trauma, y el trauma decae solo.
/// Se suscribe a MortarProjectile.AnyExploded, así que reacciona a cualquier explosión sin referencias directas:
/// cuanto más cerca de la nave explota, más fuerte sacude.
///
/// Se pone en el MISMO GameObject que CamaraSeguimiento (la cámara). Para no pelearse con ese script:
///   * en Update() retira la sacudida aplicada el frame anterior (antes de que la cámara siga a la nave),
///   * en LateUpdate() la vuelve a aplicar (ejecución tardía, después de CamaraSeguimiento).
/// Con el juego en pausa (Time.timeScale = 0) no se aplica sacudida.
/// </summary>
[DefaultExecutionOrder(1000)]
public class CameraShake : MonoBehaviour
{
    [Header("Intensidad máxima")]
    [SerializeField] private float maxPositionOffset = 0.6f;
    [SerializeField] private float maxRotationDegrees = 2.5f;
    [SerializeField] private float frequency = 22f;
    [Tooltip("Cuánto trauma se pierde por segundo (1.6 = una sacudida fuerte dura ~0.6 s).")]
    [SerializeField] private float traumaDecayPerSecond = 1.6f;

    [Header("Explosiones de mortero")]
    [Tooltip("Trauma de una explosión pegada a la nave (0..1).")]
    [SerializeField] private float explosionTrauma = 0.9f;
    [Tooltip("Distancia mínima (m) a la que aún se nota una explosión. Se amplía con el radio de la explosión.")]
    [SerializeField] private float minShakeRadius = 35f;

    [Header("Referencia (opcional)")]
    [Tooltip("Nave a la que se mide la distancia. Si está vacío se busca el objeto con tag Player.")]
    [SerializeField] private Transform ship;

    private float trauma;
    private float noiseTime;
    private float seed;
    private Vector3 appliedOffset;
    private Quaternion appliedRotation = Quaternion.identity;

    private void Awake()
    {
        seed = Random.Range(0f, 1000f);
    }

    private void OnEnable()
    {
        MortarProjectile.AnyExploded += OnMortarExploded;
    }

    private void OnDisable()
    {
        MortarProjectile.AnyExploded -= OnMortarExploded;
        RevertShake();
    }

    /// <summary>Añade trauma manualmente (0..1). Útil para disparos, choques, etc.</summary>
    public void AddTrauma(float amount)
    {
        trauma = Mathf.Clamp01(trauma + Mathf.Max(0f, amount));
    }

    private void OnMortarExploded(MortarImpactInfo info)
    {
        Transform reference = GetShip();
        float distance = Vector3.Distance(info.position, reference.position);
        float radius = Mathf.Max(minShakeRadius, info.blastRadius * 4f);

        float proximity = 1f - Mathf.Clamp01(distance / radius);
        if (proximity <= 0f) return;

        float hitFactor = info.hitPlayer ? 1f : 0.85f;
        AddTrauma(explosionTrauma * proximity * hitFactor);
    }

    private Transform GetShip()
    {
        if (ship == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            ship = (player != null) ? player.transform : transform;
        }
        return ship;
    }

    // Antes de cualquier LateUpdate: deja la cámara tal como la dejó CamaraSeguimiento.
    private void Update()
    {
        RevertShake();
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return; // pausa: la cámara queda en reposo y el trauma se conserva

        trauma = Mathf.Max(0f, trauma - traumaDecayPerSecond * dt);
        if (trauma <= 0f) return;

        noiseTime += dt * frequency;
        float intensity = trauma * trauma;

        float nx = Noise(0f);
        float ny = Noise(10f);
        float roll = Noise(20f);
        float pitch = Noise(30f);
        float yaw = Noise(40f);

        Vector3 offset = (transform.right * nx + transform.up * ny) * (maxPositionOffset * intensity);
        Quaternion rotation = Quaternion.Euler(
            pitch * maxRotationDegrees * intensity,
            yaw * maxRotationDegrees * intensity,
            roll * maxRotationDegrees * intensity);

        transform.position += offset;
        transform.rotation = transform.rotation * rotation;

        appliedOffset = offset;
        appliedRotation = rotation;
    }

    private void RevertShake()
    {
        if (appliedOffset == Vector3.zero && appliedRotation == Quaternion.identity) return;

        transform.position -= appliedOffset;
        transform.rotation = transform.rotation * Quaternion.Inverse(appliedRotation);
        appliedOffset = Vector3.zero;
        appliedRotation = Quaternion.identity;
    }

    // Ruido Perlin en [-1, 1].
    private float Noise(float channel)
    {
        return Mathf.PerlinNoise(seed + channel, noiseTime) * 2f - 1f;
    }
}
