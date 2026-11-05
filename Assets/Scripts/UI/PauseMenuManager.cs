using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Menú de pausa. Se pone en el Canvas de pausa de cada escena de planeta.
///
///   * Input System: ESC o botón Start del mando pausan/reanudan; B del mando (o ESC) reanudan. Las acciones se crean
///     por código, no hace falta tocar el asset InputSystem_Actions.
///   * Pausa segura: Time.timeScale = 0, cursor liberado y audio atenuado (o pausado). Al reanudar se restauran el
///     timeScale y el estado del cursor que había antes. No se puede pausar con el juego ya terminado
///     (victoria/derrota ponen Time.timeScale = 0).
///   * Los botones y el slider se conectan SOLOS por código (arrastra las referencias; no hace falta OnClick).
///   * Convive con GameManager: cuando existe un PauseMenuManager, GameManager deja de reaccionar a ESC.
/// </summary>
public class PauseMenuManager : MonoBehaviour
{
    public enum PauseAudioMode
    {
        None,   // el audio sigue igual
        Duck,   // baja el volumen general (duckFactor)
        Pause   // pausa todo el audio (AudioListener.pause); los sonidos de UI con ignoreListenerPause siguen sonando
    }

    public static PauseMenuManager Instance { get; private set; }

    /// <summary>True mientras el juego está pausado por este menú.</summary>
    public bool IsPaused { get; private set; }

    /// <summary>Se dispara al pausar (true) y al reanudar (false).</summary>
    public event System.Action<bool> PausedChanged;

    [Header("Panel de pausa")]
    [Tooltip("Objeto raíz del menú (PanelPausa). Se oculta al iniciar y se muestra al pausar.")]
    [SerializeField] private GameObject pausePanel;
    [Tooltip("CanvasGroup del panel para el fundido de entrada (opcional).")]
    [SerializeField] private CanvasGroup pauseCanvasGroup;
    [SerializeField] private float fadeDuration = 0.15f;

    [Header("Botones")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button mainMenuButton;

    [Header("Volumen")]
    [SerializeField] private Slider volumeSlider;
    [Tooltip("Texto que muestra el porcentaje (opcional).")]
    [SerializeField] private TMP_Text volumeLabel;

    [Header("Escenas")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Audio durante la pausa")]
    [SerializeField] private PauseAudioMode audioMode = PauseAudioMode.Duck;
    [Range(0f, 1f)]
    [SerializeField] private float duckFactor = 0.25f;

    private InputAction pauseAction;
    private InputAction cancelAction;

    private float timeScaleBeforePause = 1f;
    private CursorLockMode cursorLockBeforePause;
    private bool cursorVisibleBeforePause;
    private Coroutine fadeRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("PauseMenuManager: ya existe otro en la escena; se desactiva este.", this);
            enabled = false;
            return;
        }
        Instance = this;

        AjustesJuego.Aplicar();
        AjustesJuego.FactorPausa = 1f;

        CreateInputActions();
        WireUi();

        if (pausePanel != null) pausePanel.SetActive(false);
    }

    private void OnEnable()
    {
        if (pauseAction != null) pauseAction.Enable();
        if (cancelAction != null) cancelAction.Enable();
    }

    private void OnDisable()
    {
        if (pauseAction != null) pauseAction.Disable();
        if (cancelAction != null) cancelAction.Disable();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;

        if (pauseAction != null)
        {
            pauseAction.performed -= OnPausePerformed;
            pauseAction.Dispose();
        }
        if (cancelAction != null)
        {
            cancelAction.performed -= OnCancelPerformed;
            cancelAction.Dispose();
        }

        // Si la escena se cierra con el juego en pausa, no se deja el tiempo ni el audio congelados.
        if (IsPaused) RestoreWorldState();
    }

    // ------------------------------------------------------------------ Entrada

    private void CreateInputActions()
    {
        pauseAction = new InputAction("Pausa", InputActionType.Button);
        pauseAction.AddBinding("<Keyboard>/escape");
        pauseAction.AddBinding("<Gamepad>/start");
        pauseAction.performed += OnPausePerformed;

        // Botón "atrás" del mando: solo reanuda (no pausa), para no pausar sin querer en pleno juego.
        cancelAction = new InputAction("Cancelar", InputActionType.Button);
        cancelAction.AddBinding("<Gamepad>/buttonEast");
        cancelAction.performed += OnCancelPerformed;
    }

