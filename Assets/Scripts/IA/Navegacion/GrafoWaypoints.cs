using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Grafo de waypoints con búsqueda A* propia (f(n) = g(n) + h(n)).
///
/// ¿Por qué un grafo además del NavMesh? Es pathfinding JERÁRQUICO:
///   * El grafo decide "por qué zonas pasar" (patrullas, búsqueda del jugador, rutas del dron). Es pequeño
///     (cientos de nodos), así que A* es barato y además podemos cambiar el costo de las aristas en tiempo real
///     (los cráteres encarecen las zonas bombardeadas).
///   * El NavMesh resuelve el detalle "cómo ir de un nodo al siguiente" esquivando rocas.
///
/// Los nodos son los WaypointNode hijos de este objeto. Se crea con el menú
/// Solar Adventure > IA Táctica > 2 · Generar grafo de waypoints.
/// </summary>
public class GrafoWaypoints : MonoBehaviour
{
    [Tooltip("Costo extra (en metros) de pasar por un nodo dentro de un cráter reciente. A* evitará las zonas bombardeadas.")]
    public float penalizacionCrater = 40f;
    [Tooltip("Mostrar nodos y aristas en la vista Scene.")]
    public bool mostrarGizmos = true;
    [Tooltip("Solo se dibujan los nodos a menos de esta distancia (m) de la cámara. Dibujar los ~2000 nodos a la vez frena el Editor.")]
    public float radioGizmos = 150f;

    private static GrafoWaypoints actual;

    private WaypointNode[] nodos = new WaypointNode[0];

    // Memoria de A* reutilizada entre búsquedas (sin basura por búsqueda).
    private float[] g;
    private float[] f;
    private int[] padre;
    private int[] marcaAbierto;
    private int[] marcaCerrado;
    private int sello;
    // Lista abierta de A*: montículo binario (mínimo arriba) con entradas repetidas; las viejas se descartan al sacarlas.
    private readonly List<int> heapIdx = new List<int>();
    private readonly List<float> heapF = new List<float>();
    private readonly List<WaypointNode> candidatosZona = new List<WaypointNode>();

    /// <summary>Grafo de la escena actual (o null si no hay).</summary>
    public static GrafoWaypoints Actual
    {
        get
        {
            if (actual == null) actual = FindFirstObjectByType<GrafoWaypoints>();
            if (actual != null && actual.nodos.Length == 0) actual.Indexar();
            return actual;
        }
    }

    public IList<WaypointNode> Nodos { get { return nodos; } }
    public int Cantidad { get { return nodos.Length; } }

    private void Awake()
    {
        actual = this;
        Indexar();
    }

    private void OnDestroy()
    {
        if (actual == this) actual = null;
    }

    private WaypointNode[] nodosGizmo;
    private int hijosCacheados = -1;

    // Dibuja todo el grafo en una sola pasada y solo cerca de la cámara (Scene o Game).
    private void OnDrawGizmos()
    {
        if (!mostrarGizmos) return;

        if (nodosGizmo == null || hijosCacheados != transform.childCount)
        {
            nodosGizmo = GetComponentsInChildren<WaypointNode>();
            hijosCacheados = transform.childCount;
        }

        Camera camara = Camera.current;
        Vector3 centro = camara != null ? camara.transform.position : Vector3.zero;
        float r2 = radioGizmos * radioGizmos;
        Vector3 alzar = Vector3.up * 0.3f;
        Color colorNodo = new Color(0.3f, 0.8f, 1f, 0.8f);
        Color colorNido = new Color(1f, 0.55f, 0.1f, 0.9f);
        Color colorArista = new Color(0.3f, 0.8f, 1f, 0.35f);

        for (int i = 0; i < nodosGizmo.Length; i++)
        {
            WaypointNode n = nodosGizmo[i];
            if (n == null) continue;

            Vector3 p = n.transform.position;
            if (camara != null && (p - centro).sqrMagnitude > r2) continue;

            Gizmos.color = n.candidatoNido ? colorNido : colorNodo;
            Gizmos.DrawSphere(p + alzar, n.candidatoNido ? 0.55f : 0.35f);

            Gizmos.color = colorArista;
            for (int k = 0; k < n.vecinos.Count; k++)
            {
                WaypointNode v = n.vecinos[k];
                // Cada arista se dibuja una sola vez.
                if (v != null && v.GetInstanceID() > n.GetInstanceID())
                {
                    Gizmos.DrawLine(p + alzar, v.transform.position + alzar);
                }
            }
        }
    }

