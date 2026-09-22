using UnityEngine;

public class Moneda : MonoBehaviour
{
    [Header("Configuración")]
    public float velocidadGiro = 100f;
    public AudioClip sonidoRecoleccion; // Opcional: Arrastra un sonido aquí si tienes

    void Update()
    {
        // 1. Animación básica: Girar sobre su propio eje
        transform.Rotate(Vector3.up * velocidadGiro * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter(Collider other)
    {
        // 2. Detectar al Astronauta
        // IMPORTANTE: Tu astronauta debe tener el Tag "Player"
        if (other.CompareTag("Player"))
        {
            // 3. Avisar al GameManager
            // Usamos la instancia estática que creamos antes (Singleton)
            if (GameManager.instancia != null)
            {
                GameManager.instancia.SumarMoneda();
            }
            else
            {
                Debug.LogError("¡No encuentro el GameManager! Asegúrate de que existe en la escena.");
            }

            // 4. Sonido (Opcional - Truco para que suene aunque el objeto se destruya)
            if (sonidoRecoleccion != null)
            {
                AudioSource.PlayClipAtPoint(sonidoRecoleccion, transform.position);
            }

            // 5. Desaparecer
            Destroy(gameObject);
        }
    }
}