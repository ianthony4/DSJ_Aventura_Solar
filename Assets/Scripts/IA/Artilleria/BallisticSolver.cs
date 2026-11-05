using UnityEngine;

/// <summary>
/// Utilidades de balística para la artillería (Morteros Nómadas).
///
/// Modelo: el proyectil sale con velocidad horizontal CASI constante (shellHorizontalSpeed) y sobre él actúan
/// la gravedad (eje Y) y, opcionalmente, un "viento": una aceleración horizontal constante. Todo en forma cerrada:
///
///   p(t) = p0 + v0·t + ½·a·t²,    a = (viento.x, -g, viento.z)
///
/// El problema se separa en dos partes:
///
///   1) Lead targeting en el plano horizontal  ->  ecuación CUADRÁTICA en el tiempo de vuelo t:
///        |Dh + Vh·t| = s·t   =>   (Vh·Vh - s²)·t² + 2(Dh·Vh)·t + Dh·Dh = 0
///      donde Dh = vector horizontal origen->objetivo, Vh = velocidad horizontal del objetivo, s = velocidad horizontal del proyectil.
///      Si el discriminante es negativo, el objetivo es más rápido que el proyectil y se aleja: no hay intercepción.
///      Con viento, Dh se corrige con la deriva esperada (½·w·t²) en unas pocas iteraciones, y la velocidad final se
///      calcula con la fórmula exacta para que el proyectil llegue al punto previsto en t.
///
///   2) Componente vertical en forma cerrada, conocido t:
///        vy = (Dy + Vy·t + ½·g·t²) / t
///
///   Con la velocidad de lanzamiento ya definida, PredictImpact resuelve otra cuadrática (altura = suelo)
///   para obtener el tiempo de caída ti y el punto de impacto pi.
///
/// Sin MonoBehaviour: es una clase estática pura, fácil de probar y de reutilizar (granadas del jugador, otros NPC).
/// </summary>
public static class BallisticSolver
{
    private const float Epsilon = 1e-5f;
    private const int WindIterations = 4;
    // Si el viento obliga a una velocidad horizontal muy distinta de la configurada, el tiro se descarta.
    private const float MinSpeedRatio = 0.65f;
    private const float MaxSpeedRatio = 1.35f;

    public enum SolveStatus
    {
        Ok,
        NoInterception, // discriminante < 0 o sin raíz positiva: el objetivo escapa
        TooClose,       // el tiempo de vuelo sería menor que el mínimo legible para el jugador
        OutOfReach      // supera el tiempo de vuelo máximo, la velocidad de lanzamiento máxima o el viento lo hace inviable
    }

    public struct Solution
    {
        public SolveStatus status;
        public Vector3 launchVelocity;  // v0 completa (horizontal + vertical)
        public float flightTime;        // ti: tiempo hasta llegar al punto apuntado
        public Vector3 aimPoint;        // posición predicha del objetivo en t = ti
        public float launchSpeed;       // |v0|
        public float apexHeight;        // altura máxima sobre el origen

        public bool IsValid { get { return status == SolveStatus.Ok; } }
    }

    /// <summary>Lead targeting sin viento.</summary>
    public static Solution SolveLeadShot(
        Vector3 origin, Vector3 targetPos, Vector3 targetVel,
        float horizontalSpeed, float gravity,
        float minFlightTime, float maxFlightTime, float maxLaunchSpeed)
    {
        return SolveLeadShot(origin, targetPos, targetVel, horizontalSpeed, gravity, Vector3.zero,
            minFlightTime, maxFlightTime, maxLaunchSpeed);
    }