    /// <summary>Recoge los nodos hijos y les asigna un índice.</summary>
    public void Indexar()
    {
        nodos = GetComponentsInChildren<WaypointNode>();
        for (int i = 0; i < nodos.Length; i++) nodos[i].indice = i;

        int n = nodos.Length;
        g = new float[n];
        f = new float[n];
        padre = new int[n];
        marcaAbierto = new int[n];
        marcaCerrado = new int[n];
        sello = 0;
    }

    public WaypointNode MasCercano(Vector3 p)
    {
        WaypointNode mejor = null;
        float mejorD = float.MaxValue;
        for (int i = 0; i < nodos.Length; i++)
        {
            if (nodos[i] == null) continue;
            float d = (nodos[i].Posicion - p).sqrMagnitude;
            if (d < mejorD)
            {
                mejorD = d;
                mejor = nodos[i];
            }
        }
        return mejor;
    }

    public void NodosEnRadio(Vector3 centro, float radio, List<WaypointNode> resultado)
    {
        resultado.Clear();
        float r2 = radio * radio;
        for (int i = 0; i < nodos.Length; i++)
        {
            if (nodos[i] == null) continue;
            Vector3 d = nodos[i].Posicion - centro;
            d.y = 0f;
            if (d.sqrMagnitude <= r2) resultado.Add(nodos[i]);
        }
    }

    /// <summary>Vecino al azar evitando volver al anterior (patrulla que no oscila).</summary>
    public WaypointNode VecinoAleatorio(WaypointNode actualNodo, WaypointNode anterior)
    {
        if (actualNodo == null || actualNodo.vecinos.Count == 0) return actualNodo;

        int intentos = actualNodo.vecinos.Count * 2;
        for (int k = 0; k < intentos; k++)
        {
            WaypointNode v = actualNodo.vecinos[Random.Range(0, actualNodo.vecinos.Count)];
            if (v != null && (v != anterior || actualNodo.vecinos.Count == 1)) return v;
        }
        return anterior != null ? anterior : actualNodo;
    }

    /// <summary>
    /// Igual que VecinoAleatorio pero sin alejarse más de 'radio' de 'centro' (cada enemigo patrulla su zona).
    /// Si todos los vecinos quedan fuera, vuelve por el que más se acerca al centro.
    /// </summary>
    public WaypointNode VecinoAleatorioEnZona(WaypointNode actualNodo, WaypointNode anterior, Vector3 centro, float radio)
    {
        if (actualNodo == null || actualNodo.vecinos.Count == 0) return actualNodo;
        if (radio <= 0f) return VecinoAleatorio(actualNodo, anterior);

        float r2 = radio * radio;
        candidatosZona.Clear();
        WaypointNode masCercano = null;
        float mejorD = float.MaxValue;
        for (int i = 0; i < actualNodo.vecinos.Count; i++)
        {
            WaypointNode v = actualNodo.vecinos[i];
            if (v == null) continue;
            Vector3 d = v.Posicion - centro;
            d.y = 0f;
            float d2 = d.sqrMagnitude;
            if (d2 < mejorD) { mejorD = d2; masCercano = v; }
            if (d2 <= r2 && v != anterior) candidatosZona.Add(v);
        }
        if (candidatosZona.Count > 0) return candidatosZona[Random.Range(0, candidatosZona.Count)];
        return masCercano != null ? masCercano : actualNodo;
    }

