using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD de la IA táctica, construido por código (no hay que montar nada en el Canvas ni crear arte):
///   * Arriba a la izquierda: barra de vida (SaludJugador) y destello rojo al recibir daño.
///   * Arriba al centro: estado de detección según la RedTactica (OCULTO / TE BUSCAN / DETECTADO).
///   * Arriba a la derecha: granadas y barra de recarga (LanzadorGranadas).
///   * Izquierda: avisos breves ("Un dron te ha detectado", "Fuego amigo: duende eliminado"...).
/// El tiempo y las monedas del GameManager siguen abajo, como antes.
///
/// Cualquier script puede mostrar un aviso con  HUDTactico.Mensaje("texto", color).
/// El menú  Solar Adventure > IA Táctica  lo añade a la escena (objeto HUD_Tactico).
/// </summary>
public class HUDTactico : MonoBehaviour
{
    [Header("Colores")]
    public Color colorPanel = new Color(0.04f, 0.08f, 0.19f, 0.75f);
    public Color colorTexto = new Color(0.78f, 0.88f, 1f, 1f);
    public Color colorVida = new Color(0.25f, 0.85f, 0.45f, 1f);
    public Color colorVidaBaja = new Color(1f, 0.25f, 0.15f, 1f);
    public Color colorOculto = new Color(0.6f, 0.7f, 0.8f, 0.8f);
    public Color colorBuscan = new Color(1f, 0.75f, 0.15f, 1f);
    public Color colorDetectado = new Color(1f, 0.2f, 0.12f, 1f);

    [Header("Detección")]
    [Tooltip("Segundos desde el último avistamiento para mostrar DETECTADO.")]
    public float segundosDetectado = 0.6f;
    [Tooltip("Segundos desde el último avistamiento para mostrar TE BUSCAN.")]
    public float segundosBuscan = 6f;

    [Header("Avisos")]
    public float duracionAviso = 3.5f;
    public int maximoAvisos = 4;

    private static HUDTactico instancia;
    private static readonly Dictionary<string, float> ultimoPorClave = new Dictionary<string, float>();

    private SaludJugador salud;
    private LanzadorGranadas lanzador;
    private bool hayEnemigos;
    private float temporizadorBusqueda;

    private RectTransform rellenoVida;
    private Image imagenVida;
    private TextMeshProUGUI textoVida;
    private TextMeshProUGUI textoDeteccion;
    private Image fondoDeteccion;
    private TextMeshProUGUI textoGranadas;
    private RectTransform rellenoRecarga;
    private Image destello;
    private float alphaDestello;
    private GameObject panelVida;
    private GameObject panelGranadas;
    private GameObject panelDeteccion;

    private class Aviso
    {
        public TextMeshProUGUI texto;
        public float nacimiento;
    }
    private readonly List<Aviso> avisos = new List<Aviso>();
    private RectTransform contenedorAvisos;

    /// <summary>True si hay un HUD táctico en la escena (LanzadorGranadas oculta entonces su recuadro de respaldo).</summary>
    public static bool Activo { get { return instancia != null; } }

    /// <summary>Muestra un aviso breve. Sin HUD en la escena, lo escribe en la Console.</summary>
    public static void Mensaje(string texto, Color color)
    {
        if (instancia == null)
        {
            Debug.Log(texto);
            return;
        }
        instancia.AgregarAviso(texto, color);
    }

    /// <summary>Como Mensaje, pero no repite la misma clave antes de 'enfriamiento' segundos (evita spam).</summary>
    public static void MensajeUnico(string clave, string texto, Color color, float enfriamiento)
    {
        float ultimo;
        if (ultimoPorClave.TryGetValue(clave, out ultimo) && Time.time - ultimo < enfriamiento) return;
        ultimoPorClave[clave] = Time.time;
        Mensaje(texto, color);
    }

    private void Awake()
    {
        instancia = this;
        ultimoPorClave.Clear();
        Construir();
    }

    private void OnDestroy()
    {
        if (instancia == this) instancia = null;
        if (salud != null) salud.Danado -= AlRecibirDanio;
    }

    private void Start()
    {
        BuscarReferencias();
    }

    private void BuscarReferencias()
    {
        GameObject jugador = GameObject.FindGameObjectWithTag("Player");
        if (jugador != null)
        {
            if (salud == null)
            {
                salud = jugador.GetComponent<SaludJugador>();
                if (salud != null) salud.Danado += AlRecibirDanio;
            }
            if (lanzador == null) lanzador = jugador.GetComponent<LanzadorGranadas>();
        }
        hayEnemigos = FindFirstObjectByType<AIMob>() != null || FindFirstObjectByType<DronObservador>() != null;

        panelVida.SetActive(salud != null);
        panelGranadas.SetActive(lanzador != null);
        panelDeteccion.SetActive(hayEnemigos);
    }

