using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Menú principal con tres destinos: selección de planetas, opciones y créditos.
///
/// COMPATIBILIDAD: los campos públicos y los métodos MostrarMenuPrincipal / MostrarSeleccionNivel / MostrarCreditos /
/// SalirDelJuego conservan su nombre, así que los botones que ya tienes conectados por OnClick siguen funcionando.
/// Todo lo nuevo es opcional: si un campo queda vacío, esa función simplemente no se usa (sin errores).
///
/// NOVEDADES
///   * Panel de opciones con volumen (se guarda) y pantalla completa.
///   * Fundido suave entre paneles (tiempo real).
///   * Foco inicial por panel: se puede navegar con teclado y mando.
///   * ESC / B del mando vuelven al menú principal desde cualquier subpanel.
///   * Los botones se pueden conectar SOLOS arrastrándolos a los campos de "Botones". No los conectes además por
///     OnClick, o se ejecutarán dos veces.
///   * Al entrar restaura Time.timeScale, audio y cursor (por si se llega desde la pausa).
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Paneles UI")]
    public GameObject panelMenuPrincipal;
    public GameObject panelSeleccionNivel;
    public GameObject panelCreditos;
    [Tooltip("Panel de opciones (nuevo, opcional).")]
    public GameObject panelOpciones;

    [Header("Foco inicial (teclado / mando)")]
    public Selectable focoMenuPrincipal;
    public Selectable focoSeleccionNivel;
    public Selectable focoOpciones;
    public Selectable focoCreditos;

    [Header("Transición")]
    [Tooltip("Duración del fundido entre paneles (0 = sin fundido).")]
    public float duracionFundido = 0.2f;

    [Header("Botones (opcional: se conectan solos)")]
    public Button botonJugar;
    public Button botonOpciones;
    public Button botonCreditos;
    public Button botonSalir;
    [Tooltip("Todos los botones 'Volver': regresan al menú principal.")]
    public Button[] botonesVolver;

    [Header("Opciones")]
    public Slider sliderVolumen;
    [Tooltip("Texto con el porcentaje de volumen (opcional).")]
    public TMP_Text textoVolumen;
    public Toggle togglePantallaCompleta;

    // El estado es ESTÁTICO a propósito: en la escena MainMenu este script está puesto en varios paneles
    // (Panel_MenuPrincipal y Panel_SeleccionNivel) y todos deben compartir el mismo estado y una sola entrada de teclado.
    private static GameObject panelActual;
    private static Coroutine rutinaFundido;
    private static InputAction accionVolver;
    private static MainMenuController duenio;   // la instancia que creó la entrada y el corredor

    // Objeto auxiliar que siempre está activo. El controlador puede estar puesto en un panel que se desactiva
    // y un objeto inactivo no puede ejecutar corrutinas.
    private static MonoBehaviour corredor;
    private class CorredorMenu : MonoBehaviour { }

    void Awake()
    {
        // Sin límite de FPS: lo gestiona RendimientoJuego (antes aquí se fijaba en 60 y quedaba así en todas las escenas).

        // Se puede llegar desde la pausa de un planeta: se deja todo en estado normal.
        Time.timeScale = 1f;
        AudioListener.pause = false;
        AjustesJuego.FactorPausa = 1f;
        AjustesJuego.Aplicar();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (duenio == null)
        {
            duenio = this;

            accionVolver = new InputAction("Volver", InputActionType.Button);
            accionVolver.AddBinding("<Keyboard>/escape");
            accionVolver.AddBinding("<Gamepad>/buttonEast");
            accionVolver.performed += AlPulsarVolver;
            accionVolver.Enable(); // vive mientras exista el controlador, aunque su panel se oculte

            GameObject auxiliar = new GameObject("MainMenu_Corredor");
            corredor = auxiliar.AddComponent<CorredorMenu>();
        }

        ConectarBotones();
        ConfigurarOpciones();
    }

    void OnDestroy()
    {
        if (duenio != this) return;

        if (accionVolver != null)
        {
            accionVolver.performed -= AlPulsarVolver;
            accionVolver.Disable();
            accionVolver.Dispose();
            accionVolver = null;
        }
        if (corredor != null) Destroy(corredor.gameObject);
        corredor = null;
        rutinaFundido = null;
        panelActual = null;
        duenio = null;
    }

    void Start()
    {
        MostrarMenuPrincipal();
    }

    // ------------------------------------------------------------------ Navegación entre paneles

    public void MostrarMenuPrincipal()
    {
        Debug.Log("Menu Principal");
        MostrarPanel(panelMenuPrincipal, focoMenuPrincipal);
    }

    public void MostrarSeleccionNivel()
    {
        Debug.Log("Mostrando seleccion de nivel");
        MostrarPanel(panelSeleccionNivel, focoSeleccionNivel);
    }

    public void MostrarOpciones()
    {
        Debug.Log("Mostrando opciones");
        MostrarPanel(panelOpciones, focoOpciones);
    }

    public void MostrarCreditos()
    {
        Debug.Log("Mostrando creditos");
        MostrarPanel(panelCreditos, focoCreditos);
    }

    /// <summary>Regresa al menú principal (para los botones 'Volver').</summary>
    public void VolverAlMenuPrincipal()
    {
        MostrarMenuPrincipal();
    }

    public void SalirDelJuego()
    {
        Debug.Log("Saliendo de la aplicación...");
        AjustesJuego.Guardar();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void MostrarPanel(GameObject destino, Selectable foco)
    {
        if (destino == null)
        {
            Debug.LogWarning("MainMenuController: el panel que se quiere mostrar no está asignado en el Inspector.", this);
            return;
        }

        // Al salir del panel de opciones se guardan los ajustes.
        if (panelActual == panelOpciones && panelOpciones != null && destino != panelOpciones)
        {
            AjustesJuego.Guardar();
        }

        panelActual = destino;
        ActivarSoloEste(destino);

        if (corredor == null) return;
        if (rutinaFundido != null) corredor.StopCoroutine(rutinaFundido);
        rutinaFundido = corredor.StartCoroutine(FundirYEnfocar(destino, foco));
    }

    private void ActivarSoloEste(GameObject destino)
    {
        ActivarSiExiste(panelMenuPrincipal, destino);
        ActivarSiExiste(panelSeleccionNivel, destino);
        ActivarSiExiste(panelOpciones, destino);
        ActivarSiExiste(panelCreditos, destino);
    }

    private static void ActivarSiExiste(GameObject panel, GameObject destino)
    {
        if (panel != null) panel.SetActive(panel == destino);
    }

    private IEnumerator FundirYEnfocar(GameObject destino, Selectable foco)
    {
        CanvasGroup grupo = destino.GetComponent<CanvasGroup>();
        if (grupo == null) grupo = destino.AddComponent<CanvasGroup>();

        if (duracionFundido > 0f)
        {
            grupo.alpha = 0f;
            grupo.interactable = false;

            float transcurrido = 0f;
            while (transcurrido < duracionFundido)
            {
                transcurrido += Time.unscaledDeltaTime;
                grupo.alpha = Mathf.Clamp01(transcurrido / duracionFundido);
                yield return null;
            }
        }

        grupo.alpha = 1f;
        grupo.interactable = true;
        grupo.blocksRaycasts = true;

        if (foco != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(foco.gameObject);
        }

        rutinaFundido = null;
    }

    private void AlPulsarVolver(InputAction.CallbackContext contexto)
    {
        // Desde el menú principal no hace nada: no se sale del juego por accidente.
        if (panelActual != null && panelActual != panelMenuPrincipal)
        {
            VolverAlMenuPrincipal();
        }
    }

    // ------------------------------------------------------------------ Botones y opciones

    private void ConectarBotones()
    {
        if (botonJugar != null) botonJugar.onClick.AddListener(MostrarSeleccionNivel);
        if (botonOpciones != null) botonOpciones.onClick.AddListener(MostrarOpciones);
        if (botonCreditos != null) botonCreditos.onClick.AddListener(MostrarCreditos);
        if (botonSalir != null) botonSalir.onClick.AddListener(SalirDelJuego);

        if (botonesVolver != null)
        {
            for (int i = 0; i < botonesVolver.Length; i++)
            {
                if (botonesVolver[i] != null) botonesVolver[i].onClick.AddListener(VolverAlMenuPrincipal);
            }
        }
    }

    private void ConfigurarOpciones()
    {
        if (sliderVolumen != null)
        {
            sliderVolumen.minValue = 0f;
            sliderVolumen.maxValue = 1f;
            sliderVolumen.SetValueWithoutNotify(AjustesJuego.VolumenGeneral);
            sliderVolumen.onValueChanged.AddListener(AlCambiarVolumen);
        }
        ActualizarTextoVolumen(AjustesJuego.VolumenGeneral);

        if (togglePantallaCompleta != null)
        {
            togglePantallaCompleta.SetIsOnWithoutNotify(Screen.fullScreen);
            togglePantallaCompleta.onValueChanged.AddListener(AlCambiarPantallaCompleta);
        }
    }

    private void AlCambiarVolumen(float valor)
    {
        AjustesJuego.VolumenGeneral = valor;
        ActualizarTextoVolumen(valor);
    }

    private void ActualizarTextoVolumen(float valor)
    {
        if (textoVolumen != null) textoVolumen.text = Mathf.RoundToInt(valor * 100f) + "%";
    }

    private void AlCambiarPantallaCompleta(bool activada)
    {
        Screen.fullScreen = activada;
    }
}
