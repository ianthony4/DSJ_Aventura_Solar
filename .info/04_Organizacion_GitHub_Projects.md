# Guía Práctica: Configuración de GitHub Projects (Kanban y Sprints)

Esta guía paso a paso te ayudará a configurar manualmente el tablero en GitHub Projects para el equipo **DIVersion Games**, organizar el trabajo en **Sprints** y designar las tareas correspondientes a Anthony, Richard y Marco.

## Paso 1: Crear el Tablero (Project)
1. Ve a la página principal de tu repositorio en GitHub: `DSJ_Aventura_Solar`.
2. Haz clic en la pestaña **Projects** y luego en **New Project**.
3. Selecciona la plantilla **Board** (Tablero Kanban).
4. Ponle como nombre: `Solar Adventure: AI Expedition - Scrum Board`.

## Paso 2: Configurar las Columnas y Campos de Sprint
Por defecto, GitHub te da *To Do*, *In Progress* y *Done*. Debes adaptar las columnas a la metodología SUM:

1. Renombra y crea las siguientes columnas (Status):
   - **Backlog:** Todo lo que se hará a futuro (Ej. Árboles de comportamiento del Jefe).
   - **To Do (Sprint Actual):** Tareas del sprint en curso que aún no se empiezan.
   - **In Progress:** Tareas en las que alguien está trabajando *ahora mismo*.
   - **Code Review / QA:** Tareas terminadas en código que necesitan ser probadas (ideal para que Marco verifique que no haya bugs).
   - **Done:** Tarea finalizada, probada e integrada.

2. **Habilitar Sprints (Iteraciones):**
   - En tu Project de GitHub, ve a *Settings* (el icono de engranaje) -> *Custom fields* -> Crea un nuevo campo seleccionando el tipo **Iteration**.
   - Nómbralo **Sprint**. Automáticamente GitHub creará "Sprint 1", "Sprint 2", etc. Puedes cambiarles el nombre luego (Ej: *Sprint 1: MVP Tierra*).
   - Asegúrate de mostrar este campo en la vista de tu tablero.

3. **Crear Etiquetas (Labels):**
   - Ve a la pestaña de *Issues* -> *Labels* en tu repositorio y crea etiquetas con colores: `IA`, `NavMesh`, `Bug`, `QA`, `Diseño de Nivel`, `UI`.

## Paso 3: Crear Tareas (Issues) y Asignarlas
Para cada Historia de Usuario, debes crear un **Issue** en tu repositorio, asignarle un responsable, una etiqueta, el Sprint actual, y agregarlo al Project.

### Ejemplo de Designación de Tareas (Para el Sprint 1)

Basado en los roles (Anthony = Programación/Scrum Master, Richard = Level Design, Marco = QA), así debes crear las primeras tarjetas:

| Título del Issue (Tarea) | Descripción a poner en el Issue | Asignado a (Assignee) | Etiqueta (Label) |
| :--- | :--- | :--- | :--- |
| **Configurar NavMesh en el nivel Tierra** | "Hornear (Bake) la superficie del mapa base con NavMesh para que la IA pueda caminar sin atravesar rocas ni montañas." | **Richard** | `Diseño de Nivel`, `NavMesh` |
| **Script: Máquina de Estados (FSM) Base** | "Crear el esqueleto del script en C# (`EnemyAI.cs`) para manejar los cambios de estados del enemigo." | **Anthony** | `IA` |
| **Estado: Wander (Patrullar)** | "Hacer que el dron enemigo se mueva aleatoriamente por el mapa usando `NavMeshAgent`." | **Anthony** | `IA`, `NavMesh` |
| **Testing: Colisiones y Patrullaje fluido** | "Probar el mapa de Richard y el script de Anthony para asegurar que el duende patrulle sin quedarse atascado en ningún cráter." | **Marco** | `QA`, `Bug` |
| **Estado: Seek (Perseguir)** | "Si la distancia al jugador es < 15m, el enemigo cambia de Wander a Seek y lo persigue." | **Anthony** | `IA` |
| **Colocar recursos y nave de escape** | "Posicionar 10 monedas y la zona de victoria en el nivel Tierra. Ajustar colliders." | **Richard** | `Diseño de Nivel` |

## Paso 4: El Flujo de Trabajo (Para la presentación)
Al docente le interesará ver que el equipo usa el tablero correctamente. Este es el flujo que deben seguir:

1. **Mover las tarjetas:** Cuando Anthony empiece a programar el FSM, entra al GitHub y arrastra su tarjeta de *To Do* a *In Progress*.
2. **Revisión:** Cuando termina el script, Anthony no la pasa directo a Done; mueve la tarjeta a **Code Review / QA**.
3. **Pruebas (El rol de Marco):** Marco ve que hay una tarjeta en QA. Baja los cambios a su Unity, juega el nivel e intenta que el enemigo se atasque. 
   - *Si se atasca:* Marco comenta el error en el issue y devuelve la tarjeta a *In Progress* para que Anthony lo arregle.
   - *Si funciona perfecto:* Marco la aprueba y la arrastra a **Done**.

Esta organización evidenciará control total sobre el proyecto, trabajo en equipo real por sprints, y la correcta aplicación de roles metodológicos.