    private void Update()
    {
        // Revisa de vez en cuando por si el jugador o los enemigos aparecen después (o mueren todos).
        temporizadorBusqueda -= Time.unscaledDeltaTime;
        if (temporizadorBusqueda <= 0f)
        {
            temporizadorBusqueda = 2f;
            BuscarReferencias();
        }

        ActualizarVida();
        ActualizarDeteccion();
        ActualizarGranadas();
        ActualizarAvisos();

        alphaDestello = Mathf.MoveTowards(alphaDestello, 0f, Time.unscaledDeltaTime * 1.6f);
        destello.color = new Color(1f, 0.05f, 0f, alphaDestello);
    }

    // Para no crear texto nuevo en cada fotograma, solo se reescribe lo que cambió.
    private int vidaMostrada = int.MinValue;
    private bool vidaBajaMostrada;
    private float proporcionVidaMostrada = -1f;
    private int estadoDeteccion = -1;      // 0 oculto, 1 te buscan, 2 detectado
    private float proximoPulso;
    private int granadasMostradas = -1;
    private int granadasMaxMostradas = -1;
    private float progresoMostrado = -1f;

    private void ActualizarVida()
    {
        if (salud == null) return;

        float p = salud.Porcentaje;
        if (Mathf.Abs(p - proporcionVidaMostrada) > 0.001f)
        {
            proporcionVidaMostrada = p;
            rellenoVida.anchorMax = new Vector2(p, 1f);
        }

        bool baja = p < 0.3f;
        if (vidaMostrada == int.MinValue || baja != vidaBajaMostrada)
        {
            vidaBajaMostrada = baja;
            imagenVida.color = baja ? colorVidaBaja : colorVida;
        }

        int vida = Mathf.CeilToInt(salud.SaludActual);
        if (vida != vidaMostrada)
        {
            vidaMostrada = vida;
            textoVida.text = "VIDA  " + vida;
        }
    }

