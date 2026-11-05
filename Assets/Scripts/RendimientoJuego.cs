using UnityEngine;

/// <summary>
/// Ajustes de rendimiento que se aplican solos al arrancar el juego (no hay que ponerlo en ninguna escena).
///   * Sin límite de FPS: targetFrameRate = -1 deja que el equipo dibuje lo más rápido que pueda.
///     (En PC es "sin límite"; en móviles -1 significa el valor predeterminado de la plataforma, normalmente 30.)
///   * Sin VSync desde código. En el Editor, el botón VSync de la ventana Game también puede limitar a la
///     frecuencia del monitor: si ves 60 FPS exactos, revísalo.
/// Antes, MainMenuController fijaba 60 FPS al abrir el menú y el límite se quedaba en todas las escenas.
/// </summary>
public static class RendimientoJuego
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Iniciar()
    {
        Application.targetFrameRate = -1;
        QualitySettings.vSyncCount = 0;
    }
}
