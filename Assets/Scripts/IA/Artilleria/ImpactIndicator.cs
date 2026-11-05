using UnityEngine;

/// <summary>
/// Aviso en el suelo del punto de impacto (pi). Muestra dos anillos dibujados con LineRenderer, sin arte extra:
///   - Anillo interior FIJO: huella de la explosión (blastRadius). Es la zona a abandonar.
///   - Anillo exterior que SE ENCOGE durante el tiempo de vuelo (ti) hasta coincidir con el interior en el impacto.
/// El color pasa de ámbar a rojo y se vuelve más opaco conforme se acerca el impacto.
/// Los anillos se crean por código en Awake, así que el prefab solo necesita este script y un material.
/// </summary>
public class ImpactIndicator : MonoBehaviour
{
    [Header("Visual")]
    [Tooltip("Material transparente sin iluminación (URP Particles/Unlit o Sprites/Default). Ver guía.")]
    [SerializeField] private Material ringMaterial;
    [SerializeField] private Color farColor = new Color(1f, 0.75f, 0.1f, 0.35f);
    [SerializeField] private Color nearColor = new Color(1f, 0.1f, 0.05f, 0.95f);
    [SerializeField] private Color footprintColor = new Color(1f, 0.15f, 0.1f, 0.7f);
    [SerializeField] private float startRadiusMultiplier = 3f;
    [SerializeField] private float lineWidth = 0.18f;
    [SerializeField] private int segments = 48;
    [Tooltip("Elevación sobre la superficie para evitar parpadeo con el terreno.")]
    [SerializeField] private float surfaceOffset = 0.1f;
    [Tooltip("Segundos extra que sobrevive si el proyectil nunca avisa (seguridad).")]
    [SerializeField] private float safetyLifetime = 1.5f;

    private LineRenderer outerRing;
    private LineRenderer innerRing;
    private Vector3[] outerPoints;
    private Vector3[] innerPoints;
    private float[] cosTable;
    private float[] sinTable;

    private float blastRadius;
    private float duration;
    private float elapsed;
    private bool shown;

    private void Awake()
    {
        segments = Mathf.Max(8, segments);
        cosTable = new float[segments];
        sinTable = new float[segments];
        for (int i = 0; i < segments; i++)
        {
            float angle = (Mathf.PI * 2f) * i / segments;
            cosTable[i] = Mathf.Cos(angle);
            sinTable[i] = Mathf.Sin(angle);
        }
        outerPoints = new Vector3[segments];
        innerPoints = new Vector3[segments];

        Material material = ringMaterial;
        if (material == null)
        {
            Shader fallback = Shader.Find("Sprites/Default");
            if (fallback != null) material = new Material(fallback);
            Debug.LogWarning("ImpactIndicator: asigna un Ring Material en el prefab (el material de respaldo puede no incluirse en una build).", this);
        }

        outerRing = CreateRing("AnilloExterior", material);
        innerRing = CreateRing("AnilloHuella", material);
        SetVisible(false);
    }

    /// <summary>Coloca el indicador sobre el suelo y arranca la cuenta atrás.</summary>
    /// <param name="groundPoint">Punto de impacto pi sobre la superficie.</param>
    /// <param name="surfaceNormal">Normal de la superficie (para que el anillo se apoye en pendientes).</param>
    /// <param name="radius">Radio de la explosión.</param>
    /// <param name="timeToImpact">Tiempo de vuelo ti en segundos.</param>
    public void Show(Vector3 groundPoint, Vector3 surfaceNormal, float radius, float timeToImpact)
    {
        blastRadius = Mathf.Max(0.1f, radius);
        duration = Mathf.Max(0.05f, timeToImpact);
        elapsed = 0f;

        transform.position = groundPoint + surfaceNormal * surfaceOffset;
        transform.rotation = Quaternion.FromToRotation(Vector3.up, surfaceNormal);

        BuildCircle(innerPoints, blastRadius);
        innerRing.SetPositions(innerPoints);
        innerRing.startColor = footprintColor;
        innerRing.endColor = footprintColor;

        shown = true;
        SetVisible(true);
        Refresh(0f);
    }

    /// <summary>Retira el indicador (el proyectil lo llama al explotar).</summary>
    public void Dismiss()
    {
        Destroy(gameObject);
    }

    private void Update()
    {
        if (!shown) return;

        elapsed += Time.deltaTime;
        Refresh(Mathf.Clamp01(elapsed / duration));

        if (elapsed >= duration + safetyLifetime)
        {
            Dismiss();
        }
    }

    private void Refresh(float progress)
    {
        // Aceleración suave al final para que el último instante se lea como "inminente".
        float eased = progress * progress;
        float radius = Mathf.Lerp(blastRadius * startRadiusMultiplier, blastRadius, progress);
        BuildCircle(outerPoints, radius);
        outerRing.SetPositions(outerPoints);

        Color c = Color.Lerp(farColor, nearColor, eased);
        outerRing.startColor = c;
        outerRing.endColor = c;
    }

    // Círculo en el plano XY local del anillo (el anillo se gira 90° para quedar horizontal).
    private void BuildCircle(Vector3[] buffer, float radius)
    {
        for (int i = 0; i < segments; i++)
        {
            buffer[i] = new Vector3(cosTable[i] * radius, sinTable[i] * radius, 0f);
        }
    }

    private LineRenderer CreateRing(string ringName, Material material)
    {
        GameObject go = new GameObject(ringName);
        go.transform.SetParent(transform, false);
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.alignment = LineAlignment.TransformZ;
        lr.positionCount = segments;
        lr.widthMultiplier = lineWidth;
        lr.numCapVertices = 0;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.sharedMaterial = material;
        return lr;
    }

    private void SetVisible(bool visible)
    {
        if (outerRing != null) outerRing.enabled = visible;
        if (innerRing != null) innerRing.enabled = visible;
    }
}
