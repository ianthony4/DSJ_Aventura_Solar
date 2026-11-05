using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Crea y abre el archivo con los sonidos del juego (Assets/Resources/SonidosJuego.asset) y rellena los espacios
/// con los clips de la carpeta Assets/Sounds, por nombre.
///   * El archivo se crea solo la primera vez que Unity compila los scripts, y en ese momento se asignan los sonidos.
///   * Menú  Solar Adventure > Sonido > Abrir ajustes de sonido          lo selecciona en el Inspector.
///   * Menú  Solar Adventure > Sonido > Asignar sonidos de la carpeta Sounds   rellena los espacios VACÍOS
///     (no pisa lo que ya hayas puesto tú). Se puede repetir cada vez que añadas o renombres archivos.
///   * Menú  Solar Adventure > Sonido > Cuántos sonidos faltan             resumen de qué espacios tienen sonido.
///
/// Nombres que reconoce (cualquier parte del nombre del archivo, sin importar mayúsculas):
///   fondo_mercurio ......... fondo del planeta Mercury (varios archivos = se elige uno al empezar)
///   pisadas_enemigos ....... pasos de los enemigos       pisadas_jugador ..... pisadas del astronauta
///   disparo_mortero ........ disparo del mortero Y disparo del jugador (mismo audio)
///   sonido_impacto ......... impacto del proyectil del mortero Y del disparo del jugador (mismo audio)
///   movimiento_mortero ..... motor del mortero
///   escaneo_dron ........... dron en patrulla            deteccion_dron ...... dron te detecta
/// Al asignar: los fondos largos pasan a "Streaming" (no se cargan enteros en memoria) y los sonidos 3D a mono.
/// </summary>
[InitializeOnLoad]
public static class AjustesSonido
{
    private const string Carpeta = "Assets/Resources";
    private const string Ruta = "Assets/Resources/SonidosJuego.asset";
    private const string CarpetaSonidos = "Assets/Sounds";
    private const float SegundosParaStreaming = 20f;
    private const int VersionAsignacion = 2;      // sube cuando se añaden espacios nuevos: se rellenan una vez con los de Sounds

    static AjustesSonido()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            SonidosJuego a = ObtenerOCrear(true);

