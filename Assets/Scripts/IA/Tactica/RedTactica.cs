using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// "Pizarra" compartida (blackboard) de todos los enemigos: es la forma más simple de COOPERACIÓN.
///   * Quien ve al jugador (dron, mortero o duende) publica dónde está y a qué velocidad va.
///   * El mortero puede disparar sin verlo si un observador lo está viendo (fuego indirecto).
///   * Los duendes cercanos acuden a investigar la última posición conocida.
///   * La escuadra de persecución reparte posiciones de flanqueo para rodear al jugador.
/// Es estática: no hay que ponerla en la escena. Se reinicia sola al cargar cada escena.
/// </summary>
public static class RedTactica
{
    public static Vector3 UltimaPosicion { get; private set; }
    public static Vector3 UltimaVelocidad { get; private set; }
    public static float TiempoUltimoAvistamiento { get; private set; }
    public static Object UltimoInformante { get; private set; }

    /// <summary>Aumenta cada vez que alguien da una alerta nueva (tras un rato sin ver al jugador).</summary>
    public static int ContadorAlertas { get; private set; }

    private const float PausaEntreAlertas = 3f;
    private static readonly List<AIMob> perseguidores = new List<AIMob>();

    /// <summary>Segundos desde el último avistamiento.</summary>
    public static float EdadInformacion { get { return Time.time - TiempoUltimoAvistamiento; } }

    public static bool HayAvistamiento(float maxEdadSegundos)
    {
        return EdadInformacion <= maxEdadSegundos;
    }

    public static void ReportarAvistamiento(Vector3 posicion, Vector3 velocidad, Object informante)
    {
        if (EdadInformacion > PausaEntreAlertas) ContadorAlertas++;

        UltimaPosicion = posicion;
        UltimaVelocidad = velocidad;
        TiempoUltimoAvistamiento = Time.time;
        UltimoInformante = informante;
    }

    // ---------------- Escuadra de persecución ----------------

    /// <summary>Se apunta a la persecución y devuelve su puesto (0 = persigue de frente, 1+ = flanqueos).</summary>
    public static int RegistrarPerseguidor(AIMob mob)
    {
        perseguidores.RemoveAll(m => m == null);
        if (!perseguidores.Contains(mob)) perseguidores.Add(mob);
        return perseguidores.IndexOf(mob);
    }

    public static void RetirarPerseguidor(AIMob mob)
    {
        perseguidores.Remove(mob);
    }

    public static int PuestoEnEscuadra(AIMob mob)
    {
        return perseguidores.IndexOf(mob);
    }

    public static int CantidadPerseguidores { get { return perseguidores.Count; } }

    // ---------------- Reinicio ----------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Inicializar()
    {
        Reiniciar();
        SceneManager.sceneLoaded -= AlCargarEscena;
        SceneManager.sceneLoaded += AlCargarEscena;
    }

    private static void AlCargarEscena(Scene escena, LoadSceneMode modo)
    {
        if (modo == LoadSceneMode.Single) Reiniciar();
    }

    private static void Reiniciar()
    {
        UltimaPosicion = Vector3.zero;
        UltimaVelocidad = Vector3.zero;
        TiempoUltimoAvistamiento = -1000f;
        UltimoInformante = null;
        ContadorAlertas = 0;
        perseguidores.Clear();
    }
}
