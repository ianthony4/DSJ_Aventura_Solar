using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Menú  Solar Adventure > Enemigos  : cambia los modelos de los enemigos sin tocar su IA.
///
///   Enemigo      Stocklancer/ArachnidAlienHead_Lite  ->  Assets/Prefabs/Aracnido.prefab
///                (modelo con esqueleto + Animator Controller creado aquí + AIMob + AnimadorEnemigo)
///   Mortero      Autarca/AlienShipsPack "mortero Variant" (una nave)  ->  Assets/Prefabs/Mortero_Nave.prefab
///                (nave flotando + cañón con pivote + FirePoint + AIMob en modo MORTAR)
///
///   1 · Crear prefabs        construye los dos prefabs (se puede repetir: los reconstruye)
///   2 · Reemplazar en escena sustituye los duendes y los morteros de la escena abierta, conservando posición,
///                            modo de IA y ajustes de cada uno (se puede deshacer con Ctrl+Z)
/// El tamaño, la orientación, el CharacterController y la posición del cañón se calculan solos a partir del modelo.
/// </summary>
public static class ReemplazoEnemigos
{
    private const string Menu = "Solar Adventure/Enemigos/";

    // --- Modelos de entrada
    public const string RutaModeloAracnido = "Assets/Stocklancer/ArachnidAlienHead_Lite/Prefabs/SKM_ArachnidAlienHead_Lite.prefab";
    private const string CarpetaAnimaciones = "Assets/Stocklancer/ArachnidAlienHead_Lite/Animation/";
    private const string ClipCaminar = "AS_ArachnicAlienHead_WalkFW_Lite.fbx";
    private const string ClipAtacar = "AS_ArachnicAlienHead_Attack4_Lite.fbx";
    private const string ClipGolpe = "AS_ArachnicAlienHead_TakeHit_Lite.fbx";
    private const string ClipMorir = "AS_ArachnicAlienHead_Dead_Lite.fbx";
    public const string RutaModeloMortero = "Assets/Autarca/AlienShipsPack/Prefabs/mortero Variant.prefab";
    private const string RutaMaterialCanon = "Assets/Autarca/AlienShipsPack/Prefabs/Negro.mat";

    // --- Resultados
    public const string RutaPrefabAracnido = "Assets/Prefabs/Aracnido.prefab";
    public const string RutaPrefabNave = "Assets/Prefabs/Mortero_Nave.prefab";
    private const string RutaControlador = "Assets/Prefabs/Aracnido_Animator.controller";

    // --- Para copiar la configuración del mortero anterior
    private const string RutaMorteroAnterior = "Assets/Prefabs/Mortero.prefab";
    private const string RutaProyectil = "Assets/Prefabs/Proyectil_Mortero.prefab";
    private const string RutaIndicador = "Assets/Prefabs/Indicador_Impacto.prefab";

    // --- Medidas (metros)
    private const float TamanoAracnido = 2.5f;      // largo máximo del arácnido
    private const float TamanoNaveMin = 3f;          // si la nave mide menos o más que esto, se reescala a TamanoNaveBase
    private const float TamanoNaveMax = 12f;
    private const float TamanoNaveBase = 6f;
    private const float AlturaFlotando = 0.6f;       // la nave flota sobre el suelo

    // Campos que NO se copian al reemplazar: dependen del modelo nuevo.
    private static readonly HashSet<string> NoCopiar = new HashSet<string>
    {
        "m_Script", "yawModelo", "girarAlMorir", "cannonPivot", "firePoint", "bodyYawOffset"
    };

    // =====================================================================================
    //  Menús
    // =====================================================================================

    [MenuItem(Menu + "1 · Crear prefabs de los enemigos nuevos", false, 0)]
    public static void CrearPrefabs()
    {
        GameObject a = CrearPrefabAracnido();
        GameObject n = CrearPrefabNave();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string r = (a != null ? "✔ " + RutaPrefabAracnido + "\n" : "✖ No se pudo crear el arácnido (mira la Console).\n") +
                   (n != null ? "✔ " + RutaPrefabNave + "\n" : "✖ No se pudo crear el mortero-nave (mira la Console).\n");
        EditorUtility.DisplayDialog("Enemigos nuevos", r + "\nSiguiente paso: abre una escena y usa 2 · Reemplazar en la escena abierta.", "OK");
        if (a != null) EditorGUIUtility.PingObject(a);
    }

