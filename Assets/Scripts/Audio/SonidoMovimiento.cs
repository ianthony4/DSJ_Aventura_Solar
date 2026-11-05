using UnityEngine;

/// <summary>
/// Sonido en bucle que acompaña al movimiento de un enemigo (pasos del arácnido, motor del mortero).
/// Lo añade AIMob solo, al empezar, si hay un sonido asignado. Suena mientras se mueve (más agudo si corre),
/// y sube y baja de volumen con suavidad. En silencio si está parado, muerto o el juego está en pausa.
/// </summary>
public class SonidoMovimiento : MonoBehaviour
{
    private const float VelocidadMinima = 0.3f;

    private AIMob mob;
    private AudioSource fuente;
    private AudioCue cue;
    private float velocidadReferencia = 3f;
    private float tonoBase = 1f;
    private float volumenActual;
    private bool encendida;

    /// <param name="dueno">El enemigo que se mueve.</param>
    /// <param name="sonido">Ajustes del sonido (volumen, distancia, tono...).</param>
    /// <param name="velocidadDeReferencia">Rapidez (m/s) a la que el sonido va a su tono normal.</param>
    public void Iniciar(AIMob dueno, AudioCue sonido, float velocidadDeReferencia)
    {
        mob = dueno;
        cue = sonido;
        velocidadReferencia = Mathf.Max(0.1f, velocidadDeReferencia);

        fuente = gameObject.AddComponent<AudioSource>();
        cue.Aplicar(fuente, true);
        tonoBase = fuente.pitch;
        fuente.volume = 0f;
        if (fuente.clip == null) enabled = false;
    }

    private void Update()
    {
        if (fuente == null || mob == null) return;

        float rapidez = mob.VelocidadHorizontal;
        bool suena = !mob.EstaMuerto && Time.timeScale > 0f && rapidez > VelocidadMinima;

        float objetivo = suena ? cue.volume : 0f;
        volumenActual = Mathf.Lerp(volumenActual, objetivo, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
        fuente.volume = volumenActual;

        if (suena)
        {
            fuente.pitch = tonoBase * Mathf.Clamp(rapidez / velocidadReferencia, 0.7f, 1.6f);
            if (!encendida)
            {
                encendida = true;
                fuente.Play();
            }
        }
        else if (encendida && volumenActual < 0.005f)
        {
            encendida = false;
            fuente.Pause();   // sin volumen no hace falta que siga reproduciendo
        }
    }
}