    private void OnPausePerformed(InputAction.CallbackContext context)
    {
        Toggle();
    }

    private void OnCancelPerformed(InputAction.CallbackContext context)
    {
        if (IsPaused) Resume();
    }

    private void WireUi()
    {
        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (restartButton != null) restartButton.onClick.AddListener(RestartLevel);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(ExitToMainMenu);

        if (volumeSlider != null)
        {
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.SetValueWithoutNotify(AjustesJuego.VolumenGeneral);
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }
        UpdateVolumeLabel(AjustesJuego.VolumenGeneral);
    }

    // ------------------------------------------------------------------ Pausa

    public void Toggle()
    {
        if (IsPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        if (IsPaused) return;

        // Con Time.timeScale en 0 y sin estar pausado por nosotros, el juego ya terminó (victoria o derrota).
        if (Time.timeScale <= 0f) return;

        IsPaused = true;
        timeScaleBeforePause = Time.timeScale;
        Time.timeScale = 0f;

        cursorLockBeforePause = Cursor.lockState;
        cursorVisibleBeforePause = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        ApplyPauseAudio(true);
        ShowPanel();

        if (PausedChanged != null) PausedChanged(true);
    }

    public void Resume()
    {
        if (!IsPaused) return;

        RestoreWorldState();
        HidePanel();
        AjustesJuego.Guardar();

        if (PausedChanged != null) PausedChanged(false);
    }

    public void RestartLevel()
    {
        LeaveScene(true);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ExitToMainMenu()
    {
        LeaveScene(false);
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // Devuelve tiempo, audio y (opcionalmente) el cursor a como estaban antes de pausar.
    private void RestoreWorldState()
    {
        IsPaused = false;
        Time.timeScale = timeScaleBeforePause > 0f ? timeScaleBeforePause : 1f;
        Cursor.lockState = cursorLockBeforePause;
        Cursor.visible = cursorVisibleBeforePause;
        ApplyPauseAudio(false);
    }

    private void LeaveScene(bool restoreCursor)
    {
        if (IsPaused)
        {
            RestoreWorldState();
        }
        else
        {
            Time.timeScale = 1f;
            ApplyPauseAudio(false);
        }

        // El menú principal gestiona su propio cursor; al reiniciar se conserva el del juego.
        if (!restoreCursor)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        AjustesJuego.Guardar();
    }

    // ------------------------------------------------------------------ Audio

    private void ApplyPauseAudio(bool paused)
    {
        switch (audioMode)
        {
            case PauseAudioMode.Duck:
                AjustesJuego.FactorPausa = paused ? duckFactor : 1f;
                break;
            case PauseAudioMode.Pause:
                AudioListener.pause = paused;
                AjustesJuego.FactorPausa = 1f;
                break;
            default:
                AjustesJuego.FactorPausa = 1f;
                break;
        }

        if (!paused) AudioListener.pause = false;
    }

    private void OnVolumeChanged(float value)
    {
        AjustesJuego.VolumenGeneral = value;
        UpdateVolumeLabel(value);
    }

    private void UpdateVolumeLabel(float value)
    {
        if (volumeLabel != null) volumeLabel.text = Mathf.RoundToInt(value * 100f) + "%";
    }

    // ------------------------------------------------------------------ Panel

    private void ShowPanel()
    {
        if (pausePanel == null) return;

        pausePanel.SetActive(true);

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeInAndFocus());
    }

    private void HidePanel()
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }
        if (pausePanel != null) pausePanel.SetActive(false);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    // Fundido de entrada con tiempo real (el juego está a timeScale 0) y foco en "Reanudar" para teclado/mando.
    private IEnumerator FadeInAndFocus()
    {
        if (pauseCanvasGroup != null)
        {
            pauseCanvasGroup.alpha = 0f;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                pauseCanvasGroup.alpha = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, fadeDuration));
                yield return null;
            }
            pauseCanvasGroup.alpha = 1f;
        }

        if (EventSystem.current != null && resumeButton != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
        }

        fadeRoutine = null;
    }
}
