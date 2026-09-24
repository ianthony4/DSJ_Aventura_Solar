# Planificación y Roles del Equipo (Metodología SUM)

Basado en la metodología ágil SUM, enfocada al desarrollo de videojuegos multidisciplinarios, hemos definido los siguientes roles para los miembros del equipo.

## Asignación de Roles

| Miembro | Rol Principal en SUM | Subroles / Responsabilidades Específicas |
| :--- | :--- | :--- |
| **Anthony** | **Productor Interno (Scrum Master)** | Coordina el proceso, el cronograma y el tablero en GitHub Projects. Lidera la programación principal y la integración del proyecto en Unity. |
| **Richard** | **Equipo de Desarrollo (Level / Game Design)** | Encargado del diseño de los niveles en 3D, disposición de objetos (nave, monedas) y calibración de las mecánicas (velocidad de los enemigos, tiempo de oxígeno). |
| **Marco** | **Verificador Beta (QA) / Desarrollo** | Encargado de realizar pruebas funcionales a las mecánicas implementadas (ej. verificar que los duendes no se atasquen al perseguir) y reportar observaciones de la experiencia. También apoya en tareas de desarrollo. |

*(Nota: En equipos pequeños, todos los miembros pueden compartir responsabilidades de programación, pero los roles aseguran que siempre haya un encargado de la gestión, el diseño y las pruebas).*

## Gestión de Riesgos (Fase Inicial)
Como parte de la metodología SUM, se han identificado los siguientes riesgos que podrían impedir cumplir los objetivos del MVP:

| Riesgo | Probabilidad | Impacto | Mitigación | Plan de Contingencia |
| :--- | :--- | :--- | :--- | :--- |
| Curva de aprendizaje de NavMesh y A* sea más alta de lo esperado. | Media | Alto | Revisar la documentación de Unity y referentes desde el inicio del sprint. | Usar temporalmente Waypoints básicos si NavMesh falla. |
| Bugs críticos de colisión en el terreno del juego base. | Media | Medio | El QA (Marco) realizará testeos exhaustivos del mapa base antes de poner IA. | Bloquear con colisionadores invisibles las zonas problemáticas. |
| Descoordinación en los *commits* del repositorio (Conflictos). | Baja | Alto | Usar ramas (*branches*) separadas para cada característica grande (Ej. `feature/raycast`). | Anthony (Scrum Master) mediará la resolución de conflictos manual. |

## Definition of Done (DoD) - Criterios de "Terminado"
Para que una tarea pase a estado "Hecho", debe cumplir:
1. El código está integrado en la rama principal sin errores de compilación.
2. La funcionalidad fue probada y no introduce bugs críticos.
3. Los *assets* están organizados correctamente en las carpetas de Unity.
4. El alienígena y el jugador pueden navegar el mapa sin atascarse.
5. El *issue* o tarjeta en GitHub Projects está cerrado.