    [MenuItem(Menu + "2 · Reemplazar duendes y morteros de la escena abierta", false, 1)]
    public static void ReemplazarTodo()
    {
        Scene escena = SceneManager.GetActiveScene();
        List<AIMob> duendes, morteros;
        int duplicados;
        BuscarAReemplazar(out duendes, out morteros, out duplicados);

        if (duendes.Count == 0 && morteros.Count == 0)
        {
            EditorUtility.DisplayDialog("Enemigos nuevos", "No hay duendes ni morteros viejos por reemplazar en '" + escena.name + "'.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Reemplazar enemigos",
            "En '" + escena.name + "' se reemplazarán:\n\n• " + duendes.Count + " duende(s) por arácnidos\n• " + morteros.Count +
            " mortero(s) por la nave\n\nSe conservan posición, modo de IA y ajustes de cada uno. Se pueden deshacer con Ctrl+Z." +
            (duplicados > 0 ? "\n\nNota: " + duplicados + " AIMob duplicado(s) en objetos hijos (BAG.2) se descartan; los modelos nuevos no los tienen." : ""),
            "Reemplazar", "Cancelar")) return;

        GameObject prefabAracnido = duendes.Count > 0 ? ObtenerPrefab(RutaPrefabAracnido, CrearPrefabAracnido) : null;
        GameObject prefabNave = morteros.Count > 0 ? ObtenerPrefab(RutaPrefabNave, CrearPrefabNave) : null;

        int nd = prefabAracnido != null ? Reemplazar(duendes, prefabAracnido, true) : 0;
        int nm = prefabNave != null ? Reemplazar(morteros, prefabNave, false) : 0;

        EditorSceneManager.MarkSceneDirty(escena);
        EditorUtility.DisplayDialog("Enemigos nuevos",
            nd + " duende(s) y " + nm + " mortero(s) reemplazados.\n\nGuarda la escena con Ctrl+S.\n" +
            "Si hiciste el NavMesh y el grafo, no hace falta repetirlos.", "OK");
    }

    // =====================================================================================
    //  Reemplazo en escena
    // =====================================================================================

    // Cada enemigo se cuenta una sola vez, con el AIMob de su objeto raíz. Los duendes y morteros antiguos traían
    // un segundo AIMob en un hijo (BAG.2, modo COMBINED) que no vale como configuración: se descarta.
    private static void BuscarAReemplazar(out List<AIMob> duendes, out List<AIMob> morteros, out int duplicados)
    {
        duendes = new List<AIMob>();
        morteros = new List<AIMob>();
        duplicados = 0;

        Dictionary<GameObject, AIMob> porRaiz = new Dictionary<GameObject, AIMob>();
        foreach (AIMob mob in Object.FindObjectsByType<AIMob>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mob.GetComponentInParent<AnimadorEnemigo>() != null) continue;           // ya es un enemigo nuevo
            GameObject raiz = PrefabUtility.GetOutermostPrefabInstanceRoot(mob.gameObject);
            if (raiz == null) raiz = mob.gameObject;
            if (raiz.name.StartsWith("Mortero_Nave")) continue;

            AIMob existente;
            if (!porRaiz.TryGetValue(raiz, out existente)) { porRaiz[raiz] = mob; continue; }
            duplicados++;
            if (mob.gameObject == raiz) porRaiz[raiz] = mob;                              // el del objeto raíz manda
        }

