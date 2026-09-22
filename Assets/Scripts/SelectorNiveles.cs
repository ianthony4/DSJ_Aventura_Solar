using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class SelectorNiveles : MonoBehaviour
{
 
    [System.Serializable]
    public class DatosPlaneta
    {
        public string nombre;
        [TextArea(3, 10)] 
        public string informacionEducativa;
        public Sprite imagenDelPlaneta;
        public string nombreDeLaEscena; 
       
    }

    
    public DatosPlaneta[] listaDePlanetas; 

    public Image imagenDisplay;
    public TextMeshProUGUI textoNombre;
    public TextMeshProUGUI textoInfo;
    public Button botonJugar;

    private int indiceActual = 0; 

    void Start()
    {
        ActualizarVisuales();
    }

   
    public void SiguientePlaneta()
    {
        indiceActual++;

        if (indiceActual >= listaDePlanetas.Length)
        {
            indiceActual = 0;
        }

        ActualizarVisuales();
    }

    public void PlanetaAnterior()
    {
        indiceActual--;

        
        if (indiceActual < 0)
        {
            indiceActual = listaDePlanetas.Length - 1;
        }

        ActualizarVisuales();
    }

    void ActualizarVisuales()
    {
        DatosPlaneta planetaActual = listaDePlanetas[indiceActual];

        textoNombre.text = planetaActual.nombre;
        textoInfo.text = planetaActual.informacionEducativa;
        imagenDisplay.sprite = planetaActual.imagenDelPlaneta;

        
    }

    public void JugarNivelSeleccionado()
    {
        string escenaACargar = listaDePlanetas[indiceActual].nombreDeLaEscena;
        SceneManager.LoadScene(escenaACargar);
    }
}