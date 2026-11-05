using UnityEngine;

/// <summary>
/// Pisadas del jugador en bucle. Se añade solo al astronauta al empezar un planeta (ver SonidosDeEscena) si hay un
/// sonido en el espacio "5 · Jugador". Suena mientras camina por el suelo; más agudo si corre; en silencio si está
/// parado, en el aire o el juego está en pausa.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class SonidoPasosJugador : MonoBehaviour
{
    private const float VelocidadMinima = 0.5f;

    private CharacterController controlador;
    private AudioSource fuente;
    private AudioCue cue;
    private float velocidadReferencia = 8f;
    private float tonoBase = 1f;
    private float volumenActual;
    private bool encendida;

    private void Start()
    {
        SonidosJuego sonidos = SonidosJuego.Actual;
        controlador = GetComponent<CharacterController>();
        if (sonidos == null || !sonidos.pasosJugador.HasClip || controlador == null)
        {
            enabled = false;
            return;
        }

        cue = sonidos.pasosJugador;
        velocidadReferencia = Mathf.Max(0.5f, sonidos.velocidadReferenciaPasosJugador);

        fuente = gameObject.AddComponent<AudioSource>();
        cue.Aplicar(fuente, true);
        tonoBase = fuente.pitch;
        fuente.volume = 0f;
        if (fuente.clip == null) enabled = false;
    }

    private void Update()
    {
        if (fuente == null) return;

        Vector3 v = controlador.velocity;
        v.y = 0f;
        float rapidez = v.magnitude;
        bool suena = controlador.isGrounded && Time.timeScale > 0f && rapidez > VelocidadMinima;

        float objetivo = suena ? cue.volume : 0f;
        volumenActual = Mathf.Lerp(volumenActual, objetivo, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
        fuente.volume = volumenActual;

        if (suena)
        {
            fuente.pitch = tonoBase * Mathf.Clamp(rapidez / velocidadReferencia, 0.8f, 1.3f);
            if (!encendida)
            {
                encendida = true;
                fuente.Play();
            }
        }
        else if (encendida && volumenActual < 0.005f)
        {
            encendida = false;
            fuente.Pause();
        }
    }
}
