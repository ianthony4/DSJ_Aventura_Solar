using TMPro; // Necesario para los textos
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement; // Por si queremos reiniciar

public class GameManager : MonoBehaviour
{
    // --- CONFIGURACIÓN ---
    [Header("Configuración del Nivel")]
    public float tiempoLimite = 480f; // 8 minutos en segundos
    public int monedasParaGanar = 7;
    public int monedasTotalesEnMapa = 10;
    public string nombreEscenaMenu = "MainMenu"; // Nombre de la escena del menú principal

    [Header("Puntaje")]
    public int puntosPorMoneda = 100;
    public int puntosPorSegundo = 10;

    // --- REFERENCIAS UI ---
    [Header("Interfaz (Arrastra los textos aquí)")]
    public TextMeshProUGUI textoTiempo;
    public TextMeshProUGUI textoMonedas;

    //paneles
    public GameObject panelGanaste;
    public GameObject panelPerdiste;
    public TextMeshProUGUI textoPuntajeFinal; // El texto dentro del panel para mostrar el score
    public GameObject panelOpciones;

    // --- VARIABLES INTERNAS ---
    private float tiempoRestante;
    private int monedasActuales = 0;
    private bool juegoTerminado = false;
    private bool juegoPausado = false;

    // Singleton: Para poder llamar a este script desde la moneda o la nave fácilmente
    public static GameManager instancia;

    // Lectura para otros sistemas (la IA usa esto para saber si el jugador ya va hacia la nave)
    public int MonedasActuales { get { return monedasActuales; } }
    public int MonedasParaGanar { get { return monedasParaGanar; } }
    public bool JuegoTerminado { get { return juegoTerminado; } }

    void Awake()
    {
        // Esto asegura que solo haya un GameManager y sea accesible desde todos lados
        if (instancia == null) instancia = this;
    }

    void Start()
    {
        tiempoRestante = tiempoLimite;
        panelGanaste.SetActive(false); // Asegurar que el panel de ganar esté oculto
        panelPerdiste.SetActive(false); // Asegurar que el panel de perder esté oculto
        panelOpciones.SetActive(false); // Asegurar que el panel de opciones esté oculto
        Time.timeScale = 1; // Asegurar que el tiempo esté normal al iniciar
        ActualizarHUD();
    }

    void Update()
    {
        // 1. DETECTAR TECLA ESCAPE (PAUSA). Si existe un PauseMenuManager (nuevo menú, Input System), él se encarga de ESC.
        if (PauseMenuManager.Instance == null && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            AlternarPausa();
        }

        // 2. CORRER TIEMPO
        if (!juegoTerminado && !juegoPausado)
        {
            GestionarTiempo();
        }
    }

    // --- LÓGICA DE PAUSA ---
    public void AlternarPausa()
    {
        // Si el juego ya terminó (ganaste o perdiste), no permitimos pausar/despausar
        if (juegoTerminado) return;

        juegoPausado = !juegoPausado;

        // Mostramos u ocultamos el panel de botones
        panelOpciones.SetActive(juegoPausado);

        // Congelamos o descongelamos el tiempo (Física y Animaciones)
        if (juegoPausado)
        {
            Time.timeScale = 0; // Congela todo
            // Opcional: Mostrar cursor del mouse
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Time.timeScale = 1; // Vuelve a la normalidad
            // Opcional: Ocultar cursor
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public void Boton_RepetirNivel()
    {
        Time.timeScale = 1; // Importante: Descongelar antes de recargar
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        Debug.Log("Reiniciando nivel!!");
    }

    public void Boton_IrAlMenu()
    {
        Time.timeScale = 1;
        SceneManager.LoadScene(nombreEscenaMenu);
    }

    public void Boton_SalirDelJuego()
    {
        Debug.Log("Saliendo del juego...");
        Application.Quit();
    }

    void GestionarTiempo()
    {
        if (tiempoRestante > 0)
        {
            tiempoRestante -= Time.deltaTime;
        }
        else
        {
            tiempoRestante = 0;
            GameOver(); // Se acabó el tiempo
        }

        ActualizarTextoTiempo();
    }

    // --- LÓGICA DE MONEDAS ---

    public void SumarMoneda()
    {
        monedasActuales++;
        ActualizarHUD();
    }

    // --- LÓGICA DE VICTORIA (La nave llamará a esto) ---

    public void IntentarTerminarMision()
    {
        if (monedasActuales >= monedasParaGanar)
        {
            Victoria();
        }
        else
        {
            Debug.Log("¡Faltan monedas! Necesitas " + monedasParaGanar);
            // Aquí podríamos poner un mensajito temporal en pantalla
        }
    }

    void Victoria()
    {
        TerminarJuego(true);
        // Puntaje = puntos por moneda + puntos por cada segundo de oxígeno sobrante
        int puntaje = monedasActuales * puntosPorMoneda + Mathf.FloorToInt(tiempoRestante) * puntosPorSegundo;
        textoPuntajeFinal.text = "Puntaje: " + puntaje;
    }

    private string motivoDerrota = "se acabó el tiempo";

    // Igual que GameOver(), pero indica por qué se perdió (aparece en la Console).
    public void GameOver(string motivo)
    {
        if (juegoTerminado) return;
        motivoDerrota = motivo;
        GameOver();
    }

    public void GameOver()
    {
        TerminarJuego(false);
        Debug.Log("Game Over: " + motivoDerrota + ".");
    }

    void TerminarJuego(bool gano)
    {
        juegoTerminado = true;
        Time.timeScale = 0; // Congelar juego
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (gano)
        {
            panelGanaste.SetActive(true);
        }
        else
        {
            panelPerdiste.SetActive(true);
        }
        panelOpciones.SetActive(true); // Mostrar los botones (repetir, menú, salir)
    }

    // --- ACTUALIZAR PANTALLA ---

    void ActualizarHUD()
    {
        textoMonedas.text = "Monedas: " + monedasActuales + " / " + monedasTotalesEnMapa;
    }

    private int segundoMostrado = -1;

    void ActualizarTextoTiempo()
    {
        // El reloj solo cambia una vez por segundo: así no se crea texto nuevo en cada fotograma.
        int total = Mathf.FloorToInt(tiempoRestante);
        if (total == segundoMostrado) return;
        segundoMostrado = total;

        // Convierte los segundos (ej: 125) a formato reloj (02:05)
        int minutos = total / 60;
        int segundos = total % 60;
        textoTiempo.text = string.Format("{0:00}:{1:00}", minutos, segundos);

        // Opcional: Ponerlo rojo si queda poco tiempo
        if (tiempoRestante < 30) textoTiempo.color = Color.red;
    }
}
