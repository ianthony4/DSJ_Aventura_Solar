using UnityEngine;

public class ConfigVenus : MonoBehaviour
{
    public Vector3 gravedad = new Vector3(0, -9.81f, 0);
    public float densidadAtmosfera = 2f; // Ahora es variable

    void Start()
    {
        AplicarFisica();
    }

    void AplicarFisica()
    {
        Physics.gravity = gravedad;
        GameObject jugador = GameObject.FindGameObjectWithTag("Player");
        if (jugador != null)
        {
            jugador.GetComponent<Rigidbody>().linearDamping = densidadAtmosfera;
        }
    }
}