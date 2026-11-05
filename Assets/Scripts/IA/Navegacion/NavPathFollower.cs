using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Recorre rutas del NavMesh SIN NavMeshAgent: el NavMesh solo planifica (NavMesh.CalculatePath, que internamente
/// usa A* sobre los polígonos) y el personaje se sigue moviendo con su CharacterController y steering.
/// Así no hay dos sistemas peleando por mover al mismo objeto.
///
///   Planificar:  CalculatePath(origen, destino) -> lista de esquinas
///   Recorrer:    Seek hacia la esquina actual; al acercarse, pasa a la siguiente
///   Re-planificar cada <see cref="repathInterval"/> s o si el destino se movió (persecuciones, cráteres nuevos)
///
/// Si la escena no tiene NavMesh horneado, devuelve el destino directamente (Seek en línea recta).
/// </summary>
public class NavPathFollower
{
    public float repathInterval = 0.4f;
    public float cornerReachDistance = 1.2f;
    public float destinationTolerance = 1.0f;

    private const float SampleRadius = 4f;

    private NavMeshPath path; // se crea al primer uso (Unity no deja crearlo en el constructor de un componente)
    private readonly Vector3[] corners = new Vector3[64];
    private int cornerCount;
    private int cornerIndex;
    private Vector3 plannedDestination;
    private float timer;
    private bool hasPath;

    private static NavMeshPath sharedPath;

    public bool HasPath { get { return hasPath; } }
    public int CornerCount { get { return cornerCount; } }
    public int CurrentIndex { get { return cornerIndex; } }
    public Vector3 GetCorner(int i) { return corners[i]; }

    public void Reset()
    {
        hasPath = false;
        cornerCount = 0;
        cornerIndex = 0;
        timer = 0f;
    }

    /// <summary>Siguiente punto hacia el que hacer Seek. isLast = true cuando ya es el tramo final.</summary>
    public Vector3 NextCorner(Vector3 position, Vector3 destination, float dt, out bool isLast)
    {
        timer -= dt;
        float tol = destinationTolerance;
        if (!hasPath || timer <= 0f || PlanarSqr(destination - plannedDestination) > tol * tol)
        {
            Replan(position, destination);
        }

        if (!hasPath)
        {
            isLast = true;
            return destination;
        }

        while (cornerIndex < cornerCount - 1 && PlanarDistance(position, corners[cornerIndex]) <= cornerReachDistance)
        {
            cornerIndex++;
        }

        isLast = cornerIndex >= cornerCount - 1;
        return corners[cornerIndex];
    }

    private void Replan(Vector3 position, Vector3 destination)
    {
        // Intervalo con ±25 % de variación: los enemigos no recalculan todos en el mismo fotograma.
        timer = repathInterval * Random.Range(0.75f, 1.25f);
        plannedDestination = destination;
        hasPath = false;

        if (path == null) path = new NavMeshPath();
        if (!TryCalculatePath(position, destination, path)) return;

        cornerCount = path.GetCornersNonAlloc(corners);
        if (cornerCount == 0) return;

        // La esquina 0 es la posición actual: se empieza por la 1.
        cornerIndex = cornerCount > 1 ? 1 : 0;
        hasPath = true;
    }

    /// <summary>Calcula una ruta en el NavMesh proyectando antes los extremos sobre él. Acepta rutas parciales.</summary>
    public static bool TryCalculatePath(Vector3 from, Vector3 to, NavMeshPath result)
    {
        NavMeshHit a, b;
        if (!NavMesh.SamplePosition(from, out a, SampleRadius, NavMesh.AllAreas)) return false;
        if (!NavMesh.SamplePosition(to, out b, SampleRadius * 2f, NavMesh.AllAreas)) return false;
        if (!NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, result)) return false;
        return result.status != NavMeshPathStatus.PathInvalid;
    }

    /// <summary>
    /// Longitud de la ruta a pie entre dos puntos. Sin NavMesh devuelve la distancia en línea recta.
    /// Una ruta parcial (no llega) se penaliza con el triple del tramo que falta.
    /// </summary>
    public static float PathLength(Vector3 from, Vector3 to)
    {
        if (sharedPath == null) sharedPath = new NavMeshPath();
        if (!TryCalculatePath(from, to, sharedPath)) return Vector3.Distance(from, to);

        Vector3[] c = sharedPath.corners;
        float length = 0f;
        for (int i = 1; i < c.Length; i++) length += Vector3.Distance(c[i - 1], c[i]);
        if (sharedPath.status == NavMeshPathStatus.PathPartial && c.Length > 0)
        {
            length += 3f * Vector3.Distance(c[c.Length - 1], to);
        }
        return length;
    }

    /// <summary>¿Hay NavMesh horneado cerca de este punto?</summary>
    public static bool NavMeshNear(Vector3 p)
    {
        NavMeshHit hit;
        return NavMesh.SamplePosition(p, out hit, SampleRadius, NavMesh.AllAreas);
    }

    public void DrawGizmos(Color color)
    {
        if (!hasPath) return;
        Gizmos.color = color;
        for (int i = 1; i < cornerCount; i++)
        {
            Gizmos.DrawLine(corners[i - 1] + Vector3.up * 0.3f, corners[i] + Vector3.up * 0.3f);
        }
        if (cornerIndex < cornerCount) Gizmos.DrawWireSphere(corners[cornerIndex] + Vector3.up * 0.3f, 0.4f);
    }

    private static float PlanarSqr(Vector3 v) { return v.x * v.x + v.z * v.z; }

    private static float PlanarDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }
}
