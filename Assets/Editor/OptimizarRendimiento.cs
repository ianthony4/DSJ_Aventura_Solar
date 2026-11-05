using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Menú  Solar Adventure > Rendimiento  : ajustes de rendimiento que se aplican a la escena abierta.
///   Optimizar terrenos   dibujado por instancias (sin cambio visual) y menos triángulos lejanos (Pixel Error 8).
/// El terreno de Mercurio ya está optimizado en su prefab; los demás planetas tienen sus terrenos dentro de la
/// escena, así que se aplica abriendo cada uno y usando este menú (se puede deshacer con Ctrl+Z).
/// </summary>
public static class OptimizarRendimiento
{
    private const float PixelErrorObjetivo = 8f;

    [MenuItem("Solar Adventure/Rendimiento/Optimizar terrenos de la escena abierta")]
    public static void OptimizarTerrenos()
    {
        Terrain[] terrenos = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        if (terrenos.Length == 0)
        {
            EditorUtility.DisplayDialog("Rendimiento", "Esta escena no tiene terrenos (componente Terrain).", "OK");
            return;
        }

        int cambiados = 0;
        foreach (Terrain t in terrenos)
        {
            bool cambio = false;
            Undo.RecordObject(t, "Optimizar terreno");
            if (!t.drawInstanced) { t.drawInstanced = true; cambio = true; }
            if (t.heightmapPixelError < PixelErrorObjetivo) { t.heightmapPixelError = PixelErrorObjetivo; cambio = true; }
            if (cambio)
            {
                EditorUtility.SetDirty(t);
                cambiados++;
            }
        }

        if (cambiados > 0) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Rendimiento",
            cambiados > 0
                ? cambiados + " de " + terrenos.Length + " terreno(s) optimizados. Guarda la escena con Ctrl+S."
                : "Los " + terrenos.Length + " terreno(s) ya estaban optimizados.", "OK");
    }
}
