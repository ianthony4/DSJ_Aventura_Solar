using System.Collections.Generic;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Menú  Solar Adventure > IA Táctica  : monta en la escena abierta todo lo que la IA táctica necesita,
/// para no tener que hacerlo a mano. Cada paso se puede repetir sin romper nada (reemplaza lo que generó antes).
///
///   Montar todo           pasos 1 a 6 seguidos + validación
///   1 · Hornear NavMesh   NavMeshSurface sobre la zona de juego (capas Suelo y Default) y guarda el asset
///   2 · Grafo waypoints   reparte nodos sobre el NavMesh, los conecta y marca promontorios como nidos
///   3 · Dron observador   lo arma con primitivas, lo guarda como prefab y lo coloca
///   4 · Fuego amigo       el proyectil del mortero también daña a la capa Enemigo
///   5 · Granadas          añade LanzadorGranadas al jugador y lo rellena
///   6 · Morteros          nidos TACTICO, visión requerida, máscara de suelo y salud
///   7 · HUD táctico       vida, detección, granadas y avisos
///   Preparar planeta      TODO para un planeta nuevo: jugador, suelo, perfil, cámara, morteros + Montar todo
///   Crear prefab mortero  guarda Mortero_1 de Mercurio como Assets/Prefabs/Mortero.prefab
///   Validar escena        lista lo que falta
/// </summary>
public static class MontajeIATactica
{
    private const string Menu = "Solar Adventure/IA Táctica/";
    private const string NombreNavMesh = "NavMesh_IA";
    private const string NombreGrafo = "GrafoWaypoints";
    private const string RutaProyectil = "Assets/Prefabs/Proyectil_Mortero.prefab";
    private const string RutaMaterialAviso = "Assets/Materiales/Mat_AnilloAviso.mat";
    private const string CarpetaMateriales = "Assets/Materiales/IA";
    private const string RutaPrefabDron = "Assets/Prefabs/Dron_Observador.prefab";
    private const string RutaPrefabMortero = "Assets/Prefabs/Mortero.prefab";
    private const string RutaEscenaMercurio = "Assets/Scenes/Mercury.unity";
    private const string CarpetaPerfiles = "Assets/Data/Artilleria";

    private const float EspacioMinimoGrafo = 12f;
    private const int MaximoNodos = 2000;

    // ------------------------------------------------------------------ Montar todo