    private void ActualizarDeteccion()
    {
        if (!hayEnemigos) return;

        int estado = RedTactica.HayAvistamiento(segundosDetectado) ? 2 : (RedTactica.HayAvistamiento(segundosBuscan) ? 1 : 0);
        if (estado != estadoDeteccion)
        {
            estadoDeteccion = estado;
            proximoPulso = 0f;
            if (estado == 2)
            {
                textoDeteccion.text = "DETECTADO";
                textoDeteccion.color = colorDetectado;
            }
            else if (estado == 1)
            {
                textoDeteccion.text = "TE BUSCAN";
                textoDeteccion.color = colorBuscan;
                fondoDeteccion.color = colorPanel;
            }
            else
            {
                textoDeteccion.text = "OCULTO";
                textoDeteccion.color = colorOculto;
                fondoDeteccion.color = colorPanel;
            }
        }

        // El parpadeo de DETECTADO se actualiza 20 veces por segundo, no en cada fotograma.
        if (estado == 2 && Time.unscaledTime >= proximoPulso)
        {
            proximoPulso = Time.unscaledTime + 0.05f;
            float pulso = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 10f);
            fondoDeteccion.color = new Color(colorDetectado.r * 0.35f, 0.02f, 0.02f, 0.8f * pulso);
        }
    }

    private void ActualizarGranadas()
    {
        if (lanzador == null) return;

        if (lanzador.Granadas != granadasMostradas || lanzador.granadasMaximas != granadasMaxMostradas)
        {
            granadasMostradas = lanzador.Granadas;
            granadasMaxMostradas = lanzador.granadasMaximas;
            textoGranadas.text = "GRANADAS  " + granadasMostradas + " / " + granadasMaxMostradas + "\n<size=60%>[clic izq. / G]</size>";
        }

        float progreso = lanzador.Granadas >= lanzador.granadasMaximas ? 1f : lanzador.ProgresoRecarga;
        if (Mathf.Abs(progreso - progresoMostrado) > 0.004f)
        {
            progresoMostrado = progreso;
            rellenoRecarga.anchorMax = new Vector2(progreso, 1f);
        }
    }

    private void ActualizarAvisos()
    {
        for (int i = avisos.Count - 1; i >= 0; i--)
        {
            Aviso a = avisos[i];
            float edad = Time.unscaledTime - a.nacimiento;
            if (edad > duracionAviso)
            {
                Destroy(a.texto.gameObject);
                avisos.RemoveAt(i);
                continue;
            }
            Color c = a.texto.color;
            c.a = Mathf.Clamp01((duracionAviso - edad) / 0.6f);
            a.texto.color = c;
        }
    }

    private void AgregarAviso(string texto, Color color)
    {
        while (avisos.Count >= Mathf.Max(1, maximoAvisos))
        {
            Destroy(avisos[0].texto.gameObject);
            avisos.RemoveAt(0);
        }

        TextMeshProUGUI t = CrearTexto("Aviso", contenedorAvisos, 26, TextAlignmentOptions.Left);
        t.text = texto;
        t.color = color;
        t.rectTransform.sizeDelta = new Vector2(700f, 36f);
        avisos.Add(new Aviso { texto = t, nacimiento = Time.unscaledTime });
    }

    private void AlRecibirDanio(float cantidad)
    {
        alphaDestello = Mathf.Clamp(alphaDestello + 0.25f + cantidad / 100f, 0f, 0.55f);
    }

    // ------------------------------------------------------------ Construcción por código

    private void Construir()
    {
        GameObject canvasGo = new GameObject("CanvasHUDTactico", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -1; // debajo del Canvas de Logica: la pausa y los paneles de ganar/perder lo tapan
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        RectTransform raiz = (RectTransform)canvasGo.transform;

        // Destello de daño (pantalla completa, no bloquea clics)
        destello = CrearImagen("Destello", raiz, new Color(1f, 0f, 0f, 0f));
        Estirar(destello.rectTransform);

        // Vida (arriba izquierda)
        panelVida = CrearPanel("Vida", raiz, new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(380f, 64f));
        textoVida = CrearTexto("Texto", panelVida.transform, 24, TextAlignmentOptions.Left);
        Colocar(textoVida.rectTransform, new Vector2(0f, 1f), new Vector2(14f, -6f), new Vector2(350f, 28f));
        Image fondoBarra = CrearImagen("FondoBarra", panelVida.transform, new Color(0f, 0f, 0f, 0.5f));
        Colocar(fondoBarra.rectTransform, new Vector2(0f, 0f), new Vector2(14f, 12f), new Vector2(352f, 16f));
        imagenVida = CrearImagen("Relleno", fondoBarra.transform, colorVida);
        rellenoVida = imagenVida.rectTransform;
        Estirar(rellenoVida);

        // Detección (arriba centro)
        panelDeteccion = CrearPanel("Deteccion", raiz, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(300f, 52f));
        fondoDeteccion = panelDeteccion.GetComponent<Image>();
        textoDeteccion = CrearTexto("Texto", panelDeteccion.transform, 30, TextAlignmentOptions.Center);
        Estirar(textoDeteccion.rectTransform);
        textoDeteccion.fontStyle = FontStyles.Bold;

        // Granadas (arriba derecha)
        panelGranadas = CrearPanel("Granadas", raiz, new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(300f, 84f));
        textoGranadas = CrearTexto("Texto", panelGranadas.transform, 26, TextAlignmentOptions.Right);
        Colocar(textoGranadas.rectTransform, new Vector2(1f, 1f), new Vector2(-14f, -6f), new Vector2(280f, 54f));
        Image fondoRecarga = CrearImagen("FondoRecarga", panelGranadas.transform, new Color(0f, 0f, 0f, 0.5f));
        Colocar(fondoRecarga.rectTransform, new Vector2(1f, 0f), new Vector2(-14f, 10f), new Vector2(272f, 10f));
        Image recarga = CrearImagen("Relleno", fondoRecarga.transform, new Color(0.3f, 0.9f, 1f, 1f));
        rellenoRecarga = recarga.rectTransform;
        Estirar(rellenoRecarga);

        // Avisos (izquierda, debajo de la vida)
        GameObject avisosGo = new GameObject("Avisos", typeof(RectTransform));
        avisosGo.transform.SetParent(raiz, false);
        contenedorAvisos = (RectTransform)avisosGo.transform;
        Colocar(contenedorAvisos, new Vector2(0f, 1f), new Vector2(44f, -130f), new Vector2(700f, 200f));
        VerticalLayoutGroup layout = avisosGo.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.spacing = 4f;
        layout.childControlHeight = false;
        layout.childControlWidth = false;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;
    }

    private GameObject CrearPanel(string nombre, Transform padre, Vector2 ancla, Vector2 posicion, Vector2 tamano)
    {
        Image img = CrearImagen(nombre, padre, colorPanel);
        Colocar(img.rectTransform, ancla, posicion, tamano);
        return img.gameObject;
    }

    private static Image CrearImagen(string nombre, Transform padre, Color color)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private TextMeshProUGUI CrearTexto(string nombre, Transform padre, float tamano, TextAlignmentOptions alineacion)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = tamano;
        t.alignment = alineacion;
        t.color = colorTexto;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    // Ancla y pivote en el mismo punto (esquina o borde), posición relativa a él.
    private static void Colocar(RectTransform rt, Vector2 ancla, Vector2 posicion, Vector2 tamano)
    {
        rt.anchorMin = ancla;
        rt.anchorMax = ancla;
        rt.pivot = ancla;
        rt.anchoredPosition = posicion;
        rt.sizeDelta = tamano;
    }

    private static void Estirar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0f, 0.5f);
    }
}
