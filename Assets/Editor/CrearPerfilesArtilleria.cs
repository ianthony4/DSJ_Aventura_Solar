using UnityEditor;
using UnityEngine;

/// <summary>
/// Menú  Solar Adventure > Crear perfiles de artillería
/// Crea (si no existen) los 8 assets PerfilArtilleria en Assets/Data/Artilleria con valores iniciales pensados
/// para que cada planeta se sienta distinto. Son un punto de partida: afínalos jugando.
/// No sobrescribe perfiles que ya hayas creado o editado.
/// </summary>
public static class CrearPerfilesArtilleria
{
    private const string CarpetaRaiz = "Assets/Data";
    private const string Carpeta = "Assets/Data/Artilleria";

    [MenuItem("Solar Adventure/Crear perfiles de artillería")]
    public static void CrearPerfiles()
    {
        AsegurarCarpeta(CarpetaRaiz);
        AsegurarCarpeta(Carpeta);

        int creados = 0;
        int existentes = 0;

        //                 archivo      planeta     mundo  g     vel  lanzMax tMin  tMax  vientoX vientoZ rMin rMax aviso salva interv enfr  precis disper radio dano  notas
        Crear(ref creados, ref existentes, "Mercurio", "Mercurio", true, 3.7f, 14f, 30f, 1.2f, 6f, 0f, 0f, 18f, 45f, 1.5f, 3, 1.2f, 4.5f, 0.60f, 3.0f, 1.0f, 1.0f,
            "Vacío y gravedad baja: arcos altos y largos, mucho tiempo para reaccionar. El astronauta corre a 10 m/s, por eso el proyectil va a 14. Planeta de aprendizaje: adelanto parcial y dispersión alta.");
        Crear(ref creados, ref existentes, "Venus", "Venus", false, 8.9f, 13f, 38f, 1.2f, 8f, 1.8f, 0.9f, 18f, 42f, 1.3f, 3, 1.0f, 4.0f, 0.80f, 2.5f, 1.0f, 1.0f,
            "Atmósfera densa: el viento curva visiblemente las parábolas. Hay que leer el anillo, no la dirección del cañón.");
        Crear(ref creados, ref existentes, "Tierra", "Tierra", false, 9.8f, 14f, 40f, 1.2f, 8f, 0.6f, -0.4f, 18f, 45f, 1.2f, 3, 1.0f, 4.0f, 0.85f, 2.0f, 1.0f, 1.0f,
            "Referencia: valores equilibrados, brisa ligera.");
        Crear(ref creados, ref existentes, "Marte", "Marte", false, 3.7f, 11f, 32f, 1.3f, 8f, 1.2f, 0f, 16f, 45f, 1.3f, 3, 1.0f, 4.0f, 0.80f, 2.5f, 1.0f, 1.0f,
            "Gravedad baja con tormentas de polvo: arcos altos que el viento desvía hacia un lado.");
        Crear(ref creados, ref existentes, "Jupiter", "Júpiter", false, 24.8f, 22f, 50f, 0.9f, 6f, -1.0f, 1.0f, 22f, 48f, 1.0f, 2, 1.4f, 4.5f, 0.95f, 1.5f, 1.5f, 1.6f,
            "Gravedad enorme: pocos disparos, rápidos y pesados, con explosión grande. Poco aviso, mucho daño.");
        Crear(ref creados, ref existentes, "Saturno", "Saturno", false, 10.4f, 15f, 40f, 1.1f, 8f, 2.6f, 0f, 18f, 45f, 1.1f, 4, 0.9f, 4.0f, 0.90f, 2.0f, 1.0f, 1.0f,
            "Vientos fuertes y andanadas de 4: las parábolas se curvan mucho y hay que moverse todo el rato.");
        Crear(ref creados, ref existentes, "Urano", "Urano", false, 8.9f, 13f, 38f, 1.0f, 8f, 0f, 2.0f, 16f, 40f, 1.2f, 6, 0.55f, 5.0f, 0.75f, 5.0f, 0.8f, 0.7f,
            "Bombardeo disperso: muchos proyectiles pequeños con mucha dispersión. Cantidad en lugar de potencia.");
        Crear(ref creados, ref existentes, "Neptuno", "Neptuno", false, 11.2f, 17f, 42f, 1.0f, 8f, -2.8f, 1.5f, 20f, 48f, 0.9f, 5, 0.8f, 3.0f, 1.00f, 1.0f, 1.2f, 1.2f,
            "Jefe final: preciso, rápido y con viento brutal. Poco enfriamiento entre andanadas.");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Object carpeta = AssetDatabase.LoadAssetAtPath<Object>(Carpeta);
        if (carpeta != null)
        {
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = carpeta;
        }

        Debug.Log("Perfiles de artillería: " + creados + " creados, " + existentes + " ya existían (" + Carpeta + ").");
    }

    private static void Crear(
        ref int creados, ref int existentes,
        string archivo, string planeta, bool usarGravedadDelMundo, float gravedad,
        float velocidadHorizontal, float velocidadMaxLanzamiento, float tiempoVueloMin, float tiempoVueloMax,
        float vientoX, float vientoZ, float alcanceMin, float alcanceMax,
        float tiempoAviso, int tamanoAndanada, float intervalo, float enfriamiento,
        float precision, float dispersion, float multRadio, float multDano, string notas)
    {
        string ruta = Carpeta + "/Perfil_" + archivo + ".asset";
        if (AssetDatabase.LoadAssetAtPath<PerfilArtilleria>(ruta) != null)
        {
            existentes++;
            return;
        }

        PerfilArtilleria perfil = ScriptableObject.CreateInstance<PerfilArtilleria>();
        perfil.planeta = planeta;
        perfil.notasDeDiseno = notas;
        perfil.useWorldGravity = usarGravedadDelMundo;
        perfil.gravity = gravedad;
        perfil.shellHorizontalSpeed = velocidadHorizontal;
        perfil.maxLaunchSpeed = velocidadMaxLanzamiento;
        perfil.minFlightTime = tiempoVueloMin;
        perfil.maxFlightTime = tiempoVueloMax;
        perfil.wind = new Vector3(vientoX, 0f, vientoZ);
        perfil.blastRadiusMultiplier = multRadio;
        perfil.damageMultiplier = multDano;
        perfil.engageMinRange = alcanceMin;
        perfil.engageMaxRange = alcanceMax;
        perfil.lockOnTime = tiempoAviso;
        perfil.salvoSize = tamanoAndanada;
        perfil.shotInterval = intervalo;
        perfil.salvoCooldown = enfriamiento;
        perfil.leadAccuracy = precision;
        perfil.aimScatter = dispersion;

        AssetDatabase.CreateAsset(perfil, ruta);
        creados++;
    }

    private static void AsegurarCarpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;

        string padre = System.IO.Path.GetDirectoryName(ruta).Replace('\\', '/');
        string nombre = System.IO.Path.GetFileName(ruta);
        AssetDatabase.CreateFolder(padre, nombre);
    }
}
