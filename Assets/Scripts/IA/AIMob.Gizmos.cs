using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Dibujos de depuración (vista Scene). Selecciona un enemigo en la Hierarchy para ver todo su razonamiento.
public partial class AIMob
{
    private void OnDrawGizmos()
    {
        // El mortero dibuja todo en OnDrawGizmosSelected (selecciónalo en la jerarquía para verlo).
        if (mode == BehaviorMode.MORTAR) return;

        if (mode == BehaviorMode.TACTICO)
        {
            // Solo si la cámara de la Scene/Game está a menos de 120 m (con muchos enemigos, dibujarlos todos frena el Editor).
            Camera camara = Camera.current;
            if (camara == null || (camara.transform.position - transform.position).sqrMagnitude < 120f * 120f)
            {
                DrawTacticoGizmos();
            }
            return;
        }

        // Solo dibujar detección y frenado si pueden ocurrir
        if (mode == BehaviorMode.COMBINED || mode == BehaviorMode.SEEK_ONLY)
        {
            Gizmos.color = Color.white;
            if (mode == BehaviorMode.COMBINED) Gizmos.DrawWireSphere(transform.position, detectionRadius);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, slowRadius);
        }

        if (Application.isPlaying)
        {
            if (currentState == State.WANDER)
            {
                Gizmos.color = Color.cyan;

                Vector3 forward = new Vector3(velocity.x, 0, velocity.z).normalized;
                if (forward.sqrMagnitude < 0.1f) forward = transform.forward;

                Vector3 circleCenter = transform.position + forward * wanderDistance;
                Gizmos.DrawWireSphere(circleCenter, wanderRadius);

                float angleRad = currentWanderAngle * Mathf.Deg2Rad;
                Vector3 displacement = new Vector3(Mathf.Cos(angleRad), 0, Mathf.Sin(angleRad)) * wanderRadius;
                Gizmos.DrawSphere(circleCenter + displacement, 0.5f);
            }
            else
            {
                if (player != null)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(transform.position, player.position);
                }
            }
        }
    }

    private void DrawTacticoGizmos()
    {
        Vector3 ojo = transform.position + Vector3.up * alturaOjos;

        Color colorCono;
        switch (currentState)
        {
            case State.SEEK: colorCono = new Color(1f, 0.15f, 0.1f, 0.9f); break;
            case State.SEARCH: colorCono = new Color(1f, 0.75f, 0.1f, 0.9f); break;
            default: colorCono = new Color(0.3f, 1f, 0.4f, 0.6f); break;
        }
        Percepcion.DibujarCono(ojo, MiradaDuende, rangoVision, anguloVision, colorCono);

        Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, radioOido);

        if (!Application.isPlaying) return;

        rutaTactica.DrawGizmos(colorCono);
        Gizmos.color = colorCono;
        Gizmos.DrawLine(transform.position + Vector3.up * 0.5f, destinoTactico + Vector3.up * 0.5f);

        if (currentState == State.SEARCH)
        {
            Gizmos.color = new Color(1f, 0.75f, 0.1f, 0.9f);
            Gizmos.DrawWireSphere(puntoInvestigar, 1f);
            for (int i = 0; i < pendientesBusqueda.Count; i++)
            {
                if (pendientesBusqueda[i] != null) Gizmos.DrawWireCube(pendientesBusqueda[i].Posicion + Vector3.up * 0.5f, Vector3.one * 0.8f);
            }
            for (int i = 1; i < rutaGrafo.Count; i++)
            {
                if (rutaGrafo[i - 1] != null && rutaGrafo[i] != null)
                    Gizmos.DrawLine(rutaGrafo[i - 1].Posicion + Vector3.up, rutaGrafo[i].Posicion + Vector3.up);
            }
        }

#if UNITY_EDITOR
        string texto = currentState.ToString();
        if (currentState == State.SEEK)
        {
            int puesto = RedTactica.PuestoEnEscuadra(this);
            texto = puesto <= 0 ? "PERSIGUE" : "FLANQUEA " + puesto;
        }
        else if (currentState == State.SEARCH) texto = "BUSCA " + Mathf.CeilToInt(duracionBusqueda - tiempoBusqueda) + "s";
        Handles.Label(transform.position + Vector3.up * 2.6f, texto);
