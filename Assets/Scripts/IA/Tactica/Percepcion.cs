using UnityEngine;

/// <summary>
/// Percepción honesta: un NPC solo "sabe" dónde está el jugador si lo ve (cono de visión + raycast sin obstáculos)
/// o si lo oye (muy cerca). Nada de conocer su posición por arte de magia.
/// </summary>
public static class Percepcion
{
    /// <summary>Altura del pecho del jugador sobre su pivote (el astronauta mide ~2 m).</summary>
    public const float AlturaPecho = 1.0f;

    public static Vector3 PuntoPecho(Transform objetivo)
    {
        return objetivo.position + Vector3.up * AlturaPecho;
    }

    /// <summary>True si ningún obstáculo (terreno, rocas) corta la línea entre ojo y objetivo. Máscara vacía = siempre visible.</summary>
    public static bool HayLineaDeVision(Vector3 ojo, Vector3 objetivo, LayerMask obstaculos)
    {
        if (obstaculos.value == 0) return true;

        Vector3 d = objetivo - ojo;
        float distancia = d.magnitude;
        if (distancia < 0.05f) return true;

        return !Physics.Raycast(ojo, d / distancia, distancia - 0.25f, obstaculos, QueryTriggerInteraction.Ignore);
    }

    /// <summary>True si el objetivo está dentro del cono (ángulo TOTAL en grados) y del rango.</summary>
    public static bool DentroDelCono(Vector3 ojo, Vector3 adelante, Vector3 objetivo, float rango, float anguloTotal)
    {
        Vector3 d = objetivo - ojo;
        if (d.sqrMagnitude > rango * rango) return false;
        if (d.sqrMagnitude < 0.0001f) return true;
        return Vector3.Angle(adelante, d) <= anguloTotal * 0.5f;
    }

    public static bool PuedeVer(Vector3 ojo, Vector3 adelante, Vector3 objetivo, float rango, float anguloTotal, LayerMask obstaculos)
    {
        return DentroDelCono(ojo, adelante, objetivo, rango, anguloTotal) && HayLineaDeVision(ojo, objetivo, obstaculos);
    }

    /// <summary>Dibuja el cono de visión como un abanico (para Gizmos).</summary>
    public static void DibujarCono(Vector3 ojo, Vector3 adelante, float rango, float anguloTotal, Color color)
    {
        if (adelante.sqrMagnitude < 0.0001f) return;
        Gizmos.color = color;

        Vector3 eje = Vector3.Cross(adelante, Vector3.up);
        if (eje.sqrMagnitude < 0.0001f) eje = Vector3.right;

        const int segmentos = 12;
        float mitad = anguloTotal * 0.5f;
        Vector3 anterior = ojo + Quaternion.AngleAxis(-mitad, Vector3.up) * adelante.normalized * rango;
        Gizmos.DrawLine(ojo, anterior);
        for (int i = 1; i <= segmentos; i++)
        {
            float a = Mathf.Lerp(-mitad, mitad, i / (float)segmentos);
            Vector3 p = ojo + Quaternion.AngleAxis(a, Vector3.up) * adelante.normalized * rango;
            Gizmos.DrawLine(anterior, p);
            anterior = p;
        }
        Gizmos.DrawLine(ojo, anterior);

        // Arco vertical para que se note la inclinación del cono (útil en el dron, que mira hacia abajo).
        Vector3 arriba = ojo + Quaternion.AngleAxis(-mitad, eje) * adelante.normalized * rango;
        Vector3 abajo = ojo + Quaternion.AngleAxis(mitad, eje) * adelante.normalized * rango;
        Gizmos.DrawLine(ojo, arriba);
        Gizmos.DrawLine(ojo, abajo);
    }
}