            // Espacios nuevos (versión mayor): se rellenan UNA vez con los archivos de Sounds, solo los vacíos.
            if (a != null && a.versionAsignacion < VersionAsignacion)
            {
                int n = AsignarDeCarpeta(a);
                a.versionAsignacion = VersionAsignacion;
                EditorUtility.SetDirty(a);
                AssetDatabase.SaveAssets();
                if (n > 0) Debug.Log("Sonido: se rellenaron " + n + " espacio(s) nuevo(s) con los clips de " + CarpetaSonidos + ".", a);
            }
        };
    }

    [MenuItem("Solar Adventure/Sonido/Abrir ajustes de sonido", false, 0)]
    public static void Abrir()
    {
        SonidosJuego ajustes = ObtenerOCrear(false);
        if (ajustes == null) return;
        Selection.activeObject = ajustes;
        EditorGUIUtility.PingObject(ajustes);
    }

    [MenuItem("Solar Adventure/Sonido/Asignar sonidos de la carpeta Sounds", false, 1)]
    public static void AsignarDesdeMenu()
    {
        SonidosJuego ajustes = ObtenerOCrear(false);
        if (ajustes == null) return;
        int n = AsignarDeCarpeta(ajustes);
        Selection.activeObject = ajustes;
        EditorUtility.DisplayDialog("Sonidos del juego",
            n > 0 ? n + " espacio(s) rellenado(s) con los clips de " + CarpetaSonidos + ".\n\n" + ResumenTexto(ajustes)
                  : "No había espacios vacíos que coincidan con archivos de " + CarpetaSonidos + ".\n\n" + ResumenTexto(ajustes), "OK");
    }

    [MenuItem("Solar Adventure/Sonido/Cuántos sonidos faltan", false, 2)]
    public static void Resumen()
    {
        SonidosJuego a = ObtenerOCrear(false);
        if (a == null) return;
        EditorUtility.DisplayDialog("Sonidos del juego", ResumenTexto(a), "OK");
    }

    private static string ResumenTexto(SonidosJuego a)
    {
        string E(bool ok) { return ok ? "✔ " : "✖ "; }
        bool fondo = a.FondoPara("Mercury").HasClip;
        return E(fondo) + "1 · Fondo (Mercury o general)\n" +
               E(a.pasosEnemigo.HasClip) + "2 · Pasos de los enemigos\n" +
               E(a.disparoMortero.HasClip) + "3 · Disparo del mortero\n" +
               E(a.impactoMortero.HasClip) + "3 · Impacto del mortero\n" +
               E(a.movimientoMortero.HasClip) + "3 · Movimiento del mortero\n" +
               E(a.dronPatrulla.HasClip) + "4 · Dron en patrulla\n" +
               E(a.dronDetecta.HasClip) + "4 · Dron te detecta\n" +
               E(a.pasosJugador.HasClip) + "5 · Pisadas del jugador\n" +
               E(a.disparoJugador.HasClip) + "5 · Disparo del jugador\n" +
               E(a.impactoJugador.HasClip) + "5 · Impacto del jugador\n\n" +
               a.Asignados + " de " + SonidosJuego.TotalEspacios + " asignados. Los vacíos simplemente no suenan.";
    }

    /// <summary>Devuelve el archivo de ajustes; si no existe lo crea y le asigna los sonidos de la carpeta Sounds.</summary>
    public static SonidosJuego ObtenerOCrear(bool silencioso)
    {
        SonidosJuego existente = AssetDatabase.LoadAssetAtPath<SonidosJuego>(Ruta);
        if (existente != null) return existente;

        if (!AssetDatabase.IsValidFolder(Carpeta)) AssetDatabase.CreateFolder("Assets", "Resources");
        SonidosJuego nuevo = ScriptableObject.CreateInstance<SonidosJuego>();
        AssetDatabase.CreateAsset(nuevo, Ruta);
        int asignados = AsignarDeCarpeta(nuevo);
        nuevo.versionAsignacion = VersionAsignacion;
        EditorUtility.SetDirty(nuevo);
        AssetDatabase.SaveAssets();
        Debug.Log("Sonido: se creó " + Ruta + (asignados > 0 ? " y se asignaron " + asignados + " espacio(s) con los clips de " + CarpetaSonidos : "") +
                  ". Ajusta volúmenes en Solar Adventure > Sonido > Abrir ajustes de sonido.", nuevo);
        return nuevo;
    }

    // ------------------------------------------------------------------ Asignación por nombre

    /// <summary>Rellena los espacios vacíos con los clips de Assets/Sounds cuyo nombre coincide. Devuelve cuántos espacios llenó.</summary>
    public static int AsignarDeCarpeta(SonidosJuego a)
    {
        List<AudioClip> clips = new List<AudioClip>();
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { CarpetaSonidos }))
        {
            AudioClip c = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
            if (c != null) clips.Add(c);
        }

        Undo.RecordObject(a, "Asignar sonidos");
        int llenados = 0;
        List<AudioClip> usadosEn3D = new List<AudioClip>();
        List<AudioClip> usadosComoFondo = new List<AudioClip>();

        llenados += Llenar(a.pasosEnemigo, Buscar(clips, "pisadas_enemigos"), usadosEn3D);
        llenados += Llenar(a.disparoMortero, Buscar(clips, "disparo_mortero"), usadosEn3D);
        llenados += Llenar(a.movimientoMortero, Buscar(clips, "movimiento_mortero"), usadosEn3D);
        llenados += Llenar(a.impactoMortero, Buscar(clips, "sonido_impacto"), usadosEn3D);
        llenados += Llenar(a.impactoJugador, Buscar(clips, "sonido_impacto"), usadosEn3D);
        llenados += Llenar(a.dronPatrulla, Buscar(clips, "escaneo_dron"), usadosEn3D);
        llenados += Llenar(a.dronDetecta, Buscar(clips, "deteccion_dron"), usadosEn3D);
        llenados += Llenar(a.pasosJugador, Buscar(clips, "pisadas_jugador"), null);
        llenados += Llenar(a.disparoJugador, Buscar(clips, "disparo_mortero"), null);   // mismo audio que el mortero

        // Fondo de Mercury (si existen archivos "fondo_mercurio...")
        List<AudioClip> fondosMercurio = Buscar(clips, "fondo_mercurio");
        if (fondosMercurio.Count > 0)
        {
            SonidosJuego.FondoDePlaneta entrada = null;
            if (a.fondosPorPlaneta != null)
            {
                foreach (SonidosJuego.FondoDePlaneta f in a.fondosPorPlaneta)
                {
                    if (f != null && f.escena != null && f.escena.ToLowerInvariant().Contains("mercur")) entrada = f;
                }
            }
            if (entrada == null)
            {
                List<SonidosJuego.FondoDePlaneta> lista = new List<SonidosJuego.FondoDePlaneta>(a.fondosPorPlaneta ?? new SonidosJuego.FondoDePlaneta[0]);
                entrada = new SonidosJuego.FondoDePlaneta { escena = "Mercury" };
                lista.Add(entrada);
                a.fondosPorPlaneta = lista.ToArray();
            }
            llenados += Llenar(entrada.sonido, fondosMercurio, usadosComoFondo);
        }

        EditorUtility.SetDirty(a);
        AssetDatabase.SaveAssets();

        foreach (AudioClip c in usadosEn3D) AjustarImportacion(c, true, false);
        foreach (AudioClip c in usadosComoFondo) AjustarImportacion(c, false, true);
        return llenados;
    }

    private static List<AudioClip> Buscar(List<AudioClip> clips, string fragmento)
    {
        List<AudioClip> r = new List<AudioClip>();
        foreach (AudioClip c in clips)
        {
            if (c.name.ToLowerInvariant().Contains(fragmento)) r.Add(c);
        }
        r.Sort((x, y) => string.CompareOrdinal(x.name, y.name));
        return r;
    }

    // Llena un espacio solo si está vacío. Devuelve 1 si lo llenó.
    private static int Llenar(AudioCue cue, List<AudioClip> candidatos, List<AudioClip> registro)
    {
        if (cue == null || cue.HasClip || candidatos.Count == 0) return 0;
        cue.clips = candidatos.ToArray();
        if (registro != null) registro.AddRange(candidatos);
        return 1;
    }

    // Fondos largos: Streaming. Sonidos 3D: mono (en 3D el estéreo no aporta y ocupa el doble).
    private static void AjustarImportacion(AudioClip clip, bool mono, bool streamingSiLargo)
    {
        string ruta = AssetDatabase.GetAssetPath(clip);
        AudioImporter importador = AssetImporter.GetAtPath(ruta) as AudioImporter;
        if (importador == null) return;

        bool cambio = false;
        if (mono && !importador.forceToMono)
        {
            importador.forceToMono = true;
            cambio = true;
        }
        if (streamingSiLargo && clip.length > SegundosParaStreaming)
        {
            AudioImporterSampleSettings s = importador.defaultSampleSettings;
            if (s.loadType != AudioClipLoadType.Streaming)
            {
                s.loadType = AudioClipLoadType.Streaming;
                importador.defaultSampleSettings = s;
                cambio = true;
            }
        }
        if (cambio) importador.SaveAndReimport();
    }
}