#endif
    }

    private void OnDrawGizmosSelected()
    {
        if (mode != BehaviorMode.MORTAR) return;

        // Rangos de tiro: amarillo = máximo (detección), rojo = mínimo (zona segura junto al mortero)
        DrawCircleXZ(transform.position, engageMaxRange, new Color(1f, 0.92f, 0.2f, 0.9f));
        DrawCircleXZ(transform.position, engageMinRange, new Color(1f, 0.25f, 0.2f, 0.9f));

        if (patrullarPorWaypoints) DibujarZonaPatrulla();
        DrawNestGizmos();
        DrawShotGizmos();

        if (Application.isPlaying)
        {
            nestPath.DrawGizmos(Color.cyan);
#if UNITY_EDITOR
            string info = currentState.ToString();
            if (currentState == State.ATTACK || currentState == State.ALERT)
            {
                info += salvoIsSuppression ? " · SUPRESIÓN" : " · " + patronActual;
            }
            info += seesPlayer ? " · lo ve" : (hasFreshInfo ? " · observador" : (hasStaleInfo ? " · recuerdo" : ""));
            Handles.Label(transform.position + Vector3.up * 3.2f, info);
#endif
        }
    }

    // Radio de patrulla (cian) y, en el Editor, los waypoints que recorrería desde donde está colocado.
    private void DibujarZonaPatrulla()
    {
        Vector3 centro = Application.isPlaying ? homePosition : transform.position;
        Color cian = new Color(0.3f, 0.8f, 1f, 0.9f);
        DrawCircleXZ(centro, radioPatrullaMortero, cian);
        if (Application.isPlaying) return;   // en juego se ven los puntos reales de la patrulla

        GrafoWaypoints grafo = GrafoWaypoints.Actual;
        int cantidad = 0;
        if (grafo != null)
        {
            System.Collections.Generic.List<WaypointNode> enRadio = new System.Collections.Generic.List<WaypointNode>();
            grafo.NodosEnRadio(centro, radioPatrullaMortero, enRadio);
            cantidad = enRadio.Count;
            Gizmos.color = cian;
            for (int i = 0; i < enRadio.Count; i++) Gizmos.DrawWireSphere(enRadio[i].Posicion + Vector3.up * 0.3f, 0.9f);
        }
#if UNITY_EDITOR
        Handles.Label(centro + Vector3.up * 3.6f, "Patrulla: " + cantidad + " waypoints en " + radioPatrullaMortero.ToString("0") + " m");
#endif
    }

    private void DrawNestGizmos()
    {
        // Con patrulla por waypoints, la lista Firing Nests no se usa: no se dibuja en el Editor para no confundir.
        if (!Application.isPlaying && patrullarPorWaypoints) return;
        Transform[] lista = Application.isPlaying ? nidos : firingNests;
        if (lista == null || lista.Length == 0) return;

        int count = lista.Length;
        for (int i = 0; i < count; i++)
        {
            if (lista[i] == null) continue;

            bool isCurrent = Application.isPlaying && i == currentNest;
            Gizmos.color = isCurrent ? Color.cyan : new Color(0.2f, 1f, 0.35f, 1f);
            Gizmos.DrawWireSphere(lista[i].position, 0.8f);
#if UNITY_EDITOR
            string label = "Nido " + i;
            if (Application.isPlaying && nestPattern == NestPattern.TACTICO && i < nestScores.Length && nestScores[i] > float.MinValue)
            {
                label += "  (" + nestScores[i].ToString("0.0") + ")";
            }
            Handles.Label(lista[i].position + Vector3.up * 1.4f, label);
#endif

            bool hasNext = (i + 1 < count) && lista[i + 1] != null;
            if (hasNext && nestPattern != NestPattern.TACTICO && !patrullaPorGrafo)
            {
                Gizmos.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
                Gizmos.DrawLine(lista[i].position, lista[i + 1].position);
            }
        }

        if (nestPattern == NestPattern.CIRCULAR && count > 2 && lista[0] != null && lista[count - 1] != null)
        {
            Gizmos.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
            Gizmos.DrawLine(lista[count - 1].position, lista[0].position);
        }
    }

    private void DrawShotGizmos()
    {
        Vector3 origin = firePoint != null ? firePoint.position : transform.position + Vector3.up;
        BallisticSolver.Solution solution = default(BallisticSolver.Solution);
        bool have = false;

        if (Application.isPlaying)
        {
            if (hasLastSolution)
            {
                solution = lastSolution;
                origin = lastSolveOrigin;
                have = true;
            }
        }
        else if (firePoint != null)
        {
            // En el editor: previsualiza el tiro contra el jugador si está en la escena (objetivo quieto).
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                solution = SolveShot(origin, playerObject.transform.position, Vector3.zero);
                have = true;
            }
        }

        if (!have) return;

        if (!solution.IsValid)
        {
#if UNITY_EDITOR
            Handles.Label(origin + Vector3.up * 2.5f, "Sin tiro: " + solution.status);
#endif
            return;
        }

        float duration = solution.flightTime;
        BallisticSolver.FillTrajectory(trajectoryBuffer, origin, solution.launchVelocity, CurrentGravity(), windAcceleration, duration);

        // Cian = tiro libre; naranja = el arco choca con el terreno (no dispara y se reubica).
        bool blocked = Application.isPlaying && lastShotBlocked;
        Gizmos.color = blocked ? new Color(1f, 0.5f, 0f, 1f) : Color.cyan;
        for (int i = 0; i < trajectoryBuffer.Length - 1; i++)
        {
            Gizmos.DrawLine(trajectoryBuffer[i], trajectoryBuffer[i + 1]);
        }
        if (blocked)
        {
            Gizmos.DrawWireCube(lastBlockPoint, Vector3.one * 0.8f);
        }

        float blastRadius = (projectilePrefab != null ? projectilePrefab.BlastRadius : 3f) * blastRadiusMultiplier;
        Color danger = new Color(1f, 0.15f, 0.1f, 1f);

        Gizmos.color = danger;
        Gizmos.DrawSphere(solution.aimPoint, 0.5f);
        DrawCircleXZ(solution.aimPoint, blastRadius, danger);

        if (Application.isPlaying && player != null)
        {
            // Línea magenta: desplazamiento "lead" entre donde está el jugador y donde caerá el proyectil.
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(player.position, solution.aimPoint);
#if UNITY_EDITOR
            string arco = lastArcFactor >= 0.95f ? "arco normal" : (lastArcFactor >= 0.7f ? "arco medio" : "arco alto");
            if (lastWasChokepoint) arco += " · cuello de botella";
            Handles.Label(solution.aimPoint + Vector3.up * 1.5f, arco + " · " + solution.flightTime.ToString("0.0") + " s");
#endif
        }
    }

    private static void DrawCircleXZ(Vector3 center, float radius, Color color)
    {
        const int segments = 48;
        Gizmos.color = color;
        Vector3 previous = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = (Mathf.PI * 2f) * i / segments;
            Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
