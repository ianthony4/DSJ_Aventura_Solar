using UnityEngine;

/// <summary>
/// Se coloca UNA vez por escena de planeta (junto a Config_Mercurio / ConfiguracionPlaneta) y le dice a todos
/// los morteros qué perfil de artillería usar. Es la pieza que "conecta" la artillería con el planeta.
///
/// Relación con ConfiguracionPlaneta:
///   * ConfiguracionPlaneta (Mercurio) ya fija Physics.gravity. Si el perfil tiene Use World Gravity activo,
///     la artillería usa esa misma gravedad y ambas quedan coherentes.
///   * En planetas sin ConfiguracionPlaneta, el perfil aporta su propia gravedad de artillería sin alterar la
///     física de la nave. Solo si activas "Aplicar Gravedad Al Mundo" se escribe en Physics.gravity.
/// </summary>
public class ConfiguracionArtilleria : MonoBehaviour
{
    [Tooltip("Perfil de artillería de este planeta.")]
    public PerfilArtilleria perfil;

    [Tooltip("Escribe la gravedad del perfil en Physics.gravity (afecta también a la nave). Déjalo desactivado si ya usas ConfiguracionPlaneta.")]
    public bool aplicarGravedadAlMundo = false;

    public static ConfiguracionArtilleria Actual { get; private set; }

    private void Awake()
    {
        Actual = this;

        if (aplicarGravedadAlMundo && perfil != null && !perfil.useWorldGravity)
        {
            Physics.gravity = new Vector3(0f, -Mathf.Abs(perfil.gravity), 0f);
        }
    }

    private void OnDestroy()
    {
        if (Actual == this) Actual = null;
    }
}
