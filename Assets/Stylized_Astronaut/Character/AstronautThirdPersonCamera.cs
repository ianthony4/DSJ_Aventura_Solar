using UnityEngine;
using UnityEngine.InputSystem;

namespace AstronautThirdPersonCamera
{
  /// <summary>
  /// Cámara en tercera persona estilo Roblox:
  ///   * Mantén el CLIC DERECHO y mueve el ratón para girar la cámara alrededor del astronauta (el cursor se oculta
  ///     mientras tanto y reaparece donde estaba al soltar). Sin clic derecho el cursor está libre.
  ///   * RUEDA del ratón: acerca y aleja.
  ///   * La cámara no atraviesa el terreno ni las paredes: se acerca al astronauta si algo la tapa.
  /// El clic izquierdo queda libre para disparar (ver LanzadorGranadas).
  /// </summary>
  public class AstronautThirdPersonCamera : MonoBehaviour
  {
    public Transform lookAt;
    public Transform camTransform;
    [Tooltip("Distancia inicial a la que se coloca la cámara.")]
    public float distance = 5.0f;

    [Header("Órbita (clic derecho)")]
    [Tooltip("Grados que gira la cámara por cada píxel que mueves el ratón.")]
    public float sensibilidad = 0.2f;
    public float anguloMinimo = -10f;
    public float anguloMaximo = 75f;
    [Tooltip("Invierte el eje vertical (ratón arriba = cámara baja).")]
    public bool invertirY = false;

    [Header("Zoom (rueda)")]
    public float distanciaMinima = 1.5f;
    public float distanciaMaxima = 20f;
    [Tooltip("Metros que acerca o aleja cada paso de la rueda.")]
    public float pasoZoom = 1.5f;
    [Tooltip("Rapidez con la que la cámara se aleja de nuevo tras apartarse de un obstáculo.")]
    public float suavizadoZoom = 8f;

    [Header("Punto que mira la cámara")]
    [Tooltip("Metros extra de altura sobre el punto de mira. Si el objetivo tiene CharacterController, el punto de mira ya queda a la altura del pecho.")]
    public float alturaExtra = 0f;

    [Header("Colisión")]
    [Tooltip("Capas que tapan la cámara. Se excluyen siempre las capas Jugador, Enemigo e Ignore Raycast.")]
    public LayerMask capasColision = ~0;
    public float radioColision = 0.25f;

    /// <summary>Giro horizontal actual de la cámara (grados). El astronauta camina respecto a este valor.</summary>
    public float Yaw { get { return yaw; } }
    /// <summary>True mientras se mantiene el clic derecho y se orbita.</summary>
    public bool Orbitando { get { return orbitando; } }

    private const float AnguloInicial = 20f;

    private float yaw;
    private float pitch = AnguloInicial;
    private float distanciaDeseada;
    private float distanciaActual;
    private bool orbitando;
    private bool saltarDelta;
    private Vector2 posicionCursor;
    private CharacterController controladorObjetivo;
    private int mascaraColision;

    private void Start()
    {
        camTransform = transform;

        if (lookAt == null)
        {
            GameObject jugador = GameObject.FindGameObjectWithTag("Player");
            if (jugador != null) lookAt = jugador.transform;
        }
        if (lookAt != null)
        {
            yaw = lookAt.eulerAngles.y; // la partida empieza mirando por detrás del astronauta
            controladorObjetivo = lookAt.GetComponent<CharacterController>();
        }

        distanciaDeseada = Mathf.Clamp(distance, distanciaMinima, distanciaMaxima);
        distanciaActual = distanciaDeseada;

        int excluir = LayerMask.GetMask("Jugador", "Enemigo", "Ignore Raycast");
        mascaraColision = capasColision.value & ~excluir;
    }

    private void OnDisable()
    {
        // Si la cámara se apaga con el clic derecho pulsado, no dejamos el cursor atrapado.
        if (orbitando) SoltarCursor();
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        // Pausa o fin de partida: el menú manda sobre el cursor y la cámara no gira ni hace zoom.
        if (Time.timeScale <= 0f)
        {
            orbitando = false;
            return;
        }

        bool clicDerecho = mouse.rightButton.isPressed;
        if (clicDerecho && !orbitando)
        {
            orbitando = true;
            saltarDelta = true; // el primer movimiento tras bloquear el cursor puede traer un salto
            posicionCursor = mouse.position.ReadValue();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else if (!clicDerecho && orbitando)
        {
            SoltarCursor();
        }
        else if (!clicDerecho && Cursor.lockState != CursorLockMode.None)
        {
            // Fuera del clic derecho el cursor debe estar libre (p. ej. tras cerrar el menú de pausa).
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (orbitando)
        {
            if (saltarDelta)
            {
                saltarDelta = false;
            }
            else
            {
                Vector2 delta = mouse.delta.ReadValue();
                yaw += delta.x * sensibilidad;
                pitch += (invertirY ? delta.y : -delta.y) * sensibilidad;
                pitch = Mathf.Clamp(pitch, anguloMinimo, anguloMaximo);
            }
        }

        // La rueda devuelve 120 por paso en Windows y 1 en otras plataformas.
        float rueda = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(rueda) > 0.01f)
        {
            float pasos = Mathf.Abs(rueda) > 10f ? rueda / 120f : rueda;
            distanciaDeseada = Mathf.Clamp(distanciaDeseada - pasos * pasoZoom, distanciaMinima, distanciaMaxima);
        }
    }

    private void LateUpdate()
    {
        if (lookAt == null) return;

        Vector3 pivote = PuntoDeMira();
        Quaternion rotacion = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 haciaCamara = rotacion * Vector3.back;

        // Colisión: si algo tapa el camino, la cámara se acerca de golpe; al despejarse, se aleja con suavidad.
        float distanciaLibre = distanciaDeseada;
        RaycastHit hit;
        if (Physics.SphereCast(pivote, radioColision, haciaCamara, out hit, distanciaDeseada, mascaraColision, QueryTriggerInteraction.Ignore))
        {
            distanciaLibre = Mathf.Max(distanciaMinima * 0.3f, hit.distance);
        }

        if (distanciaLibre < distanciaActual)
        {
            distanciaActual = distanciaLibre;
        }
        else
        {
            distanciaActual = Mathf.Lerp(distanciaActual, distanciaLibre, 1f - Mathf.Exp(-suavizadoZoom * Time.deltaTime));
        }

        camTransform.SetPositionAndRotation(pivote + haciaCamara * distanciaActual, rotacion);
    }

    // Altura del pecho del astronauta (o la del propio objetivo si no tiene CharacterController).
    private Vector3 PuntoDeMira()
    {
        Vector3 p = lookAt.position + Vector3.up * alturaExtra;
        if (controladorObjetivo != null)
        {
            p += Vector3.up * (controladorObjetivo.center.y + controladorObjetivo.height * 0.3f);
        }
        return p;
    }

    private void SoltarCursor()
    {
        orbitando = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (Mouse.current != null) Mouse.current.WarpCursorPosition(posicionCursor);
    }
  }
}
