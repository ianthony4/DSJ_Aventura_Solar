**C**

## Equipo: DIVersion

# Solar Adventure: AI Expedition

Propuesta de Proyecto de Videojuego Desarrollo de Software para Juegos

**Unity + C# NPCs inteligentes A* + FSM + NavMesh**

##### INTEGRANTES:

##### -Chaisa Fernandez, Anthony Leonel

##### -Condo Zamata, Richard Alberto

##### -Ponce de León Aguilar, Marco Antonio

DIVersion Games | Solar Adventure: AI Expedition

|C||
|---|---|
||Antecedente Partimos de un juego funcional ya desarrollado|
|Solar Adventure es un videojuego||
|3D de exploración educativa donde el jugador recorre planetas, recolecta recursos y completa misiones bajo tiempo||
|límite.||
|8 escenarios planetarios Recolección de monedas / combustible Puntuación y tiempo Selector de niveles Scripts ya implementados en Unity||
|DIVersion Games | Solar Adventure: AI Expedition|02|

**C**

#### Problema y oportunidad

La versión actual puede volverse predecible **Limitación** **Oportunidad actual**

El entorno tiene poca reacción autónoma. Agregar NPCs con IA para convertir Los obstáculos no toman decisiones. los niveles en escenarios dinámicos: No hay enemigos que detecten o persigan. el jugador deberá explorar, evadir, La dificultad depende más del tiempo que de combatir y tomar decisiones. estrategia. **Percibir Decidir Actuar**

|C|||
|---|---|---|
|||Nueva propuesta Solar Adventure: AI Expedition Evolucionar el juego base incorporando drones o enemigos inteligentes capaces de patrullar, detectar al jugador, calcular rutas, perseguir, atacar y buscar su última posición conocida. Exploración + IA + Combate El reto principal ya no será construir niveles, sino diseñar comportamientos autónomos para los NPCs.|
|DIVersion Games | Solar Adventure: AI Expedition||04|

**C**

#### Arquitectura de IA

Componentes que controlarán al NPC

**Percepción AI Controller FSM NavMesh**

**Raycast / FOV A* / Ruta NPC**

El NPC no sabrá mágicamente dónde está el jugador: primero percibe, luego decide y finalmente se mueve.

**C**

#### Comportamiento del enemigo

Máquina de Estados Finitos **PATRULLA**

**ALERTA** Patrulla por waypoints Detecta con visión/distancia Persigue con NavMesh Ataca si está cerca Busca si pierde al jugador **BUSCAR PERSEGUIR**

**ATACAR**

**C**

#### Pathfinding con A*

Ruta eficiente evitando obstáculos

|S|.|.|
|---|---|---|
|.|#|.|
|.|#|.|
|.|.|#|
|.|.|.|

|#|.|. El enemigo calcula una|
|---|---|---|
|#|.|. ruta navegable hacia el jugador sin atravesar|
|.|rocas, cráteres o.|. estructuras del planeta.|
|#|.|.|
|.|.|G|

### f(n) = g(n) + h(n)

g(n): costo desde el inicio h(n): estimación hasta la meta f(n): costo total estimado Se recalcula si el jugador cambia de posición

**C**

#### Progresión por planetas

La IA aumenta según el nivel

##### Mercurio Venus Tierra Marte

Patrulla Detección Persecución Raycast/FOV

##### Júpiter Saturno Urano Neptuno

Ataque Cooperación Búsqueda Jefe IA

**C**

#### Referentes y estado del arte

Juegos y técnicas que guían la propuesta

**F.E.A.R.** IA táctica de combate **Pac-Man** FSM clásica **Alien: Isolation** Búsqueda y persecución **A*** Búsqueda de caminos **The Last of Us** Percepción y alerta **Behavior Trees** IA modular avanzada

**C**

#### Alcance del MVP

Primero una versión jugable y defendible

#### MVP Extensiones

1 escenario funcional Tipos de enemigos 1 NPC enemigo inteligente Cooperación entre NPCs Patrulla + detección Predicción del jugador Persecución con NavMesh Behavior Trees Ataque básico Dificultad adaptativa Búsqueda de última posición Jefe final con fases

**C**

#### Cierre

Aporte principal del proyecto

## De un entorno de exploración estático a una experiencia con agentes autónomos.

#### Percibir → Decidir → Moverse → Atacar → Adaptarse

La base del juego ya existe. El valor nuevo del curso será implementar IA clásica de videojuegos con Unity, C#, FSM, A* y NavMesh.

|C||
|---|---|
||Referencias Fuentes para la propuesta|
|[2] Unity Technologies, “AI Navigation,” Unity Manual, 2026.|[1] P. E. Hart, N. J. Nilsson, and B. Raphael, “A Formal Basis for the Heuristic Determination of Minimum Cost Paths,” IEEE Trans. Syst. Sci. Cybern., 1968. [3] M. Iovino et al., “Comparison between Behavior Trees and Finite State Machines,” arXiv:2405.16137, 2024. [4] M. Iovino et al., “A survey of Behavior Trees in robotics and AI,” Robotics and Autonomous Systems, 2022. [5] I. Millington and J. Funge, Artificial Intelligence for Games, CRC Press, 2009. [6] J. Orkin, “Three States and a Plan: The A.I. of F.E.A.R.,” GDC, 2006. Gracias|
|DIVersion Games | Solar Adventure: AI Expedition|12|
