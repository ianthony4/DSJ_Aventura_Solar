# Fase de Concepto: Solar Adventure - AI Expedition

## Información General
- **Nombre del Proyecto:** Solar Adventure: AI Expedition
- **Estudio / Equipo:** DIVersion Games (Anthony, Richard, Marco)
- **Género:** Exploración 3D, Sigilo, Acción
- **Motor:** Unity 6
- **Contexto del Proyecto:** El proyecto parte de un juego base funcional (8 planetas, recolección de combustible/monedas, tiempo límite). El objetivo central de esta nueva versión es evolucionar el juego incorporando Inteligencia Artificial avanzada para convertir escenarios estáticos en experiencias dinámicas.

## Bucle Principal (Core Loop)
Explorar entorno planetario -> Percibir y Evadir (o combatir) NPCs inteligentes -> Recolectar recursos (ej. monedas/combustible) -> Sobrevivir al límite de oxígeno/tiempo -> Escapar en la nave.

## Mecánica Diferencial: De Estático a Dinámico
El núcleo del proyecto es la implementación de agentes autónomos (drones/alienígenas). Los enemigos dejarán de ser obstáculos estáticos y seguirán el ciclo lógico: **Percibir → Decidir → Moverse → Atacar → Adaptarse**.

La Arquitectura de IA se compone de:
- **Percepción:** Uso de sensores, *Raycast* y Cono de Visión (*Field of View - FOV*). El enemigo no sabe mágicamente dónde está el jugador.
- **Toma de Decisiones (FSM):** Máquina de Estados Finitos robusta con los estados: *Patrulla, Alerta, Perseguir, Atacar, Buscar*.
- **Navegación:** Pathfinding (A*) y NavMesh para calcular rutas eficientes y esquivar obstáculos geológicos.

## Progresión de Dificultad (Por Planetas)
La complejidad de la IA escalará conforme se avanza de nivel, introduciendo nuevos algoritmos por planeta:
- **Mercurio a Marte:** Introducción gradual de Patrulla -> Detección -> Persecución -> Percepción Avanzada (FOV/Raycast).
- **Júpiter a Neptuno:** Mecánicas complejas como Ataque -> Cooperación entre NPCs -> Búsqueda activa de la última posición conocida -> Enfrentamiento con Jefe final.

## Alcance del MVP (Producto Mínimo Viable)
- 1 escenario funcional completamente integrado con la IA.
- 1 NPC enemigo inteligente implementando Patrulla + Detección.
- Persecución fluida utilizando NavMesh (sin atascos).
- Capacidad de ataque básico.
