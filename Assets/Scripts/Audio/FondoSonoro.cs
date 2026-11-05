using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Sonido de fondo de los planetas. Lo crea SonidosDeEscena al cargar una escena de juego (donde hay un GameManager);
/// en el menú principal no suena. Sube de volumen con suavidad al empezar y se repite sin parar.
/// El sonido se elige en  Solar Adventure > Sonido > Abrir ajustes de sonido  (espacio "1 · Fondo": el de cada planeta
/// o, si no tiene, el general).
/// </summary>
public class FondoSonoro : MonoBehaviour
{
    private const float SegundosDeEntrada = 3f;

    private AudioSource fuente;
    private float volumenObjetivo;

    private void Start()
    {
        SonidosJuego sonidos = SonidosJuego.Actual;
        // El fondo del planeta si lo tiene; si no, el general.
        AudioCue cue = sonidos != null ? sonidos.FondoPara(SceneManager.GetActiveScene().name) : null;
        if (cue == null || !cue.HasClip)
        {
            enabled = false;
            return;
        }

        fuente = gameObject.AddComponent<AudioSource>();
        cue.Aplicar(fuente, true);
        fuente.spatialBlend = 0f;           // el fondo siempre es 2D
        volumenObjetivo = cue.volume;
        fuente.volume = 0f;
        if (fuente.clip == null)
        {
            enabled = false;
            return;
        }
        fuente.Play();
    }

    private void Update()
    {
        if (fuente == null) return;
        fuente.volume = Mathf.MoveTowards(fuente.volume, volumenObjetivo, volumenObjetivo / SegundosDeEntrada * Time.unscaledDeltaTime);
    }
}
