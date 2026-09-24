# Product Backlog y Planificación de Sprints

El desarrollo de *Solar Adventure: AI Expedition* se enfocará en la progresiva adición de capas de inteligencia artificial a los NPCs, tomando como base el entorno de exploración ya desarrollado por DIVersion Games.

## Plan de Sprints (Roadmap de IA)

### Sprint 1: MVP y Cimentación (Completado / En Curso)
- Restauración del proyecto base funcional (UI, coleccionables, timer).
- Implementación de la Máquina de Estados (FSM) base.
- **IA:** Estado **Patrulla (Wander)** mediante NavMesh o Waypoints.
- **IA:** Detección por proximidad y estado **Perseguir (Seek)**.

### Sprint 2: Percepción Avanzada y Combate (Niveles Tierra/Marte)
- **Percepción (Raycast/FOV):** Reemplazar la detección omnidireccional por un cono de visión real (FOV) y detección de línea de visión (Raycast). El enemigo no debe ver a través de cráteres o paredes.
- **Decisión:** Implementación del estado **Alerta** (cuando el enemigo escucha o ve de reojo al jugador pero no está seguro).
- **Acción:** Ataque básico (quitar oxígeno o vida).

### Sprint 3: Búsqueda y Navegación Dinámica (Niveles Júpiter/Urano)
- **Decisión:** Estado **Buscar** (Búsqueda de la última posición conocida si el jugador rompe la línea de visión).
- **Pathfinding (A*):** Refinamiento de la navegación para asegurar rutas óptimas, evadiendo obstáculos dinámicos si los hay.

### Sprint 4: Extensiones y Comportamiento Avanzado (Neptuno)
- **Cooperación:** Comunicación entre múltiples drones/enemigos para acorralar al jugador.
- **Behavior Trees:** Evaluar el uso de Árboles de Comportamiento (más avanzados que FSM) para programar las fases del Jefe Final.

## Product Backlog (Historias de Usuario Clave)

| ID | Épica | Historia de Usuario | Prioridad | Est. (Pts) | Criterio de Aceptación |
| :--- | :--- | :--- | :--- | :--- | :--- |
| HU-01 | MVP | Como jugador, quiero que el enemigo patrulle de forma autónoma (Wander) por el escenario. | Alta | 5 | El NPC sigue waypoints o navega zonas usando NavMesh. |
| HU-02 | MVP | Como jugador, quiero que el enemigo me persiga (Seek) usando NavMesh al detectarme. | Alta | 5 | Cambia de estado sin atascarse en la geometría del nivel. |
| HU-03 | Combate | Como jugador, quiero recibir daño si el enemigo me alcanza, para tener un reto. | Alta | 3 | La colisión reduce la barra de oxígeno/vida. |
| HU-04 | Percepción | Como NPC, quiero usar Raycast y FOV para detectar al jugador solo si está en mi línea de visión. | Alta | 8 | El jugador puede esconderse detrás de rocas y el NPC no lo detectará. |
| HU-05 | Decisión | Como NPC, quiero pasar al estado "Alerta" antes de perseguir de lleno, dando al jugador una advertencia. | Media | 3 | Un ícono temporal sobre el NPC y pausa de 1 segundo antes de atacar. |
| HU-06 | Navegación | Como NPC, quiero recordar la última posición del jugador si se esconde, e ir a investigar ahí (Estado Buscar). | Media | 5 | Si el jugador se esconde, el NPC va hacia el último punto donde lo vio. |
| HU-07 | IA Avanzada | Como jugador, quiero enfrentarme a enemigos que cooperan para acorralarme. | Baja | 13 | Varios NPCs comparten el *Target* y rodean al jugador. |
