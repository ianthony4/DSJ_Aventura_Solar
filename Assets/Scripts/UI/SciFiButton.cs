using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Feedback Sci-Fi para botones de UI: crece al pasar el ratón o al seleccionarlo con teclado/mando, se "hunde" al
/// pulsarlo, ilumina un resplandor y cambia el color del texto, con sonidos de hover y clic.
/// Se añade al mismo objeto que el Button. Todo es opcional. Usa tiempo real: funciona con el juego en pausa.
///
/// No toca el color del propio Button (lo sigue gestionando su Color Tint); el resplandor es un Graphic aparte
/// (p. ej. un Image hijo con un borde brillante) cuya opacidad se anima.
/// </summary>
[RequireComponent(typeof(Selectable))]
public class SciFiButton : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
    ISelectHandler, IDeselectHandler, ISubmitHandler
{
    [Header("Escala")]
    [SerializeField] private float hoverScale = 1.06f;
    [SerializeField] private float pressedScale = 0.97f;
    [SerializeField] private float animationSpeed = 14f;

    [Header("Resplandor (opcional)")]
    [Tooltip("Imagen hija (borde o brillo) cuya opacidad sube al resaltar el botón.")]
    [SerializeField] private Graphic glow;
    [Range(0f, 1f)] [SerializeField] private float glowAlphaOn = 1f;
    [Range(0f, 1f)] [SerializeField] private float glowAlphaOff = 0f;

    [Header("Texto (opcional)")]
    [SerializeField] private TMP_Text label;
    [SerializeField] private Color labelNormalColor = new Color(0.78f, 0.88f, 1f, 1f);
    [SerializeField] private Color labelHighlightColor = new Color(0.25f, 0.95f, 1f, 1f);

    [Header("Sonidos (UI: se oyen aunque el juego esté en pausa)")]
    [SerializeField] private AudioCue hoverCue;
    [SerializeField] private AudioCue clickCue;

    private Selectable selectable;
    private Vector3 baseScale = Vector3.one;
    private bool highlighted;
    private bool pressed;

    private void Awake()
    {
        selectable = GetComponent<Selectable>();
        baseScale = transform.localScale;

        PrepareUiCue(hoverCue);
        PrepareUiCue(clickCue);
        ApplyInstant(false);
    }

    private void OnEnable()
    {
        highlighted = false;
        pressed = false;
        ApplyInstant(false);
    }

    private void OnDisable()
    {
        transform.localScale = baseScale;
    }

    // Sonidos 2D y audibles en pausa.
    private static void PrepareUiCue(AudioCue cue)
    {
        if (cue == null) return;
        cue.spatialBlend = 0f;
        cue.ignoreListenerPause = true;
    }

    private void Update()
    {
        float targetScale = pressed ? pressedScale : (highlighted ? hoverScale : 1f);
        float t = 1f - Mathf.Exp(-animationSpeed * Time.unscaledDeltaTime);

        transform.localScale = Vector3.Lerp(transform.localScale, baseScale * targetScale, t);

        if (glow != null)
        {
            Color c = glow.color;
            c.a = Mathf.Lerp(c.a, highlighted ? glowAlphaOn : glowAlphaOff, t);
            glow.color = c;
        }

        if (label != null)
        {
            label.color = Color.Lerp(label.color, highlighted ? labelHighlightColor : labelNormalColor, t);
        }
    }

    private void ApplyInstant(bool isHighlighted)
    {
        if (glow != null)
        {
            Color c = glow.color;
            c.a = isHighlighted ? glowAlphaOn : glowAlphaOff;
            glow.color = c;
        }
        if (label != null) label.color = isHighlighted ? labelHighlightColor : labelNormalColor;
    }

    private bool IsUsable()
    {
        return selectable != null && selectable.IsInteractable();
    }

    private void Highlight()
    {
        if (!IsUsable() || highlighted) return;
        highlighted = true;
        if (hoverCue != null) hoverCue.PlayAt(Vector3.zero);
    }

    public void OnPointerEnter(PointerEventData eventData) { Highlight(); }
    public void OnSelect(BaseEventData eventData) { Highlight(); }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Si el botón sigue seleccionado (teclado/mando), conserva el resaltado.
        bool stillSelected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
        if (!stillSelected) highlighted = false;
        pressed = false;
    }

    public void OnDeselect(BaseEventData eventData)
    {
        highlighted = false;
        pressed = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!IsUsable()) return;
        pressed = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!pressed) return;
        pressed = false;
        if (clickCue != null && IsUsable()) clickCue.PlayAt(Vector3.zero);
    }

    // Enter / A del mando
    public void OnSubmit(BaseEventData eventData)
    {
        if (clickCue != null && IsUsable()) clickCue.PlayAt(Vector3.zero);
    }
}
