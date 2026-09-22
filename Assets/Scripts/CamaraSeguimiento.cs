using UnityEngine;

public class CamaraSeguimiento : MonoBehaviour
{
    [Header("Objetivo a Seguir")]
    public Transform naveObjetivo; // Arrastra aquí tu nave

    [Header("Configuración de Vista")]
    public float velocidadSuavizado = 0.125f; // Entre 0 y 1 (menor es más suave/lento)
    public Vector3 offset; // La distancia entre cámara y nave

    void LateUpdate() // Usamos LateUpdate para que la cámara se mueva DESPUÉS de la nave
    {
        if (naveObjetivo == null) return;

        // 1. Calculamos dónde debería estar la cámara (detrás y arriba de la nave)
        // Usamos TransformPoint para que el "atrás" sea relativo a la rotación de la nave
        Vector3 posicionDeseada = naveObjetivo.TransformPoint(offset);

        // 2. Nos movemos suavemente hacia esa posición (Interpolación)
        Vector3 posicionSuavizada = Vector3.Lerp(transform.position, posicionDeseada, velocidadSuavizado);
        transform.position = posicionSuavizada;

        // 3. (Opcional) Hacemos que la cámara siempre mire a la nave
        transform.LookAt(naveObjetivo);
    }
}