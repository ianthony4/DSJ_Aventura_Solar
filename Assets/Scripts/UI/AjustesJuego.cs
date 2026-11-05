using UnityEngine;

/// <summary>
/// Ajustes globales del juego compartidos entre el menú principal y el menú de pausa.
/// Guarda el volumen general en PlayerPrefs y lo aplica a AudioListener.volume.
/// "FactorPausa" permite atenuar el audio mientras el juego está en pausa sin perder el volumen elegido.
/// Clase estática: no hay que ponerla en ninguna escena.
/// </summary>
public static class AjustesJuego
{
    private const string ClaveVolumen = "ajustes.volumenGeneral";
    private const float VolumenPorDefecto = 0.8f;

    private static bool cargado;
    private static float volumenGeneral = VolumenPorDefecto;
    private static float factorPausa = 1f;

    /// <summary>Volumen general (0..1). Al asignarlo se aplica al instante y se guarda en memoria.</summary>
    public static float VolumenGeneral
    {
        get { Cargar(); return volumenGeneral; }
        set
        {
            Cargar();
            volumenGeneral = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(ClaveVolumen, volumenGeneral);
            AplicarVolumen();
        }
    }

    /// <summary>Multiplicador temporal (0..1) usado por el menú de pausa para atenuar el audio.</summary>
    public static float FactorPausa
    {
        get { return factorPausa; }
        set
        {
            factorPausa = Mathf.Clamp01(value);
            AplicarVolumen();
        }
    }

    /// <summary>Carga los ajustes guardados y los aplica. Llamar al iniciar cada escena con menús.</summary>
    public static void Aplicar()
    {
        Cargar();
        AplicarVolumen();
    }

    /// <summary>Escribe los ajustes en disco (llamar al cerrar un menú de opciones o al cambiar de escena).</summary>
    public static void Guardar()
    {
        PlayerPrefs.Save();
    }

    private static void Cargar()
    {
        if (cargado) return;
        cargado = true;
        volumenGeneral = Mathf.Clamp01(PlayerPrefs.GetFloat(ClaveVolumen, VolumenPorDefecto));
    }

    private static void AplicarVolumen()
    {
        AudioListener.volume = volumenGeneral * factorPausa;
    }
}
