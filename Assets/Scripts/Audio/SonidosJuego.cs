using UnityEngine;

/// <summary>
/// Los sonidos del juego, todos en un solo lugar. Se edita en el Inspector:
///   menú  Solar Adventure > Sonido > Abrir ajustes de sonido   (o el archivo Assets/Resources/SonidosJuego.asset)
///
/// Arrastra tus sonidos a cada espacio (o usa  Solar Adventure > Sonido > Asignar sonidos de la carpeta Sounds,
/// que rellena por nombre los espacios vacíos). Un espacio vacío no suena y no da error. Los cambios valen para
/// TODOS los enemigos y planetas, incluidos los que añadas después: no hay que tocar cada prefab.
/// En cada espacio puedes poner varios clips: en los sonidos de una sola vez (disparo, alarma) se elige uno al azar
/// en cada reproducción; en los que se repiten (fondo, pasos, motor, zumbido) se elige uno al aparecer el objeto.
///
/// Un enemigo, un mortero, un dron o el jugador concreto puede tener su sonido propio rellenando el campo de audio
/// de su prefab: ese manda sobre este.
/// </summary>
[CreateAssetMenu(menuName = "Solar Adventure/Sonidos del juego", fileName = "SonidosJuego")]
public class SonidosJuego : ScriptableObject
{
    /// <summary>Cantidad de espacios de sonido (el fondo cuenta uno, tenga o no fondos por planeta).</summary>
    public const int TotalEspacios = 10;

    /// <summary>Fondo propio de un planeta.</summary>
    [System.Serializable]
    public class FondoDePlaneta
    {
        [Tooltip("Parte del nombre de la escena: Mercury, Venus, Earth, Mars... No distingue mayúsculas.")]
        public string escena = "Mercury";
        public AudioCue sonido = new AudioCue { volume = 0.4f, spatialBlend = 0f, pitchRange = new Vector2(1f, 1f) };
    }

    [Header("1 · Fondo")]
    [Tooltip("Fondo de los planetas que no tienen uno propio (abajo). Se repite sin parar mientras juegas; no suena en el menú principal. Es 2D: se oye igual en todas partes.")]
    public AudioCue fondo = new AudioCue { volume = 0.4f, spatialBlend = 0f, pitchRange = new Vector2(1f, 1f) };
    [Tooltip("Fondo propio de cada planeta: manda sobre el general en esa escena. Con varios clips se elige uno al empezar.")]
    public FondoDePlaneta[] fondosPorPlaneta = new FondoDePlaneta[0];

    [Header("2 · Enemigos (arácnidos)")]
    [Tooltip("Pasos. Se repite mientras el enemigo camina: más rápido si corre y en silencio si está parado o muerto.")]
    public AudioCue pasosEnemigo = new AudioCue { volume = 0.6f, minDistance = 3f, maxDistance = 40f, pitchRange = new Vector2(0.95f, 1.05f) };

    [Header("3 · Mortero")]
    [Tooltip("Disparo. Suena cada vez que el mortero lanza un proyectil.")]
    public AudioCue disparoMortero = new AudioCue { volume = 1f, minDistance = 10f, maxDistance = 120f };
    [Tooltip("Movimiento. Se repite mientras el mortero se desplaza entre nidos: en silencio si está quieto o apuntando.")]
    public AudioCue movimientoMortero = new AudioCue { volume = 0.7f, minDistance = 6f, maxDistance = 60f, pitchRange = new Vector2(0.95f, 1.05f) };

    [Tooltip("Impacto. Suena donde cae y explota cada proyectil del mortero.")]
    public AudioCue impactoMortero = new AudioCue { volume = 1f, minDistance = 12f, maxDistance = 140f };

