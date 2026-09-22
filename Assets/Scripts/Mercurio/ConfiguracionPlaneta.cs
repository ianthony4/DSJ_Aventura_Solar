using UnityEngine;

public class ConfiguracionPlaneta : MonoBehaviour
{
    [Header("Datos Científicos de Mercurio")]
    public Vector3 gravedadMercurio = new Vector3(0, -3.7f, 0); // Gravedad baja
    public bool tieneAtmosfera = false;

    // Configuracion de Niebla (Visual)
    public Color colorNiebla = Color.black;
    public float densidadNiebla = 0f;

    void Start()
    {
        AplicarFisica();
        AplicarVisuales();
    }

    void AplicarFisica()
    {
        // 1. Cambiamos la gravedad global
        Physics.gravity = gravedadMercurio;

        // 2. Buscamos al jugador para ajustar su resistencia al aire
        GameObject jugador = GameObject.FindGameObjectWithTag("Player");

        if (jugador != null)
        {
            // CORRECCIÓN UNITY 6: Usamos linearDamping en vez de drag
            // En el vacío (Mercurio) es 0. En la Tierra sería aprox 0.5.
            jugador.GetComponent<Rigidbody>().linearDamping = 0f;
        }
    }

    void AplicarVisuales()
    {
        // Mercurio no tiene niebla
        RenderSettings.fog = false;
        RenderSettings.fogColor = colorNiebla;
        RenderSettings.fogDensity = densidadNiebla;

        // Ajustar intensidad del ambiente para sombras negras
        RenderSettings.ambientIntensity = 0.5f;
    }
}