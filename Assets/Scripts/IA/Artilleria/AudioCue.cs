using UnityEngine;

/// <summary>
/// "Punto de anclaje" de audio reutilizable: se declara como campo serializado y en el Inspector se asignan
/// uno o varios clips. Si no hay clips, no hace nada (puedes dejar los anclajes vacíos hasta tener los sonidos).
/// Cada reproducción crea un AudioSource temporal en la posición indicada, con variación de tono para que
/// los disparos repetidos no suenen idénticos.
///
/// Uso:  [SerializeField] private AudioCue fireCue;   ...   fireCue.PlayAt(transform.position);
///
/// También sirve para sonidos en BUCLE (pasos, zumbidos, música): Aplicar(fuente, true) configura un AudioSource
/// con este volumen, tono y distancia, y el que lo usa lo enciende, lo apaga o le sube y baja el volumen.
/// </summary>
[System.Serializable]
public class AudioCue
{
    [Tooltip("Clips posibles; se elige uno al azar en cada reproducción.")]
    public AudioClip[] clips;
    [Range(0f, 1f)] public float volume = 1f;
    [Tooltip("Rango de tono (pitch) aleatorio. 1 = original.")]
    public Vector2 pitchRange = new Vector2(0.95f, 1.05f);
    [Tooltip("0 = sonido 2D (UI), 1 = totalmente espacial.")]
    [Range(0f, 1f)] public float spatialBlend = 1f;
    public float minDistance = 5f;
    public float maxDistance = 80f;
    [Tooltip("Actívalo para sonidos de UI que deben oírse con el juego en pausa (AudioListener.pause).")]
    public bool ignoreListenerPause;

    /// <summary>True si hay al menos un clip asignado (los huecos vacíos de la lista no cuentan).</summary>
    public bool HasClip
    {
        get
        {
            if (clips == null) return false;
            for (int i = 0; i < clips.Length; i++) if (clips[i] != null) return true;
            return false;
        }
    }

    /// <summary>Un clip al azar de los asignados (null si no hay ninguno).</summary>
    public AudioClip Elegir()
    {
        if (!HasClip) return null;
        for (int intento = 0; intento < 8; intento++)
        {
            AudioClip c = clips[Random.Range(0, clips.Length)];
            if (c != null) return c;
        }
        for (int i = 0; i < clips.Length; i++) if (clips[i] != null) return clips[i];
        return null;
    }

    /// <summary>Configura un AudioSource con los ajustes de este anclaje. No lo enciende: eso lo hace quien lo usa.</summary>
    public void Aplicar(AudioSource source, bool bucle)
    {
        source.clip = Elegir();
        source.loop = bucle;
        source.volume = volume;
        source.pitch = Random.Range(Mathf.Min(pitchRange.x, pitchRange.y), Mathf.Max(pitchRange.x, pitchRange.y));
        source.spatialBlend = spatialBlend;
        source.minDistance = minDistance;
        source.maxDistance = Mathf.Max(maxDistance, minDistance + 0.1f);
        source.rolloffMode = AudioRolloffMode.Linear;
        source.dopplerLevel = 0f;                 // sin cambios de tono por velocidad: los enemigos se mueven rápido
        source.ignoreListenerPause = ignoreListenerPause;
        source.playOnAwake = false;
    }

    public void PlayAt(Vector3 position)
    {
        if (!HasClip) return;

        AudioClip clip = Elegir();
        if (clip == null) return;

        GameObject host = new GameObject("AudioCue_" + clip.name);
        host.transform.position = position;

        AudioSource source = host.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = volume;
        source.pitch = Random.Range(Mathf.Min(pitchRange.x, pitchRange.y), Mathf.Max(pitchRange.x, pitchRange.y));
        source.spatialBlend = spatialBlend;
        source.minDistance = minDistance;
        source.maxDistance = Mathf.Max(maxDistance, minDistance + 0.1f);
        source.rolloffMode = AudioRolloffMode.Linear;
        source.ignoreListenerPause = ignoreListenerPause;
        source.playOnAwake = false;
        source.Play();

        float duration = clip.length / Mathf.Max(0.1f, Mathf.Abs(source.pitch));
        Object.Destroy(host, duration + 0.1f);
    }
}
