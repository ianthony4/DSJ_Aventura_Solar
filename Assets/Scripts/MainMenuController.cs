using UnityEngine;

public class MainMenuController : MonoBehaviour
{
    [Header("Paneles UI")]
 
    public GameObject panelMenuPrincipal;
    public GameObject panelSeleccionNivel; 
    public GameObject panelCreditos;

    void Awake()
    {
        Application.targetFrameRate = 60; // Limita el juego a 60 FPS
    }

    void Start()
    {
        MostrarMenuPrincipal();
    }

    public void MostrarMenuPrincipal()
    {
        Debug.Log("Menu Princiapal");
        panelMenuPrincipal.SetActive(true);
        panelSeleccionNivel.SetActive(false);
        panelCreditos.SetActive(false);
    }

    public void MostrarSeleccionNivel()
    {
        Debug.Log("Mostrando seleccion de nivel");
        panelMenuPrincipal.SetActive(false);
        panelSeleccionNivel.SetActive(true);
        panelCreditos.SetActive(false);
    }

    public void MostrarCreditos()
    {
        Debug.Log("Mostrando creditos");
        panelMenuPrincipal.SetActive(false);
        panelSeleccionNivel.SetActive(false);
        panelCreditos.SetActive(true);
    }


    public void SalirDelJuego()
    {
        Debug.Log("Saliendo de la aplicación..."); 
        Application.Quit();
    }
}
