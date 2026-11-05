using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Cada explosión deja un cráter temporal que RECORTA el NavMesh (NavMeshObstacle con carving):
/// los enemigos recalculan sus rutas y lo rodean, y A* en el grafo de waypoints encarece esa zona.
/// Es el puente visible entre la física de los proyectiles y el pathfinding.
///
/// No hay que ponerlo en la escena: se engancha solo a MortarProjectile.AnyExploded.
/// Se configura en el perfil de artillería del planeta (sección "Cráteres").
/// Sin arte nuevo: la marca es un disco oscuro hecho con un cilindro primitivo.
/// </summary>
public class CraterNavMesh : MonoBehaviour
{
    public static readonly List<CraterNavMesh> Activos = new List<CraterNavMesh>();

    private static readonly Color ColorMarca = new Color(0.08f, 0.06f, 0.05f, 1f);

    private float radio;
    private float fin;
    private float duracion;
    private Transform marca;
    private Vector3 escalaMarca;

    public float Radio { get { return radio; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Registrar()
    {
        Activos.Clear();
        MortarProjectile.AnyExploded -= AlExplotar;
        MortarProjectile.AnyExploded += AlExplotar;
    }

    private static void AlExplotar(MortarImpactInfo info)
    {
        bool activo = true;
        bool mostrarMarca = true;
        float factor = 0.45f;
        float segundos = 15f;

        PerfilArtilleria perfil = ConfiguracionArtilleria.Actual != null ? ConfiguracionArtilleria.Actual.perfil : null;
        if (perfil != null)
        {
            activo = perfil.crateresBloqueanRuta;
            mostrarMarca = perfil.mostrarMarcaCrater;
            factor = perfil.radioCraterFactor;
            segundos = perfil.duracionCrater;
        }

        if (!activo || segundos <= 0f || factor <= 0f) return;
        Crear(info.position, info.blastRadius * factor, segundos, mostrarMarca);
    }

    public static CraterNavMesh Crear(Vector3 posicion, float radio, float segundos, bool conMarca)
    {
        GameObject go = new GameObject("Crater_IA");
        go.transform.position = posicion;

        NavMeshObstacle obstaculo = go.AddComponent<NavMeshObstacle>();
        obstaculo.shape = NavMeshObstacleShape.Capsule;
        obstaculo.radius = Mathf.Max(0.3f, radio);
        obstaculo.height = 4f;
        obstaculo.center = Vector3.zero;
        obstaculo.carving = true;
        obstaculo.carveOnlyStationary = true;

        CraterNavMesh crater = go.AddComponent<CraterNavMesh>();
        crater.radio = radio;
        crater.duracion = segundos;
        crater.fin = Time.time + segundos;

        if (conMarca) crater.CrearMarca();
        return crater;
    }

    private void CrearMarca()
    {
        GameObject disco = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disco.name = "Marca";
        Collider col = disco.GetComponent<Collider>();
        if (col != null) Destroy(col);

        disco.transform.SetParent(transform, false);
        disco.transform.localPosition = Vector3.up * 0.04f;
        escalaMarca = new Vector3(radio * 2f, 0.02f, radio * 2f);
        disco.transform.localScale = escalaMarca;

        Renderer r = disco.GetComponent<Renderer>();
        if (r != null)
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            MaterialPropertyBlock bloque = new MaterialPropertyBlock();
            bloque.SetColor("_BaseColor", ColorMarca); // URP
            bloque.SetColor("_Color", ColorMarca);     // Built-in
            r.SetPropertyBlock(bloque);
        }
        marca = disco.transform;
    }

    private void OnEnable() { Activos.Add(this); }
    private void OnDisable() { Activos.Remove(this); }

    private void Update()
    {
        float restante = fin - Time.time;
        if (marca != null && restante < 2f)
        {
            // Se encoge en los últimos 2 s: el jugador ve que la zona vuelve a ser transitable.
            float k = Mathf.Clamp01(restante / 2f);
            marca.localScale = new Vector3(escalaMarca.x * k, escalaMarca.y, escalaMarca.z * k);
        }
        if (restante <= 0f) Destroy(gameObject);
    }

    /// <summary>¿El punto cae dentro de algún cráter activo (más un margen)?</summary>
    public static bool Contiene(Vector3 punto, float margen)
    {
        for (int i = 0; i < Activos.Count; i++)
        {
            CraterNavMesh c = Activos[i];
            if (c == null) continue;
            float dx = punto.x - c.transform.position.x;
            float dz = punto.z - c.transform.position.z;
            float r = c.radio + margen;
            if (dx * dx + dz * dz <= r * r) return true;
        }
        return false;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.6f, 0.3f, 0.1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, radio);
    }
}