    /// <summary>
    /// A*: devuelve en <paramref name="ruta"/> los nodos desde inicio hasta meta (ambos incluidos).
    ///   g(n) = costo real acumulado desde el inicio (distancias + penalización de cráteres)
    ///   h(n) = distancia en línea recta hasta la meta (admisible: nunca sobreestima, así la ruta es óptima)
    ///   f(n) = g(n) + h(n)  -> siempre se expande el nodo abierto con menor f
    /// </summary>
    public bool BuscarRuta(WaypointNode inicio, WaypointNode meta, List<WaypointNode> ruta)
    {
        ruta.Clear();
        if (inicio == null || meta == null || !EsMio(inicio) || !EsMio(meta)) return false;
        if (inicio == meta)
        {
            ruta.Add(inicio);
            return true;
        }

        sello++;
        heapIdx.Clear();
        heapF.Clear();

        int s = inicio.indice;
        int m = meta.indice;
        g[s] = 0f;
        f[s] = Heuristica(s, m);
        padre[s] = -1;
        marcaAbierto[s] = sello;
        HeapPush(s, f[s]);

        while (heapIdx.Count > 0)
        {
            int actualIdx;
            float fSacado;
            HeapPop(out actualIdx, out fSacado);
            if (marcaCerrado[actualIdx] == sello) continue; // entrada vieja de un nodo ya cerrado

            if (actualIdx == m)
            {
                Reconstruir(m, ruta);
                return true;
            }

            marcaCerrado[actualIdx] = sello;
            List<WaypointNode> vecinos = nodos[actualIdx].vecinos;

            for (int k = 0; k < vecinos.Count; k++)
            {
                WaypointNode v = vecinos[k];
                if (v == null || !EsMio(v)) continue;
                int j = v.indice;
                if (marcaCerrado[j] == sello) continue;

                float tentativo = g[actualIdx] + CostoArista(actualIdx, j);
                if (marcaAbierto[j] != sello || tentativo < g[j])
                {
                    g[j] = tentativo;
                    f[j] = tentativo + Heuristica(j, m);
                    padre[j] = actualIdx;
                    marcaAbierto[j] = sello;
                    HeapPush(j, f[j]);
                }
            }
        }

        return false; // la meta no es alcanzable desde el inicio
    }

    private void HeapPush(int idx, float fv)
    {
        heapIdx.Add(idx);
        heapF.Add(fv);
        int i = heapIdx.Count - 1;
        while (i > 0)
        {
            int padreHeap = (i - 1) / 2;
            if (heapF[padreHeap] <= heapF[i]) break;
            HeapSwap(i, padreHeap);
            i = padreHeap;
        }
    }

    private void HeapPop(out int idx, out float fv)
    {
        idx = heapIdx[0];
        fv = heapF[0];
        int ultimo = heapIdx.Count - 1;
        heapIdx[0] = heapIdx[ultimo];
        heapF[0] = heapF[ultimo];
        heapIdx.RemoveAt(ultimo);
        heapF.RemoveAt(ultimo);

        int n = heapIdx.Count;
        int i = 0;
        while (true)
        {
            int l = 2 * i + 1, r = l + 1, menor = i;
            if (l < n && heapF[l] < heapF[menor]) menor = l;
            if (r < n && heapF[r] < heapF[menor]) menor = r;
            if (menor == i) break;
            HeapSwap(i, menor);
            i = menor;
        }
    }

    private void HeapSwap(int a, int b)
    {
        int ti = heapIdx[a]; heapIdx[a] = heapIdx[b]; heapIdx[b] = ti;
        float tf = heapF[a]; heapF[a] = heapF[b]; heapF[b] = tf;
    }

    private float Heuristica(int a, int b)
    {
        return Vector3.Distance(nodos[a].Posicion, nodos[b].Posicion);
    }

    private float CostoArista(int a, int b)
    {
        float costo = Vector3.Distance(nodos[a].Posicion, nodos[b].Posicion);
        if (CraterNavMesh.Contiene(nodos[b].Posicion, 1.5f)) costo += penalizacionCrater;
        return costo;
    }

    private void Reconstruir(int meta, List<WaypointNode> ruta)
    {
        int i = meta;
        while (i >= 0)
        {
            ruta.Add(nodos[i]);
            i = padre[i];
        }
        ruta.Reverse();
    }

    private bool EsMio(WaypointNode n)
    {
        return n.indice >= 0 && n.indice < nodos.Length && nodos[n.indice] == n;
    }
}