    /// <summary>
    /// Calcula la velocidad de lanzamiento para interceptar un objetivo que se mueve a velocidad constante.
    /// </summary>
    /// <param name="origin">Punto de disparo (boca del cañón).</param>
    /// <param name="targetPos">Posición actual del objetivo.</param>
    /// <param name="targetVel">Velocidad actual del objetivo (se asume constante durante el vuelo).</param>
    /// <param name="horizontalSpeed">Velocidad horizontal nominal del proyectil (m/s).</param>
    /// <param name="gravity">Módulo de la gravedad (m/s², positivo; la gravedad actúa hacia -Y).</param>
    /// <param name="windAcceleration">Aceleración horizontal constante (viento) en m/s². Solo se usan X y Z.</param>
    /// <param name="minFlightTime">Tiempo de vuelo mínimo aceptable (garantiza un aviso legible).</param>
    /// <param name="maxFlightTime">Tiempo de vuelo máximo aceptable.</param>
    /// <param name="maxLaunchSpeed">Velocidad de lanzamiento máxima admitida (alcance físico del mortero).</param>
    public static Solution SolveLeadShot(
        Vector3 origin, Vector3 targetPos, Vector3 targetVel,
        float horizontalSpeed, float gravity, Vector3 windAcceleration,
        float minFlightTime, float maxFlightTime, float maxLaunchSpeed)
    {
        Solution s = default(Solution);
        s.status = SolveStatus.NoInterception;

        if (horizontalSpeed <= Epsilon) return s;

        Vector3 dhBase = targetPos - origin;
        float dy = dhBase.y;
        dhBase.y = 0f;

        Vector3 vh = targetVel;
        float vyTarget = vh.y;
        vh.y = 0f;

        Vector3 wind = new Vector3(windAcceleration.x, 0f, windAcceleration.z);
        bool hasWind = wind.sqrMagnitude > Epsilon;
        int iterations = hasWind ? WindIterations : 1;

        // Cuadrática del lead targeting horizontal: a·t² + b·t + c = 0   (a no depende del viento)
        float a = Vector3.Dot(vh, vh) - horizontalSpeed * horizontalSpeed;

        Vector3 dh = dhBase;
        float t = 0f;
        for (int k = 0; k < iterations; k++)
        {
            float b = 2f * Vector3.Dot(dh, vh);
            float c = Vector3.Dot(dh, dh);

            float tk;
            if (!TrySmallestPositiveRoot(a, b, c, out tk))
            {
                return s; // NoInterception
            }
            t = tk;

            // Siguiente iteración: apuntar corrigiendo la deriva que el viento causará durante t.
            if (k < iterations - 1) dh = dhBase - 0.5f * t * t * wind;
        }

        if (t < minFlightTime)
        {
            s.status = SolveStatus.TooClose;
            return s;
        }
        if (t > maxFlightTime)
        {
            s.status = SolveStatus.OutOfReach;
            return s;
        }

        // Velocidad horizontal que llega EXACTAMENTE al objetivo en t, descontando la deriva del viento.
        Vector3 launchH = (dhBase - 0.5f * t * t * wind) / t + vh;

        if (hasWind)
        {
            float ratio = launchH.magnitude / horizontalSpeed;
            if (ratio < MinSpeedRatio || ratio > MaxSpeedRatio)
            {
                s.status = SolveStatus.OutOfReach; // el viento deforma demasiado el tiro
                return s;
            }
        }

        float vy = (dy + vyTarget * t + 0.5f * gravity * t * t) / t;
        Vector3 v0 = launchH + Vector3.up * vy;
        float speed = v0.magnitude;

        if (speed > maxLaunchSpeed)
        {
            s.status = SolveStatus.OutOfReach;
            return s;
        }

        s.status = SolveStatus.Ok;
        s.launchVelocity = v0;
        s.flightTime = t;
        s.aimPoint = targetPos + targetVel * t;
        s.launchSpeed = speed;
        s.apexHeight = (vy > 0f && gravity > Epsilon) ? (vy * vy) / (2f * gravity) : 0f;
        return s;
    }

    /// <summary>Impacto contra un plano horizontal sin viento.</summary>
    public static bool PredictImpact(
        Vector3 origin, Vector3 velocity, float gravity, float groundHeight,
        out float ti, out Vector3 pi)
    {
        return PredictImpact(origin, velocity, gravity, Vector3.zero, groundHeight, out ti, out pi);
    }

    /// <summary>
    /// Impacto contra un plano horizontal a altura groundHeight: resuelve y(t) = groundHeight.
    ///   oy + vy·t - ½·g·t² = gy   =>   ½·g·t² - vy·t - (oy - gy) = 0
    ///   ti = (vy + sqrt(vy² + 2·g·(oy - gy))) / g          (raíz positiva, tramo descendente)
    ///   pi = origen + (vx, 0, vz)·ti + ½·viento·ti², con y = gy
    /// Devuelve false si la trayectoria nunca alcanza esa altura (discriminante negativo).
    /// </summary>
    public static bool PredictImpact(
        Vector3 origin, Vector3 velocity, float gravity, Vector3 windAcceleration, float groundHeight,
        out float ti, out Vector3 pi)
    {
        ti = 0f;
        pi = origin;

        float h = origin.y - groundHeight;
        float t;

        if (gravity < Epsilon)
        {
            // Sin gravedad: movimiento rectilíneo; solo toca el plano si desciende.
            if (velocity.y >= -Epsilon) return false;
            t = -h / velocity.y;
        }
        else
        {
            float discriminant = velocity.y * velocity.y + 2f * gravity * h;
            if (discriminant < 0f) return false;
            t = (velocity.y + Mathf.Sqrt(discriminant)) / gravity;
        }

        if (t <= 0f) return false;

        ti = t;
        pi = new Vector3(
            origin.x + velocity.x * t + 0.5f * windAcceleration.x * t * t,
            groundHeight,
            origin.z + velocity.z * t + 0.5f * windAcceleration.z * t * t);
        return true;
    }