    [Header("4 · Dron observador")]
    [Tooltip("Patrulla. Zumbido o escaneo que se repite mientras el dron vigila; se apaga cuando te detecta.")]
    public AudioCue dronPatrulla = new AudioCue { volume = 0.5f, minDistance = 4f, maxDistance = 45f, pitchRange = new Vector2(1f, 1f) };
    [Tooltip("Detección. Alarma que suena una vez cuando el dron te detecta.")]
    public AudioCue dronDetecta = new AudioCue { volume = 1f, minDistance = 10f, maxDistance = 90f };

    [Header("5 · Jugador")]
    [Tooltip("Impacto del disparo del jugador. Suena donde explota tu granada.")]
    public AudioCue impactoJugador = new AudioCue { volume = 1f, minDistance = 10f, maxDistance = 120f };
    [Tooltip("Pisadas. Se repite mientras el astronauta camina por el suelo; en silencio si está parado, en el aire o en pausa. Es 2D.")]
    public AudioCue pasosJugador = new AudioCue { volume = 0.6f, spatialBlend = 0f, pitchRange = new Vector2(0.97f, 1.03f) };
    [Tooltip("Rapidez (m/s) a la que las pisadas suenan a su tono normal. Más rápido que esto suenan más agudas.")]
    public float velocidadReferenciaPasosJugador = 8f;
    [Tooltip("Disparo del arma del jugador. Suena al lanzar una granada.")]
    public AudioCue disparoJugador = new AudioCue { volume = 0.9f, spatialBlend = 0.3f, minDistance = 10f, maxDistance = 100f };

    /// <summary>Lo usa el Editor para saber si ya rellenó los espacios nuevos con los archivos de Sounds (no tocar).</summary>
    [HideInInspector] public int versionAsignacion;

    private static SonidosJuego actual;
    private static bool buscado;

    /// <summary>Los ajustes de sonido (Assets/Resources/SonidosJuego.asset). Null si el archivo no existe: entonces no suena nada.</summary>
    public static SonidosJuego Actual
    {
        get
        {
            if (actual == null && (!buscado || Application.isEditor))
            {
                buscado = true;
                actual = Resources.Load<SonidosJuego>("SonidosJuego");
            }
            return actual;
        }
    }

    /// <summary>El fondo que corresponde a una escena: el de su planeta si lo tiene, si no el general.</summary>
    public AudioCue FondoPara(string nombreEscena)
    {
        if (fondosPorPlaneta != null && !string.IsNullOrEmpty(nombreEscena))
        {
            string n = nombreEscena.ToLowerInvariant();
            for (int i = 0; i < fondosPorPlaneta.Length; i++)
            {
                FondoDePlaneta f = fondosPorPlaneta[i];
                if (f == null || f.sonido == null || !f.sonido.HasClip || string.IsNullOrEmpty(f.escena)) continue;
                if (n.Contains(f.escena.ToLowerInvariant())) return f.sonido;
            }
        }
        return fondo;
    }

    /// <summary>Cuántos de los espacios tienen sonido asignado.</summary>
    public int Asignados
    {
        get
        {
            int n = 0;
            bool hayFondo = fondo.HasClip;
            if (!hayFondo && fondosPorPlaneta != null)
            {
                for (int i = 0; i < fondosPorPlaneta.Length; i++)
                {
                    if (fondosPorPlaneta[i] != null && fondosPorPlaneta[i].sonido != null && fondosPorPlaneta[i].sonido.HasClip) hayFondo = true;
                }
            }
            if (hayFondo) n++;
            if (pasosEnemigo.HasClip) n++;
            if (disparoMortero.HasClip) n++;
            if (impactoMortero.HasClip) n++;
            if (movimientoMortero.HasClip) n++;
            if (dronPatrulla.HasClip) n++;
            if (dronDetecta.HasClip) n++;
            if (pasosJugador.HasClip) n++;
            if (disparoJugador.HasClip) n++;
            if (impactoJugador.HasClip) n++;
            return n;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reiniciar()
    {
        actual = null;
        buscado = false;
    }
}
