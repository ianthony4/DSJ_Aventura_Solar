using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Un punto del grafo de waypoints. Los vecinos son las aristas del grafo (se recorren con A* en GrafoWaypoints).
/// Normalmente no se crean a mano: el menú  Solar Adventure > IA Táctica > 2 · Generar grafo de waypoints
/// los reparte sobre el NavMesh y los conecta. Puedes moverlos, borrarlos o añadir vecinos a mano.
/// </summary>
[DisallowMultipleComponent]
public class WaypointNode : MonoBehaviour
{
    [Tooltip("Nodos conectados con este (aristas del grafo). La conexión debe ir en ambos sentidos.")]
    public List<WaypointNode> vecinos = new List<WaypointNode>();

    [Tooltip("Buen punto de tiro para morteros (promontorio o zona alta). El generador lo marca solo; puedes cambiarlo.")]
    public bool candidatoNido;

    [HideInInspector] public int indice = -1;

    public Vector3 Posicion { get { return transform.position; } }

    // El dibujo de todos los nodos lo hace GrafoWaypoints (en una sola pasada y solo los cercanos a la cámara).
    // Aquí solo se resalta el nodo seleccionado y sus conexiones.
    private void OnDrawGizmosSelected()
    {
        Vector3 p = transform.position + Vector3.up * 0.3f;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(p, 0.9f);
        for (int i = 0; i < vecinos.Count; i++)
        {
            if (vecinos[i] != null) Gizmos.DrawLine(p, vecinos[i].transform.position + Vector3.up * 0.3f);
        }
    }
}