    /// <summary>Posición balística en el instante t sin viento.</summary>
    public static Vector3 PositionAt(Vector3 origin, Vector3 velocity, float gravity, float t)
    {
        return PositionAt(origin, velocity, gravity, Vector3.zero, t);
    }

    /// <summary>Posición balística en el instante t (forma cerrada, sin integración numérica).</summary>
    public static Vector3 PositionAt(Vector3 origin, Vector3 velocity, float gravity, Vector3 windAcceleration, float t)
    {
        return origin + velocity * t + new Vector3(windAcceleration.x, -gravity, windAcceleration.z) * (0.5f * t * t);
    }

    /// <summary>Rellena buffer con puntos de la trayectoria entre t = 0 y t = duration (sin viento).</summary>
    public static void FillTrajectory(Vector3[] buffer, Vector3 origin, Vector3 velocity, float gravity, float duration)
    {
        FillTrajectory(buffer, origin, velocity, gravity, Vector3.zero, duration);
    }

    /// <summary>Rellena buffer con puntos de la trayectoria entre t = 0 y t = duration (para Gizmos o previsualización).</summary>
    public static void FillTrajectory(Vector3[] buffer, Vector3 origin, Vector3 velocity, float gravity, Vector3 windAcceleration, float duration)
    {
        if (buffer == null || buffer.Length < 2) return;
        int last = buffer.Length - 1;
        for (int i = 0; i <= last; i++)
        {
            float t = duration * i / last;
            buffer[i] = PositionAt(origin, velocity, gravity, windAcceleration, t);
        }
    }

    /// <summary>
    /// ¿La parábola llega al objetivo sin chocar antes con el terreno u obstáculos?
    /// Recorre la trayectoria en tramos rectos y lanza un SphereCast por tramo. Se ignora el final del vuelo
    /// (los últimos <paramref name="ignoreNearTarget"/> metros), donde el proyectil DEBE tocar el suelo.
    /// Sirve para elegir entre arco bajo (rápido) y arco alto (pasa por encima de rocas y colinas).
    /// </summary>
    public static bool IsTrajectoryClear(Vector3 origin, Vector3 velocity, float gravity, Vector3 windAcceleration,
        float flightTime, Vector3 target, LayerMask obstacles, float radius, int segments, float ignoreNearTarget,
        out Vector3 blockPoint)
    {
        blockPoint = Vector3.zero;
        if (obstacles.value == 0 || flightTime <= Epsilon) return true;

        segments = Mathf.Max(4, segments);
        float ignoreSqr = ignoreNearTarget * ignoreNearTarget;
        Vector3 previous = origin;

        for (int i = 1; i <= segments; i++)
        {
            float t = flightTime * i / segments;
            Vector3 next = PositionAt(origin, velocity, gravity, windAcceleration, t);
            if ((next - target).sqrMagnitude <= ignoreSqr) break; // ya está encima del objetivo

            Vector3 delta = next - previous;
            float distance = delta.magnitude;
            if (distance > Epsilon)
            {
                RaycastHit hit;
                if (Physics.SphereCast(previous, radius, delta / distance, out hit, distance, obstacles, QueryTriggerInteraction.Ignore))
                {
                    if ((hit.point - target).sqrMagnitude > ignoreSqr)
                    {
                        blockPoint = hit.point;
                        return false;
                    }
                    return true;
                }
            }
            previous = next;
        }
        return true;
    }

    // Menor raíz estrictamente positiva de a·t² + b·t + c = 0. Devuelve false si no existe.
    private static bool TrySmallestPositiveRoot(float a, float b, float c, out float root)
    {
        root = 0f;

        if (Mathf.Abs(a) < Epsilon)
        {
            // La velocidad horizontal del objetivo iguala a la del proyectil: la ecuación se vuelve lineal.
            if (Mathf.Abs(b) < Epsilon) return false;
            root = -c / b;
            return root > Epsilon;
        }

        float discriminant = b * b - 4f * a * c;
        if (discriminant < 0f) return false; // sin intercepción posible

        float sqrt = Mathf.Sqrt(discriminant);
        float t1 = (-b - sqrt) / (2f * a);
        float t2 = (-b + sqrt) / (2f * a);

        float best = float.PositiveInfinity;
        if (t1 > Epsilon) best = t1;
        if (t2 > Epsilon && t2 < best) best = t2;

        if (float.IsPositiveInfinity(best)) return false;
        root = best;
        return true;
    }
}
