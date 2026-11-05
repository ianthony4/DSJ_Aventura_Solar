using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Prepara los sonidos de cada planeta al cargarlo (no hay que poner nada en las escenas):
///   * crea el fondo sonoro (FondoSonoro),
///   * añade las pisadas al astronauta (SonidoPasosJugador).
/// Solo actúa en escenas de juego (donde hay un GameManager); el menú principal queda sin estos sonidos.
/// </summary>
public static class SonidosDeEscena
{
    private static bool suscrito;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Arrancar()
    {
        if (!suscrito)
        {
            suscrito = true;
            SceneManager.sceneLoaded += (escena, modo) => Preparar();
        }
        Preparar();
    }

    private static void Preparar()
    {
        if (GameManager.instancia == null) return;

        if (Object.FindFirstObjectByType<FondoSonoro>() == null)
        {
            new GameObject("FondoSonoro").AddComponent<FondoSonoro>();
        }

        GameObject jugador = GameObject.FindGameObjectWithTag("Player");
        if (jugador != null && jugador.GetComponent<CharacterController>() != null && jugador.GetComponent<SonidoPasosJugador>() == null)
        {
            jugador.AddComponent<SonidoPasosJugador>();
        }
    }
}