        foreach (KeyValuePair<GameObject, AIMob> par in porRaiz)
        {
            if (par.Value.mode == AIMob.BehaviorMode.MORTAR) morteros.Add(par.Value);
            else duendes.Add(par.Value);
        }
    }

    private static GameObject ObtenerPrefab(string ruta, System.Func<GameObject> crear)
    {
        GameObject p = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        return p != null ? p : crear();
    }

    private static int Reemplazar(List<AIMob> viejos, GameObject prefab, bool sonDuendes)
    {
        int n = 0;
        foreach (AIMob viejo in viejos)
        {
            if (viejo == null) continue;

            Transform t = viejo.transform;
            GameObject raizVieja = PrefabUtility.GetOutermostPrefabInstanceRoot(viejo.gameObject);
            if (raizVieja == null) raizVieja = viejo.gameObject;

            GameObject nuevo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, viejo.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(nuevo, "Reemplazar enemigo");
            if (raizVieja.transform.parent != null) nuevo.transform.SetParent(raizVieja.transform.parent, true);
            nuevo.transform.SetPositionAndRotation(t.position, Quaternion.Euler(0f, t.eulerAngles.y, 0f));
            nuevo.name = sonDuendes && raizVieja.name.Contains("Goblin") ? raizVieja.name.Replace("Goblin", "Aracnido") : raizVieja.name;

            AIMob nuevoMob = nuevo.GetComponent<AIMob>();
            CopiarConfiguracion(viejo, nuevoMob, true);
            EditorUtility.SetDirty(nuevoMob);

            Undo.DestroyObjectImmediate(raizVieja);
            n++;
        }
        return n;
    }

    // Copia los ajustes de IA del enemigo viejo al nuevo (modo, velocidades, rangos, nidos...).
    // Las referencias vacías del viejo no pisan las del prefab nuevo, y los campos del modelo se respetan.
    private static void CopiarConfiguracion(AIMob origen, AIMob destino, bool copiarNidos)
    {
        SerializedObject so = new SerializedObject(origen);
        SerializedObject sd = new SerializedObject(destino);
        SerializedProperty it = so.GetIterator();
        bool entrar = true;
        while (it.NextVisible(entrar))
        {
            entrar = false;
            if (NoCopiar.Contains(it.name)) continue;
            if (it.name == "firingNests" && !copiarNidos) continue;
            if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue == null) continue;
            sd.CopyFromSerializedProperty(it);
        }
        sd.ApplyModifiedProperties();
    }

    // =====================================================================================
    //  Prefab del arácnido
    // =====================================================================================

    public static GameObject CrearPrefabAracnido()
    {
        GameObject modelo = AssetDatabase.LoadAssetAtPath<GameObject>(RutaModeloAracnido);
        if (modelo == null)
        {
            Debug.LogError("Enemigos: no encuentro el modelo en " + RutaModeloAracnido);
            return null;
        }

        AnimatorController controlador = CrearControlador();
        if (controlador == null) return null;

        int enemigo = LayerMask.NameToLayer("Enemigo");
        int suelo = LayerMask.NameToLayer("Suelo");

        GameObject raiz = new GameObject("Aracnido");
        CharacterController cc = raiz.AddComponent<CharacterController>();

        GameObject hijo = (GameObject)PrefabUtility.InstantiatePrefab(modelo, raiz.transform);
        hijo.name = "Modelo";
        hijo.transform.localPosition = Vector3.zero;
        hijo.transform.localRotation = Quaternion.identity;
        hijo.transform.localScale = Vector3.one;

        // Tamaño: el largo máximo queda en TamanoAracnido metros.
        Bounds b = BoundsDe(hijo);
        float largo = Mathf.Max(b.size.x, b.size.z);
        float escala = TamanoAracnido / Mathf.Max(0.0001f, largo);
        hijo.transform.localScale = Vector3.one * escala;
        b = BoundsDe(hijo);

        // Apoyado en el suelo y centrado sobre el pivote.
        hijo.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);
        b = BoundsDe(hijo);

        // Orientación: hacia donde apuntan los ojos y la lengua respecto al centro de las patas.
        float yaw = CalcularYawArania(hijo);
        bool yawOk = !float.IsNaN(yaw);
        if (!yawOk) yaw = 0f;

        // Animator
        Animator anim = hijo.GetComponent<Animator>();
        if (anim == null) anim = hijo.AddComponent<Animator>();
        anim.runtimeAnimatorController = controlador;
        anim.applyRootMotion = false;

        // CharacterController a la medida del modelo
        float radio = Mathf.Clamp(Mathf.Min(b.size.x, b.size.z) * 0.35f, 0.35f, 1.3f);
        float altura = Mathf.Max(b.size.y, radio * 2f + 0.1f);
        cc.radius = radio;
        cc.height = altura;
        cc.center = new Vector3(0f, altura * 0.5f, 0f);
        cc.slopeLimit = 45f;

        // IA
        AIMob mob = raiz.AddComponent<AIMob>();
        mob.mode = AIMob.BehaviorMode.TACTICO;
        mob.maxSpeed = 8f;
        mob.maxForce = 15f;
        mob.slowRadius = 5f;
        mob.yawModelo = Mathf.Round(yaw * 10f) / 10f;
        mob.girarAlMorir = false;
        mob.saludMaxima = 25f;
        mob.alturaOjos = Mathf.Clamp(b.size.y * 0.8f, 0.5f, 1.5f);
        if (suelo >= 0) mob.groundMask = 1 << suelo;
        raiz.AddComponent<AnimadorEnemigo>().animator = anim;

        if (enemigo >= 0) PonerCapa(raiz, enemigo);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(raiz, RutaPrefabAracnido);
        Object.DestroyImmediate(raiz);

        Debug.Log("Enemigos: prefab del arácnido creado en " + RutaPrefabAracnido + " (tamaño " + b.size.x.ToString("0.0") + " x " +
                  b.size.y.ToString("0.0") + " x " + b.size.z.ToString("0.0") + " m, giro del modelo " + mob.yawModelo + "°" +
                  (yawOk ? "" : " — NO se pudo calcular la orientación: si camina de espaldas, cambia 'Yaw Modelo' en el AI Mob") + ").", prefab);
        return prefab;
    }

    // Animator Controller: Caminar (con la velocidad del ciclo según lo rápido que va), Atacar, Golpe y Morir.
    private static AnimatorController CrearControlador()
    {
        AnimationClip caminar = CargarClip(ClipCaminar);
        AnimationClip atacar = CargarClip(ClipAtacar);
        AnimationClip golpe = CargarClip(ClipGolpe);
        AnimationClip morir = CargarClip(ClipMorir);
        if (caminar == null || atacar == null || golpe == null || morir == null) return null;

        if (AssetDatabase.LoadAssetAtPath<Object>(RutaControlador) != null) AssetDatabase.DeleteAsset(RutaControlador);
        AnimatorController c = AnimatorController.CreateAnimatorControllerAtPath(RutaControlador);
        c.AddParameter("VelocidadAnim", AnimatorControllerParameterType.Float);
        c.AddParameter("Atacar", AnimatorControllerParameterType.Trigger);
        c.AddParameter("Golpe", AnimatorControllerParameterType.Trigger);
        c.AddParameter("Morir", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine sm = c.layers[0].stateMachine;

        AnimatorState sCaminar = sm.AddState("Caminar");
        sCaminar.motion = caminar;
        sCaminar.speedParameterActive = true;      // parado (0) la pose queda congelada: hace de "idle"
        sCaminar.speedParameter = "VelocidadAnim";

        AnimatorState sAtacar = sm.AddState("Atacar");
        sAtacar.motion = atacar;
        AnimatorState sGolpe = sm.AddState("Golpe");
        sGolpe.motion = golpe;
        AnimatorState sMorir = sm.AddState("Morir");
        sMorir.motion = morir;
        sm.defaultState = sCaminar;

        DesdeCualquiera(sm, sAtacar, "Atacar");
        DesdeCualquiera(sm, sGolpe, "Golpe");
        DesdeCualquiera(sm, sMorir, "Morir");
        VolverACaminar(sAtacar, sCaminar);
        VolverACaminar(sGolpe, sCaminar);

        EditorUtility.SetDirty(c);
        AssetDatabase.SaveAssets();
        return c;
    }

    private static void DesdeCualquiera(AnimatorStateMachine sm, AnimatorState destino, string disparador)
    {
        AnimatorStateTransition t = sm.AddAnyStateTransition(destino);
        t.AddCondition(AnimatorConditionMode.If, 0f, disparador);
        t.hasExitTime = false;
        t.duration = 0.1f;
        t.canTransitionToSelf = false;
    }

    private static void VolverACaminar(AnimatorState desde, AnimatorState caminar)
    {
        AnimatorStateTransition t = desde.AddTransition(caminar);
        t.hasExitTime = true;
        t.exitTime = 1f;
        t.duration = 0.15f;
    }

    private static AnimationClip CargarClip(string archivo)
    {
        string ruta = CarpetaAnimaciones + archivo;
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(ruta))
        {
            AnimationClip clip = o as AnimationClip;
            if (clip != null && !clip.name.StartsWith("__preview__")) return clip;
        }
        Debug.LogError("Enemigos: no encuentro la animación " + ruta);
        return null;
    }

    // Dirección "hacia delante" del modelo: de las raíces de las patas hacia los ojos, la boca y la lengua.
    // Devuelve el giro (grados) que hace falta aplicarle para que mire hacia +Z, o NaN si no se puede calcular.
    private static float CalcularYawArania(GameObject modelo)
    {
        Vector3 frente = Vector3.zero, patas = Vector3.zero;
        int nf = 0, np = 0;
        foreach (Transform t in modelo.GetComponentsInChildren<Transform>())
        {
            string n = t.name.ToLowerInvariant();
            if (n.StartsWith("eye") || n.StartsWith("tongue") || n.Contains("jaw") || n.Contains("mouth") || n.Contains("teeth"))
            {
                frente += t.position;
                nf++;
            }
            else if (n.StartsWith("leg") && n.EndsWith("root"))
            {
                patas += t.position;
                np++;
            }
        }
        if (nf == 0 || np == 0) return float.NaN;

        Vector3 f = frente / nf - patas / np;
        f.y = 0f;
        if (f.magnitude < 0.001f) return float.NaN;
        return -Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
    }

    // =====================================================================================
    //  Prefab del mortero (nave)
    // =====================================================================================

    public static GameObject CrearPrefabNave()
    {
        GameObject modelo = AssetDatabase.LoadAssetAtPath<GameObject>(RutaModeloMortero);
        if (modelo == null)
        {
            Debug.LogError("Enemigos: no encuentro el modelo del mortero en " + RutaModeloMortero);
            return null;
        }

        int enemigo = LayerMask.NameToLayer("Enemigo");
        int suelo = LayerMask.NameToLayer("Suelo");

        GameObject raiz = new GameObject("Mortero_Nave");
        CharacterController cc = raiz.AddComponent<CharacterController>();

        GameObject hijo = (GameObject)PrefabUtility.InstantiatePrefab(modelo, raiz.transform);
        hijo.name = "Modelo";
        hijo.transform.localPosition = Vector3.zero;
        hijo.transform.localRotation = Quaternion.identity;

        // Respeta la escala de tu variante; solo se corrige si la nave queda absurdamente grande o pequeña.
        Bounds b = BoundsDe(hijo);
        float largo = Mathf.Max(b.size.x, b.size.z);
        if (largo < TamanoNaveMin || largo > TamanoNaveMax)
        {
            hijo.transform.localScale *= TamanoNaveBase / Mathf.Max(0.0001f, largo);
            b = BoundsDe(hijo);
        }

        // Flotando sobre el suelo y centrada sobre el pivote.
        hijo.transform.localPosition = new Vector3(-b.center.x, -b.min.y + AlturaFlotando, -b.center.z);
        b = BoundsDe(hijo);

        float yaw = CalcularYawNave(hijo);
        if (float.IsNaN(yaw)) yaw = 0f;

        // CharacterController: en el suelo, bajo la nave
        float radio = Mathf.Clamp(Mathf.Min(b.size.x, b.size.z) * 0.3f, 0.6f, 1.8f);
        float altura = Mathf.Max(2f, radio * 2f + 0.2f);
        cc.radius = radio;
        cc.height = altura;
        cc.center = new Vector3(0f, altura * 0.5f, 0f);
        cc.slopeLimit = 45f;

        // Cañón: pivote sobre el casco, tubo (cilindro) y punta de disparo.
        float L = Mathf.Max(b.size.x, b.size.z);
        float longitud = L * 0.45f;
        float grosor = L * 0.07f;

        GameObject pivote = new GameObject("CannonPivot");
        pivote.transform.SetParent(raiz.transform, false);
        pivote.transform.localPosition = new Vector3(0f, b.max.y, 0f);

        GameObject tubo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tubo.name = "CannonMesh";
        Object.DestroyImmediate(tubo.GetComponent<Collider>()); // un collider aquí estorbaría al movimiento
        tubo.transform.SetParent(pivote.transform, false);
        tubo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        tubo.transform.localScale = new Vector3(grosor, longitud * 0.5f, grosor);
        tubo.transform.localPosition = new Vector3(0f, 0f, longitud * 0.5f);
        Material negro = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterialCanon);
        if (negro != null) tubo.GetComponent<Renderer>().sharedMaterial = negro;

        GameObject punta = new GameObject("FirePoint");
        punta.transform.SetParent(pivote.transform, false);
        punta.transform.localPosition = new Vector3(0f, 0f, longitud);

        // IA: primero valores por defecto; después, si existe, la configuración del mortero anterior.
        AIMob mob = raiz.AddComponent<AIMob>();
        mob.mode = AIMob.BehaviorMode.MORTAR;
        mob.maxSpeed = 3f;
        mob.maxForce = 15f;
        mob.slowRadius = 4f;
        mob.nestPattern = AIMob.NestPattern.TACTICO;
        mob.requiereVision = true;
        mob.saludMaxima = 75f;
        if (suelo >= 0) mob.groundMask = 1 << suelo;

        GameObject proyectil = AssetDatabase.LoadAssetAtPath<GameObject>(RutaProyectil);
        if (proyectil != null) mob.projectilePrefab = proyectil.GetComponent<MortarProjectile>();
        GameObject indicador = AssetDatabase.LoadAssetAtPath<GameObject>(RutaIndicador);
        if (indicador != null) mob.indicatorPrefab = indicador.GetComponent<ImpactIndicator>();

        GameObject anterior = AssetDatabase.LoadAssetAtPath<GameObject>(RutaMorteroAnterior);
        AIMob mobAnterior = anterior != null ? anterior.GetComponent<AIMob>() : null;
        if (mobAnterior != null) CopiarConfiguracion(mobAnterior, mob, false);

        mob.cannonPivot = pivote.transform;
        mob.firePoint = punta.transform;
        mob.firingNests = new Transform[0];     // cada mortero usa los nidos que le asignes, o los nodos del grafo
        mob.bodyYawOffset = Mathf.Round(yaw * 10f) / 10f;
        mob.alturaOjos = Mathf.Clamp(b.max.y, 1f, 4f);

        if (enemigo >= 0) PonerCapa(raiz, enemigo);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(raiz, RutaPrefabNave);
        Object.DestroyImmediate(raiz);

        Debug.Log("Enemigos: prefab del mortero creado en " + RutaPrefabNave + " (nave de " + b.size.x.ToString("0.0") + " x " +
                  b.size.y.ToString("0.0") + " x " + b.size.z.ToString("0.0") + " m, flotando " + AlturaFlotando + " m" +
                  (mobAnterior != null ? ", ajustes copiados del mortero anterior" : ", con ajustes por defecto") +
                  "). Si el cuerpo gira hacia donde no debe al caminar, cambia 'Body Yaw Offset' (0, 90, 180 o 270).", prefab);
        return prefab;
    }

    // Nariz de la nave: el extremo más largo del casco que sea más estrecho. Devuelve el giro para que mire a +Z, o NaN.
    private static float CalcularYawNave(GameObject modelo)
    {
        MeshFilter mf = modelo.GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return float.NaN;

        Vector3[] v;
        try { v = mf.sharedMesh.vertices; }
        catch { return float.NaN; }
        if (v == null || v.Length < 8) return float.NaN;

        Matrix4x4 m = mf.transform.localToWorldMatrix;
        Vector3[] w = new Vector3[v.Length];
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        for (int i = 0; i < v.Length; i++)
        {
            w[i] = m.MultiplyPoint3x4(v[i]);
            minX = Mathf.Min(minX, w[i].x); maxX = Mathf.Max(maxX, w[i].x);
            minZ = Mathf.Min(minZ, w[i].z); maxZ = Mathf.Max(maxZ, w[i].z);
        }

        bool largoEnZ = (maxZ - minZ) >= (maxX - minX);
        float aMin = largoEnZ ? minZ : minX, aMax = largoEnZ ? maxZ : maxX;
        float corte = (aMax - aMin) * 0.2f;

        float bajoMin = float.MaxValue, bajoMax = float.MinValue, altoMin = float.MaxValue, altoMax = float.MinValue;
        for (int i = 0; i < w.Length; i++)
        {
            float eje = largoEnZ ? w[i].z : w[i].x;
            float perp = largoEnZ ? w[i].x : w[i].z;
            if (eje < aMin + corte) { bajoMin = Mathf.Min(bajoMin, perp); bajoMax = Mathf.Max(bajoMax, perp); }
            if (eje > aMax - corte) { altoMin = Mathf.Min(altoMin, perp); altoMax = Mathf.Max(altoMax, perp); }
        }

        // La nariz es el extremo más estrecho.
        bool narizEnElMaximo = (altoMax - altoMin) < (bajoMax - bajoMin);
        float signo = narizEnElMaximo ? 1f : -1f;
        Vector3 f = largoEnZ ? new Vector3(0f, 0f, signo) : new Vector3(signo, 0f, 0f);
        return -Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
    }

    // =====================================================================================
    //  Utilidades
    // =====================================================================================

    private static Bounds BoundsDe(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    private static void PonerCapa(GameObject raiz, int capa)
    {
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = capa;
    }
}
