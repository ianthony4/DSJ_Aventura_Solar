using UnityEngine;

/// <summary>
/// Perfil de artillería de un planeta (ScriptableObject). Un asset por planeta: cada bioma se siente distinto
/// sin tocar código. Los morteros de la escena lo leen al iniciar (vía ConfiguracionArtilleria o su campo
/// "Profile Override") y copian estos valores a sus propios campos.
///
/// Los valores NO tienen que ser físicamente realistas: están pensados para el Game Feel.
///   * La forma del arco depende de gravedad y velocidad horizontal:  altura ≈ g·t²/8,  t ≈ distancia / velocidad.
///     Gravedad baja + velocidad lenta  -> arcos altos y lentos (mucho tiempo para reaccionar).
///     Gravedad alta + velocidad alta   -> disparos rápidos, pesados y casi planos.
///   * El viento curva la trayectoria; el mortero lo compensa, pero el jugador lo ve en la parábola del aviso.
///   * Precisión de adelanto y dispersión regulan la dificultad sin tocar la física.
/// Crea los 8 perfiles ya rellenos con el menú  Solar Adventure > Crear perfiles de artillería.
/// </summary>
[CreateAssetMenu(menuName = "Solar Adventure/Perfil de Artillería", fileName = "Perfil_Planeta")]
public class PerfilArtilleria : ScriptableObject
{
    [Header("Identidad")]
    public string planeta = "Planeta";
    [TextArea(2, 4)] public string notasDeDiseno;

    [Header("Gravedad de artillería")]
    [Tooltip("Si está activo usa Physics.gravity (p. ej. la que ya fija ConfiguracionPlaneta en Mercurio).")]
    public bool useWorldGravity = false;
    [Tooltip("Gravedad propia de la artillería en m/s² (positiva). Solo se usa si Use World Gravity está desactivado.")]
    public float gravity = 3.7f;

    [Header("Proyectil")]
    [Tooltip("Velocidad horizontal nominal (m/s). Debe superar la velocidad de la nave para poder alcanzarla.")]
    public float shellHorizontalSpeed = 12f;
    [Tooltip("Velocidad de lanzamiento máxima: limita el alcance.")]
    public float maxLaunchSpeed = 40f;
    [Tooltip("Tiempo de vuelo mínimo: es el aviso mínimo que recibe el jugador.")]
    public float minFlightTime = 1.2f;
    public float maxFlightTime = 8f;
    [Tooltip("Viento: aceleración horizontal constante (m/s²). Y se ignora. Mantén |viento| x tiempo de vuelo por debajo de ~60 % de la velocidad horizontal.")]
    public Vector3 wind = Vector3.zero;

    [Header("Explosión")]
    public float blastRadiusMultiplier = 1f;
    public float damageMultiplier = 1f;

    [Header("Combate")]
    [Tooltip("Mantenerlo >= Min Flight Time x Shell Horizontal Speed.")]
    public float engageMinRange = 15f;
    public float engageMaxRange = 45f;
    [Tooltip("Segundos de aviso (cañón apuntando) antes de disparar.")]
    public float lockOnTime = 1.2f;
    public int salvoSize = 3;
    public float shotInterval = 1f;
    public float salvoCooldown = 4f;

    [Header("Dificultad (Game Feel)")]
    [Range(0f, 1f)]
    [Tooltip("1 = adelanta al jugador perfectamente; 0 = dispara a donde está ahora. Planetas iniciales más bajo.")]
    public float leadAccuracy = 1f;
    [Tooltip("Dispersión aleatoria (m) del punto de impacto por disparo: genera fallos por poco.")]
    public float aimScatter = 0f;

    [Header("Cráteres (pathfinding)")]
    [Tooltip("Cada explosión recorta el NavMesh un rato: los enemigos rodean la zona y A* la evita.")]
    public bool crateresBloqueanRuta = true;
    [Tooltip("Radio del cráter respecto al radio de la explosión.")]
    [Range(0f, 1f)] public float radioCraterFactor = 0.45f;
    [Tooltip("Segundos que dura el cráter.")]
    public float duracionCrater = 15f;
    [Tooltip("Dibuja un disco oscuro en el suelo (desactívalo si el proyectil ya tiene Scorch Prefab).")]
    public bool mostrarMarcaCrater = true;
}
