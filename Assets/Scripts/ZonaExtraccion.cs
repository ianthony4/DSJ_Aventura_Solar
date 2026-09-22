using UnityEngine;

public class ZonaExtraccion : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // 1. Verificar si es el astronauta
        if (other.CompareTag("Player"))
        {
            // 2. Comunicarse con el GameManager
            if (GameManager.instancia != null)
            {
                // Esta función revisa si tienes 7 monedas o más
                // Si las tienes, lanza la victoria. Si no, no hace nada.
                GameManager.instancia.IntentarTerminarMision();
            }
        }
    }
}