    [MenuItem(Menu + "Montar todo en la escena abierta", false, 0)]
    public static void MontarTodo()
    {
        if (!EscenaGuardada()) return;
        if (!EditorUtility.DisplayDialog("Montar IA táctica",
            "Se va a preparar la escena '" + SceneManager.GetActiveScene().name + "':\n\n" +
            "1. Hornear NavMesh\n2. Generar grafo de waypoints\n3. Crear dron observador (si no hay)\n" +
            "4. Activar fuego amigo en el proyectil\n5. Dar granadas al jugador\n6. Configurar morteros\n\n" +
            "Puedes deshacer con Ctrl+Z o repetirlo cuando quieras.", "Montar", "Cancelar")) return;

        try
        {
            if (!PasosIATactica(false)) return;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        MostrarInformeFinal("");
    }

    // Pasos 1 a 7. Con colocarMorteros, si la escena no tiene ninguno, coloca dos del prefab sobre promontorios del grafo.
    private static bool PasosIATactica(bool colocarMorteros)
    {
        EditorUtility.DisplayProgressBar("IA táctica", "Horneando NavMesh...", 0.3f);
        if (!HornearNavMeshInterno()) return false;
        EditorUtility.DisplayProgressBar("IA táctica", "Generando grafo de waypoints...", 0.55f);
        GenerarGrafoInterno();
        if (colocarMorteros)
        {
            EditorUtility.DisplayProgressBar("IA táctica", "Colocando morteros...", 0.7f);
            ColocarMorterosInterno(2);
        }
        EditorUtility.DisplayProgressBar("IA táctica", "Dron, fuego amigo, granadas, morteros y HUD...", 0.85f);
        if (Object.FindObjectsByType<DronObservador>(FindObjectsSortMode.None).Length == 0) CrearDronInterno();
        ActivarFuegoAmigoInterno();
        DarGranadasInterno();
        ConfigurarMorterosInterno();
        AgregarHUDInterno();
        return true;
    }

    private static void MostrarInformeFinal(string notas)
    {
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        string informe = Validar(out int errores);
        EditorUtility.DisplayDialog("IA táctica",
            (errores == 0 ? "Listo. " : "Montado con " + errores + " aviso(s). ") +
            "Guarda la escena con Ctrl+S.\n\n" + notas + informe, "OK");
    }

    // ------------------------------------------------------------------ Preparar planeta

    [MenuItem(Menu + "Preparar planeta completo (escena abierta)", false, -10)]
    public static void PrepararPlaneta()
    {
        if (!EscenaGuardada()) return;
        Scene escena = SceneManager.GetActiveScene();
        if (!EditorUtility.DisplayDialog("Preparar planeta",
            "Se va a preparar '" + escena.name + "' de principio a fin:\n\n" +
            "• Astronauta: capa Jugador + SaludJugador\n• Suelo: capa Suelo (y collider si le falta)\n" +
            "• Config_Artilleria con el perfil del planeta\n• Sacudida de cámara\n" +
            "• NavMesh, grafo de waypoints y 2 morteros (si no hay)\n• Dron, fuego amigo, granadas y HUD\n\n" +
            "Se puede deshacer con Ctrl+Z.", "Preparar", "Cancelar")) return;

        StringBuilder notas = new StringBuilder();
        try
        {
            EditorUtility.DisplayProgressBar("Preparar planeta", "Jugador, suelo, perfil y cámara...", 0.1f);
            if (!PrepararJugadorInterno(notas)) return;
            PrepararSueloInterno(notas);
            PrepararConfiguracionInterno(escena.name, notas);
            PrepararCamaraInterno(notas);
            if (!PasosIATactica(true)) return;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        MostrarInformeFinal(notas.Length > 0 ? notas.ToString() + "\n" : "");
    }

    private static bool PrepararJugadorInterno(StringBuilder notas)
    {
        GameObject jugador = GameObject.FindGameObjectWithTag("Player");
        if (jugador == null)
        {
            EditorUtility.DisplayDialog("Preparar planeta", "No hay ningún objeto activo con el tag Player (el astronauta).", "OK");
            return false;
        }

        int capa = LayerMask.NameToLayer("Jugador");
        if (capa >= 0) PonerCapa(jugador, capa);
        if (jugador.GetComponent<SaludJugador>() == null)
        {
            Undo.AddComponent<SaludJugador>(jugador);
            notas.AppendLine("• SaludJugador añadido a '" + jugador.name + "'.");
        }
        return true;
    }

    // El suelo es: los Terrain de la escena; si no hay, los objetos llamados Ground/Suelo/Terrain/Floor/Piso;
    // si tampoco, lo que tengas seleccionado.
    private static void PrepararSueloInterno(StringBuilder notas)
    {
        int capa = LayerMask.NameToLayer("Suelo");
        if (capa < 0) return;

        List<GameObject> suelos = new List<GameObject>();
        foreach (Terrain t in Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None)) suelos.Add(t.gameObject);

        if (suelos.Count == 0)
        {
            string[] prefijos = { "ground", "suelo", "terrain", "floor", "piso" };
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                string n = t.name.ToLowerInvariant();
                bool coincide = false;
                foreach (string p in prefijos) if (n.StartsWith(p)) coincide = true;
                // Solo el objeto más alto con ese nombre (no cada hijo).
                if (coincide && (t.parent == null || !EmpiezaPorAlguno(t.parent.name.ToLowerInvariant(), prefijos))) suelos.Add(t.gameObject);
            }
        }
        if (suelos.Count == 0) suelos.AddRange(Selection.gameObjects);

        if (suelos.Count == 0)
        {
            notas.AppendLine("✖ No encontré el suelo. Selecciónalo en la Hierarchy y vuelve a usar 'Preparar planeta'.");
            return;
        }

        int collidersNuevos = 0;
        foreach (GameObject s in suelos)
        {
            PonerCapa(s, capa);
            if (s.GetComponentInChildren<Collider>() != null) continue;
            foreach (MeshFilter mf in s.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null || mf.GetComponent<Collider>() != null) continue;
                Undo.AddComponent<MeshCollider>(mf.gameObject);
                collidersNuevos++;
            }
        }
        notas.AppendLine("• Suelo: " + suelos.Count + " objeto(s) en la capa Suelo" +
                         (collidersNuevos > 0 ? ", " + collidersNuevos + " MeshCollider añadidos." : "."));
    }

    private static bool EmpiezaPorAlguno(string texto, string[] prefijos)
    {
        foreach (string p in prefijos) if (texto.StartsWith(p)) return true;
        return false;
    }

    private static void PrepararConfiguracionInterno(string nombreEscena, StringBuilder notas)
    {
        ConfiguracionArtilleria cfg = Object.FindFirstObjectByType<ConfiguracionArtilleria>();
        if (cfg == null)
        {
            GameObject go = new GameObject("Config_Artilleria");
            Undo.RegisterCreatedObjectUndo(go, "Crear Config_Artilleria");
            cfg = go.AddComponent<ConfiguracionArtilleria>();
        }
        if (cfg.perfil != null) return;

        string archivo = PerfilDeEscena(nombreEscena);
        string ruta = CarpetaPerfiles + "/Perfil_" + archivo + ".asset";
        PerfilArtilleria perfil = AssetDatabase.LoadAssetAtPath<PerfilArtilleria>(ruta);
        if (perfil == null)
        {
            CrearPerfilesArtilleria.CrearPerfiles();
            perfil = AssetDatabase.LoadAssetAtPath<PerfilArtilleria>(ruta);
        }
        Undo.RecordObject(cfg, "Asignar perfil");
        cfg.perfil = perfil;
        EditorUtility.SetDirty(cfg);
        notas.AppendLine(perfil != null ? "• Config_Artilleria con el perfil " + archivo + "." : "✖ No encontré " + ruta + ".");
    }

    private static string PerfilDeEscena(string escena)
    {
        string e = escena.ToLowerInvariant();
        if (e.Contains("mercur")) return "Mercurio";
        if (e.Contains("venus")) return "Venus";
        if (e.Contains("earth") || e.Contains("tierra")) return "Tierra";
        if (e.Contains("mars") || e.Contains("marte")) return "Marte";
        if (e.Contains("jupiter") || e.Contains("júpiter")) return "Jupiter";
        if (e.Contains("satur")) return "Saturno";
        if (e.Contains("uran")) return "Urano";
        if (e.Contains("neptun")) return "Neptuno";
        return "Tierra";
    }

    private static void PrepararCamaraInterno(StringBuilder notas)
    {
        GameObject jugador = GameObject.FindGameObjectWithTag("Player");
        Camera cam = jugador != null ? jugador.GetComponentInChildren<Camera>() : null;
        if (cam == null) cam = Camera.main;
        if (cam == null || cam.GetComponent<CameraShake>() != null) return;
        Undo.AddComponent<CameraShake>(cam.gameObject);
        notas.AppendLine("• Sacudida de cámara añadida a '" + cam.name + "'.");
    }

    private static void PonerCapa(GameObject raiz, int capa)
    {
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
        {
            if (t.gameObject.layer == capa) continue;
            Undo.RecordObject(t.gameObject, "Cambiar capa");
            t.gameObject.layer = capa;
        }
    }

    // ------------------------------------------------------------------ Prefab de mortero

    [MenuItem(Menu + "Crear prefab de mortero (desde Mortero_1)", false, 45)]
    public static void CrearPrefabMortero()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefabMortero) != null &&
            !EditorUtility.DisplayDialog("Prefab de mortero", "Ya existe " + RutaPrefabMortero + ". ¿Reemplazarlo con el mortero actual?", "Reemplazar", "Cancelar")) return;

        AssetDatabase.DeleteAsset(RutaPrefabMortero);
        GameObject prefab = ObtenerPrefabMortero();
        if (prefab != null)
        {
            EditorGUIUtility.PingObject(prefab);
            EditorUtility.DisplayDialog("Prefab de mortero", "Creado " + RutaPrefabMortero + ". 'Preparar planeta' lo usa en los demás planetas.", "OK");
        }
    }

    /// <summary>
    /// Devuelve Assets/Prefabs/Mortero.prefab. Si no existe, lo crea copiando un mortero de la escena abierta
    /// o, si no hay, el Mortero_1 de Mercury.unity (abre esa escena un momento y la cierra).
    /// El prefab no lleva nidos asignados: usa los nodos del grafo de cada planeta.
    /// </summary>
    private static GameObject ObtenerPrefabMortero()
    {
        // Prioridad: la nave (Solar Adventure > Enemigos). Si no existe, se intenta crearla.
        GameObject nave = AssetDatabase.LoadAssetAtPath<GameObject>(ReemplazoEnemigos.RutaPrefabNave);
        if (nave == null && AssetDatabase.LoadAssetAtPath<GameObject>(ReemplazoEnemigos.RutaModeloMortero) != null)
        {
            nave = ReemplazoEnemigos.CrearPrefabNave();
        }
        if (nave != null) return nave;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefabMortero);
        if (prefab != null) return prefab;

        AIMob fuente = BuscarMortero(SceneManager.GetActiveScene());
        Scene temporal = default(Scene);
        bool abierta = false;
        if (fuente == null && System.IO.File.Exists(RutaEscenaMercurio))
        {
            Scene ya = SceneManager.GetSceneByPath(RutaEscenaMercurio);
            if (ya.IsValid() && ya.isLoaded) fuente = BuscarMortero(ya);
            else
            {
                temporal = EditorSceneManager.OpenScene(RutaEscenaMercurio, OpenSceneMode.Additive);
                abierta = true;
                fuente = BuscarMortero(temporal);
            }
        }

        if (fuente != null) prefab = GuardarPrefabMortero(fuente.gameObject);
        if (abierta) EditorSceneManager.CloseScene(temporal, true);

        if (prefab == null) Debug.LogError("IA táctica: no encontré ningún mortero (AIMob en modo MORTAR) para crear " + RutaPrefabMortero + ". Monta uno con la guía 04.");
        return prefab;
    }

    private static AIMob BuscarMortero(Scene escena)
    {
        if (!escena.IsValid() || !escena.isLoaded) return null;
        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            foreach (AIMob mob in raiz.GetComponentsInChildren<AIMob>(true))
            {
                if (mob.mode == AIMob.BehaviorMode.MORTAR && mob.cannonPivot != null && mob.firePoint != null) return mob;
            }
        }
        return null;
    }

    private static GameObject GuardarPrefabMortero(GameObject fuente)
    {
        GameObject copia = Object.Instantiate(fuente);
        copia.name = "Mortero";
        copia.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        AIMob mob = copia.GetComponent<AIMob>();
        mob.firingNests = new Transform[0]; // los nidos de Mercurio no existen en otros planetas
        mob.nestPattern = AIMob.NestPattern.TACTICO;
        mob.requiereVision = true;
        mob.currentState = AIMob.State.PATROL;
        if (mob.saludMaxima < 50f) mob.saludMaxima = 75f;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(copia, RutaPrefabMortero);
        Object.DestroyImmediate(copia);
        Debug.Log("IA táctica: prefab de mortero creado en " + RutaPrefabMortero + " a partir de '" + fuente.name + "'.", prefab);
        return prefab;
    }

    // Coloca 'cantidad' morteros sobre nodos del grafo (mejor promontorios) a 30–90 m del jugador y separados entre sí.
    private static int ColocarMorterosInterno(int cantidad)
    {
        foreach (AIMob mob in Object.FindObjectsByType<AIMob>(FindObjectsSortMode.None))
        {
            if (mob.mode == AIMob.BehaviorMode.MORTAR) return 0; // la escena ya tiene artillería
        }

        GameObject prefab = ObtenerPrefabMortero();
        GrafoWaypoints grafo = Object.FindFirstObjectByType<GrafoWaypoints>();
        GameObject jugador = GameObject.FindGameObjectWithTag("Player");
        if (prefab == null || grafo == null || jugador == null) return 0;

        Vector3 origen = jugador.transform.position;
        List<WaypointNode> nodos = new List<WaypointNode>(grafo.GetComponentsInChildren<WaypointNode>());
        nodos.Sort((a, b) => PuntajeColocacion(a, origen).CompareTo(PuntajeColocacion(b, origen)));

        List<Vector3> usados = new List<Vector3>();
        foreach (WaypointNode n in nodos)
        {
            if (usados.Count >= cantidad) break;
            float d = DistanciaPlana(n.Posicion, origen);
            if (d < 30f || d > 90f) continue;
            bool lejos = true;
            foreach (Vector3 u in usados) if (DistanciaPlana(u, n.Posicion) < 35f) lejos = false;
            if (!lejos) continue;

            GameObject m = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(m, "Colocar mortero");
            m.name = "Mortero_" + (usados.Count + 1);
            Vector3 mirar = origen - n.Posicion;
            mirar.y = 0f;
            m.transform.SetPositionAndRotation(n.Posicion + Vector3.up * 0.05f,
                mirar.sqrMagnitude > 0.01f ? Quaternion.LookRotation(mirar) : Quaternion.identity);
            usados.Add(n.Posicion);
        }

        if (usados.Count > 0) Debug.Log("IA táctica: " + usados.Count + " mortero(s) colocados. Muévelos si quieres otra posición.");
        return usados.Count;
    }

    // Menor = mejor sitio: promontorio y a unos 50 m del jugador.
    private static float PuntajeColocacion(WaypointNode n, Vector3 jugador)
    {
        return Mathf.Abs(DistanciaPlana(n.Posicion, jugador) - 50f) - (n.candidatoNido ? 25f : 0f);
    }

    private static float DistanciaPlana(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // ------------------------------------------------------------------ 7 · HUD

    [MenuItem(Menu + "7 · Añadir HUD táctico", false, 26)]
    public static void AgregarHUD()
    {
        AgregarHUDInterno();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    private static void AgregarHUDInterno()
    {
        if (Object.FindFirstObjectByType<HUDTactico>() != null) return;
        GameObject go = new GameObject("HUD_Tactico");
        Undo.RegisterCreatedObjectUndo(go, "Crear HUD táctico");
        go.AddComponent<HUDTactico>();
    }

    // ------------------------------------------------------------------ 1 · NavMesh

    [MenuItem(Menu + "1 · Hornear NavMesh", false, 20)]
    public static void HornearNavMesh()
    {
        if (!EscenaGuardada()) return;
        if (HornearNavMeshInterno())
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("NavMesh", "NavMesh horneado. Se ve en azul en la vista Scene (si no, activa Gizmos). Guarda la escena.", "OK");
        }
    }

    private static bool HornearNavMeshInterno()
    {
        Scene escena = SceneManager.GetActiveScene();

        GameObject go = GameObject.Find(NombreNavMesh);
        if (go == null)
        {
            go = new GameObject(NombreNavMesh);
            Undo.RegisterCreatedObjectUndo(go, "Crear NavMesh_IA");
        }
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        NavMeshSurface surface = go.GetComponent<NavMeshSurface>();
        // Un volumen ya configurado (Collect Objects = Volume) se respeta SOLO si cubre al jugador, las monedas y la nave.
        // Un volumen viejo que no los cubre (p. ej. uno automático mal calculado) se recalcula.
        bool configuradoAMano = surface != null && surface.collectObjects == CollectObjects.Volume &&
                                surface.size.x >= 100f && surface.size.z >= 100f &&
                                VolumenCubreElJuego(new Bounds(surface.center, surface.size));
        if (surface != null && !configuradoAMano && surface.collectObjects == CollectObjects.Volume)
        {
            Debug.LogWarning("IA táctica: el volumen actual de NavMesh_IA no cubre al jugador, las monedas y la nave; se recalcula la zona.", surface);
        }
        if (surface == null) surface = Undo.AddComponent<NavMeshSurface>(go);
        Undo.RecordObject(surface, "Configurar NavMeshSurface");

        Bounds zona;
        if (configuradoAMano)
        {
            zona = new Bounds(surface.center, surface.size);
            Debug.Log("IA táctica: NavMesh_IA ya tiene un volumen configurado; se respeta (centro " + zona.center +
                      ", tamaño " + zona.size + ", vóxel " + surface.voxelSize + "). Solo se vuelve a hornear.", surface);
        }
        else
        {
            zona = ZonaDeJuego(60f);
            surface.collectObjects = CollectObjects.Volume;
            surface.center = zona.center;
            surface.size = zona.size;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = MascaraNavMesh();
            surface.overrideVoxelSize = true;
            // Zonas grandes (más de 100 000 m²): vóxel de 0,5 m; con 0,25 m el horneado tardaría demasiado.
            surface.voxelSize = (zona.size.x * zona.size.z > 100000f) ? 0.5f : 0.25f;
            surface.ignoreNavMeshAgent = true;
            surface.ignoreNavMeshObstacle = true;
        }

        int ignorados = IgnorarTriggers();

        surface.BuildNavMesh();
        if (surface.navMeshData != null && NavMesh.CalculateTriangulation().vertices.Length == 0)
        {
            // El suelo no tiene colliders: se intenta con las mallas visibles.
            Debug.LogWarning("IA táctica: el suelo no tiene colliders; horneando con las mallas visibles. Los proyectiles necesitan colliders en el suelo.");
            surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            surface.BuildNavMesh();
        }
        if (surface.navMeshData == null)
        {
            Debug.LogError("IA táctica: no se pudo hornear el NavMesh. ¿El terreno tiene collider y está en la capa Suelo?");
            return false;
        }

        // Guardar el NavMesh como asset junto a la escena (si no, se pierde al cerrar).
        string carpetaEscena = System.IO.Path.GetDirectoryName(escena.path).Replace('\\', '/');
        string carpeta = carpetaEscena + "/" + escena.name;
        if (!AssetDatabase.IsValidFolder(carpeta)) AssetDatabase.CreateFolder(carpetaEscena, escena.name);
        string ruta = carpeta + "/NavMesh_IA.asset";
        if (AssetDatabase.LoadAssetAtPath<NavMeshData>(ruta) != null) AssetDatabase.DeleteAsset(ruta);
        AssetDatabase.CreateAsset(surface.navMeshData, ruta);
        AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(surface);

        int vertices = NavMesh.CalculateTriangulation().vertices.Length;
        Debug.Log("IA táctica: NavMesh horneado (" + vertices + " vértices, zona " + zona.size.x.ToString("0") + " x " +
                  zona.size.z.ToString("0") + " m, " + ignorados + " triggers ignorados). Guardado en " + ruta, surface);
        return true;
    }

    private static int MascaraNavMesh()
    {
        int suelo = LayerMask.NameToLayer("Suelo");
        int mascara = 1 << 0; // Default (rocas, obstáculos)
        if (suelo >= 0) mascara |= 1 << suelo;
        return mascara;
    }

    // Monedas, zonas de extracción y demás triggers no son obstáculos: se excluyen del horneado.
    private static int IgnorarTriggers()
    {
        int n = 0;
        Collider[] colliders = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);
        foreach (Collider c in colliders)
        {
            if (!c.isTrigger) continue;
            bool tieneSolido = false;
            foreach (Collider otro in c.GetComponents<Collider>()) if (!otro.isTrigger) tieneSolido = true;
            if (tieneSolido) continue;

            NavMeshModifier mod = c.GetComponent<NavMeshModifier>();
            if (mod == null) mod = Undo.AddComponent<NavMeshModifier>(c.gameObject);
            if (!mod.ignoreFromBuild)
            {
                Undo.RecordObject(mod, "Ignorar trigger en NavMesh");
                mod.ignoreFromBuild = true;
                mod.applyToChildren = false;
            }
            n++;
        }
        return n;
    }

    // ------------------------------------------------------------------ 2 · Grafo

    [MenuItem(Menu + "2 · Generar grafo de waypoints", false, 21)]
    public static void GenerarGrafo()
    {
        if (GenerarGrafoInterno())
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
    }

    private static bool GenerarGrafoInterno()
    {
        try { return GenerarGrafoNucleo(); }
        finally { EditorUtility.ClearProgressBar(); }
    }

    // El grafo se genera dentro de la MISMA zona que el NavMesh (el volumen de NavMesh_IA si es un volumen).
    private static Bounds ZonaDelNavMesh(float margenSiAutomatica)
    {
        GameObject go = GameObject.Find(NombreNavMesh);
        NavMeshSurface s = go != null ? go.GetComponent<NavMeshSurface>() : null;
        if (s != null && s.collectObjects == CollectObjects.Volume && s.size.x >= 100f && s.size.z >= 100f)
        {
            Bounds v = new Bounds(s.center, s.size);
            if (VolumenCubreElJuego(v)) return v;
        }
        return ZonaDeJuego(margenSiAutomatica);
    }

    // ¿El volumen contiene (en X y Z) al jugador, todas las monedas y la zona de extracción?
    private static bool VolumenCubreElJuego(Bounds v)
    {
        List<Vector3> clave = new List<Vector3>();
        GameObject jugador = GameObject.FindGameObjectWithTag("Player");
        if (jugador != null) clave.Add(jugador.transform.position);
        foreach (Moneda m in Object.FindObjectsByType<Moneda>(FindObjectsSortMode.None)) clave.Add(m.transform.position);
        foreach (ZonaExtraccion z in Object.FindObjectsByType<ZonaExtraccion>(FindObjectsSortMode.None)) clave.Add(z.transform.position);

        foreach (Vector3 p in clave)
        {
            if (p.x < v.min.x || p.x > v.max.x || p.z < v.min.z || p.z > v.max.z) return false;
        }
        return true;
    }

    // Un objeto cuenta como "jaula" si él o alguno de sus padres lleva el tag LimiteMundo.
    private static bool EsLimiteMundo(Transform t)
    {
        for (; t != null; t = t.parent) if (t.CompareTag("LimiteMundo")) return true;
        return false;
    }

    // Primer impacto hacia abajo que no sea la jaula (su techo no es suelo).
    private static bool BajarRayoAlSuelo(Vector3 arriba, float largo, int mascara, out Vector3 punto)
    {
        punto = Vector3.zero;
        float mejor = float.MaxValue;
        bool hay = false;
        foreach (RaycastHit h in Physics.RaycastAll(arriba, Vector3.down, largo, mascara, QueryTriggerInteraction.Ignore))
        {
            if (EsLimiteMundo(h.collider.transform)) continue;
            if (h.distance < mejor) { mejor = h.distance; punto = h.point; hay = true; }
        }
        return hay;
    }

    private static bool GenerarGrafoNucleo()
    {
        NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
        if (tri.vertices.Length == 0)
        {
            EditorUtility.DisplayDialog("Grafo de waypoints", "Primero hornea el NavMesh (paso 1).", "OK");
            return false;
        }

        Bounds navBounds = new Bounds(tri.vertices[0], Vector3.zero);
        foreach (Vector3 v in tri.vertices) navBounds.Encapsulate(v);

        Bounds zona = ZonaDelNavMesh(40f);
        float minX = Mathf.Max(zona.min.x, navBounds.min.x), maxX = Mathf.Min(zona.max.x, navBounds.max.x);
        float minZ = Mathf.Max(zona.min.z, navBounds.min.z), maxZ = Mathf.Min(zona.max.z, navBounds.max.z);
        if (maxX <= minX || maxZ <= minZ) { minX = navBounds.min.x; maxX = navBounds.max.x; minZ = navBounds.min.z; maxZ = navBounds.max.z; }

        float area = (maxX - minX) * (maxZ - minZ);
        float espacio = Mathf.Max(EspacioMinimoGrafo, Mathf.Sqrt(area / MaximoNodos));
        int suelo = MascaraNavMesh();

        // Los rayos parten de la parte alta de la zona (nunca por encima del volumen del NavMesh) y se ignora la jaula.
        float yRayo = Mathf.Min(navBounds.max.y + 50f, Mathf.Max(zona.max.y, navBounds.max.y + 5f));
        float largoRayo = yRayo - navBounds.min.y + 40f;

        // 1) Muestrear una rejilla: bajar un rayo hasta el suelo y proyectarlo sobre el NavMesh.
        List<Vector3> puntos = new List<Vector3>();
        int columnas = Mathf.Max(1, Mathf.CeilToInt((maxX - minX) / espacio));
        int columna = 0;
        for (float x = minX + espacio * 0.5f; x < maxX; x += espacio)
        {
            columna++;
            if (EditorUtility.DisplayCancelableProgressBar("Grafo de waypoints", "Muestreando el terreno (" + puntos.Count + " puntos)...", 0.3f * columna / columnas)) return false;
            for (float z = minZ + espacio * 0.5f; z < maxZ; z += espacio)
            {
                Vector3 arriba = new Vector3(x, yRayo, z);
                Vector3 impacto;
                if (!BajarRayoAlSuelo(arriba, largoRayo, suelo, out impacto)) continue;

                NavMeshHit nh;
                if (!NavMesh.SamplePosition(impacto, out nh, espacio * 0.4f, NavMesh.AllAreas)) continue;

                bool cerca = false;
                foreach (Vector3 p in puntos) if ((p - nh.position).sqrMagnitude < espacio * espacio * 0.25f) { cerca = true; break; }
                if (!cerca) puntos.Add(nh.position);
            }
        }

        // 2) Conectar vecinos: línea recta libre sobre el NavMesh, o ruta corta (no rodea demasiado).
        int n = puntos.Count;
        List<int>[] vecinos = new List<int>[n];
        for (int i = 0; i < n; i++) vecinos[i] = new List<int>();
        NavMeshPath path = new NavMeshPath();
        float maxArista = espacio * 1.5f;
        for (int i = 0; i < n; i++)
        {
            if (i % 25 == 0 && EditorUtility.DisplayCancelableProgressBar("Grafo de waypoints", "Conectando " + n + " nodos...", 0.3f + 0.65f * i / Mathf.Max(1, n))) return false;
            for (int j = i + 1; j < n; j++)
            {
                float d = Vector3.Distance(puntos[i], puntos[j]);
                if (d > maxArista) continue;

                NavMeshHit bloqueo;
                bool conectado = !NavMesh.Raycast(puntos[i], puntos[j], out bloqueo, NavMesh.AllAreas);
                if (!conectado && NavMesh.CalculatePath(puntos[i], puntos[j], NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    float largo = 0f;
                    for (int k = 1; k < path.corners.Length; k++) largo += Vector3.Distance(path.corners[k - 1], path.corners[k]);
                    conectado = largo <= d * 1.4f;
                }
                if (conectado)
                {
                    vecinos[i].Add(j);
                    vecinos[j].Add(i);
                }
            }
        }

        // 3) Quedarse con el componente conexo más grande (sin islas inalcanzables).
        int[] componente = new int[n];
        for (int i = 0; i < n; i++) componente[i] = -1;
        int mejorComp = -1, mejorTam = 0, comp = 0;
        for (int i = 0; i < n; i++)
        {
            if (componente[i] >= 0) continue;
            int tam = 0;
            Queue<int> cola = new Queue<int>();
            cola.Enqueue(i);
            componente[i] = comp;
            while (cola.Count > 0)
            {
                int a = cola.Dequeue();
                tam++;
                foreach (int b in vecinos[a]) if (componente[b] < 0) { componente[b] = comp; cola.Enqueue(b); }
            }
            if (tam > mejorTam) { mejorTam = tam; mejorComp = comp; }
            comp++;
        }

        // 4) Crear los objetos.
        GameObject raiz = GameObject.Find(NombreGrafo);
        if (raiz == null)
        {
            raiz = new GameObject(NombreGrafo);
            Undo.RegisterCreatedObjectUndo(raiz, "Crear grafo");
        }
        if (raiz.GetComponent<GrafoWaypoints>() == null) Undo.AddComponent<GrafoWaypoints>(raiz);
        for (int i = raiz.transform.childCount - 1; i >= 0; i--)
        {
            Transform hijo = raiz.transform.GetChild(i);
            if (hijo.GetComponent<WaypointNode>() != null) Undo.DestroyObjectImmediate(hijo.gameObject);
        }

        Dictionary<int, WaypointNode> nodos = new Dictionary<int, WaypointNode>();
        int contador = 0;
        for (int i = 0; i < n; i++)
        {
            if (componente[i] != mejorComp) continue;
            GameObject go = new GameObject("WP_" + contador.ToString("000"));
            go.transform.SetParent(raiz.transform, false);
            go.transform.position = puntos[i];
            Undo.RegisterCreatedObjectUndo(go, "Crear waypoint");
            nodos[i] = go.AddComponent<WaypointNode>();
            contador++;
        }

        int candidatos = 0;
        foreach (KeyValuePair<int, WaypointNode> par in nodos)
        {
            float alturaVecinos = 0f;
            foreach (int j in vecinos[par.Key])
            {
                if (!nodos.ContainsKey(j)) continue;
                par.Value.vecinos.Add(nodos[j]);
                alturaVecinos += puntos[j].y;
            }
            // Promontorio: más alto que la media de sus vecinos -> buen nido de mortero.
            int nv = par.Value.vecinos.Count;
            if (nv >= 2 && puntos[par.Key].y - alturaVecinos / nv >= 1f)
            {
                par.Value.candidatoNido = true;
                candidatos++;
            }
        }

        Debug.Log("IA táctica: grafo con " + nodos.Count + " waypoints (separación " + espacio.ToString("0.0") + " m, zona " +
                  (maxX - minX).ToString("0") + " x " + (maxZ - minZ).ToString("0") + " m), " +
                  candidatos + " promontorios marcados como nidos.", raiz);
        Selection.activeGameObject = raiz;
        return nodos.Count > 0;
    }

    // ------------------------------------------------------------------ 3 · Dron

    [MenuItem(Menu + "3 · Crear dron observador", false, 22)]
    public static void CrearDron()
    {
        GameObject dron = CrearDronInterno();
        Selection.activeGameObject = dron;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    private static GameObject CrearDronInterno()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefabDron);
        if (prefab == null) prefab = ConstruirPrefabDron();

        GameObject dron = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(dron, "Crear dron");

        // Cerca del primer mortero (o del centro de la vista Scene), en el aire.
        Vector3 pos = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
        foreach (AIMob mob in Object.FindObjectsByType<AIMob>(FindObjectsSortMode.None))
        {
            if (mob.mode == AIMob.BehaviorMode.MORTAR) { pos = mob.transform.position + new Vector3(8f, 0f, 8f); break; }
        }
        RaycastHit hit;
        if (Physics.Raycast(pos + Vector3.up * 200f, Vector3.down, out hit, 500f, MascaraNavMesh(), QueryTriggerInteraction.Ignore)) pos = hit.point;
        dron.transform.position = pos + Vector3.up * 9f;
        return dron;
    }

    private static GameObject ConstruirPrefabDron()
    {
        int enemigo = LayerMask.NameToLayer("Enemigo");
        Material cuerpo = CrearMaterial("Mat_Dron", new Color(0.22f, 0.24f, 0.28f), Color.black, 0.7f);
        Material ojo = CrearMaterial("Mat_DronOjo", new Color(1f, 0.25f, 0.1f), new Color(2f, 0.4f, 0.1f), 0f);

        GameObject raiz = new GameObject("Dron_Observador");
        SphereCollider col = raiz.AddComponent<SphereCollider>();
        col.radius = 0.8f;

        Primitiva(PrimitiveType.Sphere, "Cuerpo", raiz.transform, Vector3.zero, new Vector3(1f, 0.45f, 1f), cuerpo);
        Primitiva(PrimitiveType.Cylinder, "Anillo", raiz.transform, Vector3.zero, new Vector3(1.9f, 0.03f, 1.9f), cuerpo);
        Primitiva(PrimitiveType.Sphere, "Ojo", raiz.transform, new Vector3(0f, -0.05f, 0.45f), Vector3.one * 0.28f, ojo);

        GameObject focoGo = new GameObject("Foco");
        focoGo.transform.SetParent(raiz.transform, false);
        focoGo.transform.localPosition = new Vector3(0f, -0.1f, 0.5f);
        Light foco = focoGo.AddComponent<Light>();
        foco.type = LightType.Spot;
        foco.range = 40f;
        foco.spotAngle = 70f;
        foco.intensity = 25f;
        foco.shadows = LightShadows.None;

        DronObservador d = raiz.AddComponent<DronObservador>();
        d.foco = foco;
        d.obstaculos = 1 << Mathf.Max(0, LayerMask.NameToLayer("Suelo"));
        d.materialLaser = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterialAviso);

        if (enemigo >= 0) foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = enemigo;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(raiz, RutaPrefabDron);
        Object.DestroyImmediate(raiz);
        Debug.Log("IA táctica: prefab del dron creado en " + RutaPrefabDron + " (sirve para los demás planetas).", prefab);
        return prefab;
    }

    private static void Primitiva(PrimitiveType tipo, string nombre, Transform padre, Vector3 pos, Vector3 escala, Material m)
    {
        GameObject go = GameObject.CreatePrimitive(tipo);
        go.name = nombre;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(padre, false);
        go.transform.localPosition = pos;
        go.transform.localScale = escala;
        go.GetComponent<Renderer>().sharedMaterial = m;
    }

    private static Material CrearMaterial(string nombre, Color color, Color emision, float metalico)
    {
        if (!AssetDatabase.IsValidFolder(CarpetaMateriales)) AssetDatabase.CreateFolder("Assets/Materiales", "IA");
        string ruta = CarpetaMateriales + "/" + nombre + ".mat";
        Material existente = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (existente != null) return existente;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material m = new Material(shader);
        m.color = color;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metalico);
        if (emision.maxColorComponent > 0f && m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emision);
        }
        AssetDatabase.CreateAsset(m, ruta);
        return m;
    }

    // ------------------------------------------------------------------ 4 · Fuego amigo

    [MenuItem(Menu + "4 · Activar fuego amigo en el proyectil", false, 23)]
    public static void ActivarFuegoAmigo()
    {
        int n = ActivarFuegoAmigoInterno();
        EditorUtility.DisplayDialog("Fuego amigo", n > 0
            ? "Listo: las explosiones del mortero ahora también dañan a la capa Enemigo (" + n + " prefab(s))."
            : "No se encontró el prefab del proyectil (" + RutaProyectil + ").", "OK");
    }

    private static int ActivarFuegoAmigoInterno()
    {
        int enemigo = LayerMask.NameToLayer("Enemigo");
        if (enemigo < 0) { Debug.LogError("IA táctica: falta la capa 'Enemigo'."); return 0; }

        HashSet<MortarProjectile> prefabs = new HashSet<MortarProjectile>();
        GameObject porRuta = AssetDatabase.LoadAssetAtPath<GameObject>(RutaProyectil);
        if (porRuta != null && porRuta.GetComponent<MortarProjectile>() != null) prefabs.Add(porRuta.GetComponent<MortarProjectile>());
        foreach (AIMob mob in Object.FindObjectsByType<AIMob>(FindObjectsSortMode.None))
        {
            if (mob.projectilePrefab != null && EditorUtility.IsPersistent(mob.projectilePrefab)) prefabs.Add(mob.projectilePrefab);
        }

        foreach (MortarProjectile p in prefabs)
        {
            SerializedObject so = new SerializedObject(p);
            SerializedProperty dano = so.FindProperty("damageMask");
            dano.intValue |= 1 << enemigo;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(p);
        }
        AssetDatabase.SaveAssets();
        if (prefabs.Count > 0) Debug.Log("IA táctica: fuego amigo activado en " + prefabs.Count + " prefab(s) de proyectil.");
        return prefabs.Count;
    }

    // ------------------------------------------------------------------ 5 · Granadas

    [MenuItem(Menu + "5 · Dar granadas al jugador", false, 24)]
    public static void DarGranadas()
    {
        if (DarGranadasInterno()) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    private static bool DarGranadasInterno()
    {
        GameObject jugador = GameObject.FindGameObjectWithTag("Player");
        if (jugador == null)
        {
            Debug.LogError("IA táctica: no hay ningún objeto activo con el tag Player.");
            return false;
        }

        LanzadorGranadas lanzador = jugador.GetComponent<LanzadorGranadas>();
        if (lanzador == null) lanzador = Undo.AddComponent<LanzadorGranadas>(jugador);
        Undo.RecordObject(lanzador, "Configurar granadas");

        if (lanzador.proyectilPrefab == null)
        {
            foreach (AIMob mob in Object.FindObjectsByType<AIMob>(FindObjectsSortMode.None))
            {
                if (mob.projectilePrefab != null) { lanzador.proyectilPrefab = mob.projectilePrefab; break; }
            }
            if (lanzador.proyectilPrefab == null)
            {
                GameObject p = AssetDatabase.LoadAssetAtPath<GameObject>(RutaProyectil);
                if (p != null) lanzador.proyectilPrefab = p.GetComponent<MortarProjectile>();
            }
        }

        int suelo = LayerMask.NameToLayer("Suelo");
        int enemigo = LayerMask.NameToLayer("Enemigo");
        if (suelo >= 0) lanzador.capasSuelo = 1 << suelo;
        if (enemigo >= 0) lanzador.capasEnemigo = 1 << enemigo;
        if (lanzador.materialLinea == null) lanzador.materialLinea = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterialAviso);

        EditorUtility.SetDirty(lanzador);
        PrefabUtility.RecordPrefabInstancePropertyModifications(lanzador);
        Debug.Log("IA táctica: granadas listas en '" + jugador.name + "' (clic derecho o G).", jugador);
        return true;
    }

    // ------------------------------------------------------------------ 6 · Morteros y duendes

    [MenuItem(Menu + "6 · Configurar morteros (nidos tácticos)", false, 25)]
    public static void ConfigurarMorteros()
    {
        int n = ConfigurarMorterosInterno();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Morteros", n + " mortero(s) configurado(s).", "OK");
    }

    private static int ConfigurarMorterosInterno()
    {
        int suelo = LayerMask.NameToLayer("Suelo");
        int n = 0;
        foreach (AIMob mob in Object.FindObjectsByType<AIMob>(FindObjectsSortMode.None))
        {
            if (mob.mode != AIMob.BehaviorMode.MORTAR) continue;
            Undo.RecordObject(mob, "Configurar mortero");
            mob.nestPattern = AIMob.NestPattern.TACTICO;
            mob.requiereVision = true;
            mob.usarPrediccionPorRuta = true;
            mob.comprobarTrayectoria = true;
            mob.patronAndanada = AIMob.PatronAndanada.ADAPTATIVO;
            if (suelo >= 0 && mob.groundMask.value == 0) mob.groundMask = 1 << suelo;
            if (mob.saludMaxima < 50f) mob.saludMaxima = 75f; // aguanta 2 granadas
            EditorUtility.SetDirty(mob);
            PrefabUtility.RecordPrefabInstancePropertyModifications(mob);
            n++;
        }
        return n;
    }

    [MenuItem(Menu + "Convertir duendes seleccionados a TACTICO", false, 40)]
    public static void ConvertirSeleccionados()
    {
        int n = 0;
        foreach (GameObject go in Selection.gameObjects)
        {
            AIMob mob = go.GetComponent<AIMob>();
            if (mob == null || mob.mode == AIMob.BehaviorMode.MORTAR) continue;
            ConvertirATactico(mob);
            n++;
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Duendes tácticos", n == 0
            ? "Selecciona en la Hierarchy uno o varios Goblins (con AIMob) y vuelve a usar este menú."
            : n + " duende(s) convertidos a TACTICO.", "OK");
    }

    [MenuItem(Menu + "Convertir todos los enemigos *_SEEKING a TACTICO", false, 41)]
    public static void ConvertirSeeking()
    {
        int n = 0;
        foreach (AIMob mob in Object.FindObjectsByType<AIMob>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mob.mode == AIMob.BehaviorMode.MORTAR || !mob.name.Contains("_SEEKING")) continue;
            ConvertirATactico(mob);
            n++;
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Duendes tácticos", n + " duende(s) convertidos a TACTICO. Los Goblin_WANDERING se dejan como estaban.", "OK");
    }

    private static void ConvertirATactico(AIMob mob)
    {
        int suelo = LayerMask.NameToLayer("Suelo");
        Undo.RecordObject(mob, "Convertir a TACTICO");
        mob.mode = AIMob.BehaviorMode.TACTICO;
        if (suelo >= 0 && mob.groundMask.value == 0) mob.groundMask = 1 << suelo;
        EditorUtility.SetDirty(mob);
        PrefabUtility.RecordPrefabInstancePropertyModifications(mob);

        // Capa Enemigo (con hijos): así le afectan las granadas y el fuego amigo del mortero.
        int enemigo = LayerMask.NameToLayer("Enemigo");
        if (enemigo >= 0)
        {
            foreach (Transform t in mob.GetComponentsInChildren<Transform>(true))
            {
                Undo.RecordObject(t.gameObject, "Capa Enemigo");
                t.gameObject.layer = enemigo;
            }
        }
    }

    // ------------------------------------------------------------------ Validación

    [MenuItem(Menu + "Validar escena", false, 60)]
    public static void ValidarEscena()
    {
        string informe = Validar(out int errores);
        Debug.Log("IA táctica – validación:\n" + informe);
        EditorUtility.DisplayDialog("IA táctica: validación", (errores == 0 ? "Todo en orden.\n\n" : errores + " problema(s):\n\n") + informe, "OK");
    }

    private static string Validar(out int errores)
    {
        StringBuilder sb = new StringBuilder();
        int fallos = 0;

        void Item(bool ok, string texto)
        {
            sb.AppendLine((ok ? "✔ " : "✖ ") + texto);
            if (!ok) fallos++;
        }

        int suelo = LayerMask.NameToLayer("Suelo");
        int enemigo = LayerMask.NameToLayer("Enemigo");
        Item(suelo >= 0 && enemigo >= 0 && LayerMask.NameToLayer("Jugador") >= 0, "Capas Suelo, Jugador y Enemigo");

        GameObject jugador = GameObject.FindGameObjectWithTag("Player");
        Item(jugador != null, "Jugador con tag Player" + (jugador != null ? " (" + jugador.name + ")" : ""));

        bool sueloBajoJugador = jugador != null && suelo >= 0 && Physics.Raycast(jugador.transform.position + Vector3.up * 50f,
            Vector3.down, 200f, 1 << suelo, QueryTriggerInteraction.Ignore);
        Item(sueloBajoJugador, "Suelo con collider en la capa Suelo bajo el jugador");

        Item(NavMesh.CalculateTriangulation().vertices.Length > 0, "NavMesh horneado (paso 1)");

        GrafoWaypoints grafo = Object.FindFirstObjectByType<GrafoWaypoints>();
        int nodos = grafo != null ? grafo.GetComponentsInChildren<WaypointNode>().Length : 0;
        Item(nodos >= 8, "Grafo de waypoints: " + nodos + " nodos (paso 2)");

        Item(Object.FindFirstObjectByType<ConfiguracionArtilleria>() != null, "ConfiguracionArtilleria en la escena (perfil del planeta)");

        int morteros = 0, tacticos = 0;
        foreach (AIMob mob in Object.FindObjectsByType<AIMob>(FindObjectsSortMode.None))
        {
            if (mob.mode == AIMob.BehaviorMode.MORTAR)
            {
                morteros++;
                bool ok = mob.cannonPivot != null && mob.firePoint != null && mob.projectilePrefab != null && mob.groundMask.value != 0;
                Item(ok, "Mortero '" + mob.name + "': cañón, proyectil y Ground Mask" + (ok ? "" : " (revisa la guía 04)"));
                if (mob.projectilePrefab != null && enemigo >= 0)
                {
                    SerializedObject so = new SerializedObject(mob.projectilePrefab);
                    bool fuegoAmigo = (so.FindProperty("damageMask").intValue & (1 << enemigo)) != 0;
                    Item(fuegoAmigo, "Fuego amigo en el proyectil de '" + mob.name + "' (paso 4)");
                }
            }
            else if (mob.mode == AIMob.BehaviorMode.TACTICO)
            {
                tacticos++;
                if (mob.groundMask.value == 0) Item(false, "Duende '" + mob.name + "': Ground Mask vacío (verá a través de las colinas)");
                if (mob.gameObject.layer != enemigo) Item(false, "Duende '" + mob.name + "': ponlo en la capa Enemigo (si no, granadas y fuego amigo no le afectan)");
            }
        }
        sb.AppendLine("• Morteros: " + morteros + "   • Duendes tácticos: " + tacticos);

        int drones = Object.FindObjectsByType<DronObservador>(FindObjectsSortMode.None).Length;
        Item(drones > 0, "Dron observador: " + drones + " (paso 3)");

        LanzadorGranadas lanzador = jugador != null ? jugador.GetComponent<LanzadorGranadas>() : null;
        Item(lanzador != null && lanzador.proyectilPrefab != null && lanzador.capasEnemigo.value != 0, "Granadas del jugador (paso 5)");
        Item(jugador != null && jugador.GetComponent<SaludJugador>() != null, "SaludJugador en el jugador (para la barra de vida)");
        Item(Object.FindFirstObjectByType<HUDTactico>() != null, "HUD táctico (paso 7)");

        SonidosJuego sonidos = AssetDatabase.LoadAssetAtPath<SonidosJuego>("Assets/Resources/SonidosJuego.asset");
        sb.AppendLine("• Sonidos asignados: " + (sonidos != null ? sonidos.Asignados + " de " + SonidosJuego.TotalEspacios + " (Solar Adventure > Sonido)" : "falta el archivo de ajustes (Solar Adventure > Sonido > Abrir ajustes de sonido)"));

        if (tacticos == 0) sb.AppendLine("ℹ Opcional: convierte algunos Goblins a TACTICO (menú 'Convertir...').");
        errores = fallos;
        return sb.ToString();
    }

    // ------------------------------------------------------------------ Utilidades

    private static bool EscenaGuardada()
    {
        Scene escena = SceneManager.GetActiveScene();
        if (!string.IsNullOrEmpty(escena.path)) return true;
        EditorUtility.DisplayDialog("IA táctica", "Guarda la escena primero (File > Save).", "OK");
        return false;
    }

    /// <summary>
    /// Si la escena tiene una jaula (objetos con tag LimiteMundo), el área jugable es la caja que la envuelve.
    /// En Y llega 30 m por debajo y se queda 10 m por debajo del techo, para no hornear el techo como si fuera suelo.
    /// Devuelve null si no hay jaula o sus medidas no son razonables (entre 160 m y 5 km por lado).
    /// </summary>
    private static Bounds? ZonaDeLaJaula()
    {
        GameObject[] limites = GameObject.FindGameObjectsWithTag("LimiteMundo");
        bool hay = false;
        Bounds b = new Bounds();
        foreach (GameObject go in limites)
        {
            foreach (Collider c in go.GetComponentsInChildren<Collider>())
            {
                if (!hay) { b = c.bounds; hay = true; }
                else b.Encapsulate(c.bounds);
            }
        }
        if (!hay) return null;
        if (b.size.x < 160f || b.size.z < 160f || b.size.x > 5000f || b.size.z > 5000f) return null;
        if (!VolumenCubreElJuego(b)) return null; // una jaula que no contiene al jugador y las monedas no es el área jugable

        float minY = b.min.y - 30f;
        float maxY = b.max.y - 10f;
        if (maxY - minY < 20f) maxY = b.max.y + 30f;
        Bounds r = new Bounds();
        r.SetMinMax(new Vector3(b.min.x, minY, b.min.z), new Vector3(b.max.x, maxY, b.max.z));
        return r;
    }

    /// <summary>
    /// Zona de juego: caja que abarca jugador, monedas, zona de extracción, enemigos y nidos, más un margen.
    /// Evita hornear un terreno gigante entero (tardaría mucho y no hace falta).
    /// </summary>
    private static Bounds ZonaDeJuego(float margen)
    {
        Bounds? jaula = ZonaDeLaJaula();
        if (jaula.HasValue) return jaula.Value;

        List<Vector3> puntos = new List<Vector3>();
        GameObject jugador = GameObject.FindGameObjectWithTag("Player");
        if (jugador != null) puntos.Add(jugador.transform.position);
        foreach (Moneda m in Object.FindObjectsByType<Moneda>(FindObjectsSortMode.None)) puntos.Add(m.transform.position);
        foreach (ZonaExtraccion z in Object.FindObjectsByType<ZonaExtraccion>(FindObjectsSortMode.None)) puntos.Add(z.transform.position);
        foreach (AIMob mob in Object.FindObjectsByType<AIMob>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            puntos.Add(mob.transform.position);
            if (mob.firingNests != null) foreach (Transform t in mob.firingNests) if (t != null) puntos.Add(t.position);
        }

        // Mercurio y otros planetas se arman con varios terrenos en cuadrícula: se usa la unión de TODOS
        // (Terrain.activeTerrain es uno solo y no tiene por qué ser donde está la acción).
        bool hayTerreno = false;
        Bounds terrenos = new Bounds();
        foreach (Terrain t in Terrain.activeTerrains)
        {
            Bounds tb = new Bounds(t.GetPosition() + t.terrainData.size * 0.5f, t.terrainData.size);
            if (!hayTerreno) { terrenos = tb; hayTerreno = true; }
            else terrenos.Encapsulate(tb);
        }
        if (puntos.Count == 0)
        {
            if (hayTerreno) return terrenos;
            return new Bounds(Vector3.zero, new Vector3(200f, 200f, 200f));
        }

        Bounds b = new Bounds(puntos[0], Vector3.zero);
        foreach (Vector3 p in puntos) b.Encapsulate(p);
        b.Expand(new Vector3(margen * 2f, 0f, margen * 2f));
        // Mínimo 160 x 160 m: en planetas con pocos objetos queda sitio para morteros a 30–90 m del jugador.
        b.Expand(new Vector3(Mathf.Max(0f, 160f - b.size.x), 0f, Mathf.Max(0f, 160f - b.size.z)));

        float minY = b.min.y - 60f, maxY = b.max.y + 60f;
        if (hayTerreno)
        {
            minY = Mathf.Min(minY, terrenos.min.y - 5f);
            maxY = Mathf.Max(maxY, terrenos.max.y + 5f);
            // No salir del conjunto de terrenos
            float minX = Mathf.Max(b.min.x, terrenos.min.x), maxX = Mathf.Min(b.max.x, terrenos.max.x);
            float minZ = Mathf.Max(b.min.z, terrenos.min.z), maxZ = Mathf.Min(b.max.z, terrenos.max.z);
            if (maxX > minX && maxZ > minZ)
            {
                b.SetMinMax(new Vector3(minX, b.min.y, minZ), new Vector3(maxX, b.max.y, maxZ));
            }
        }
        b.SetMinMax(new Vector3(b.min.x, minY, b.min.z), new Vector3(b.max.x, maxY, b.max.z));
        return b;
    }
}
