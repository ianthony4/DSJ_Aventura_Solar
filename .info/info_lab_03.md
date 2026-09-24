*Laboratorio de Desarrollo de Software para Juegos*

### Universidad Nacional de San Agustín Escuela de Ingeniería de Sistemas

### Sesión 3: Planificación metodológica del proyecto de videojuego

## I. OBJETIVOS

-Seleccionar y justificar una metodología de desarrollo adecuada para un proyecto de videojuego pequeño y multidisciplinario. -Aplicar correctamente la metodología SUM al proyecto propuesto: concepto, planificación, elaboración iterativa, beta, cierre y gestión de riesgos. -Definir los roles, el backlog de características, los criterios de aceptación, las iteraciones, los hitos y los entregables del proyecto. -Redactar requisitos verificables y trazables, tomando como referencia la norma ISO/IEC/IEEE 29148:2018. -Usar una herramienta de gestión (Trello, Jira, GitHub Projects, Asana u otra) para evidenciar el avance del equipo.

## II. TEMAS A TRATAR

- Planificación del proyecto
- Metodologías Ágiles
## III. MARCO TEÓRICO

## 3.1. Planificación del proyecto

La planificación del proyecto es el conjunto de actividades con las que se inicia la gestión de proyectos de software.

## • Fases más importantes en el desarrollo de videojuegos

o Fase de Concepción o Fase de Diseño o Fase de Planificación o Fase de Producción o Fase de Pruebas o Fase de Distribución o Fase de Mantenimiento

*José Sulla Torres – Roxana Limache*

## 3.2. Metodologías Ágiles

Las metodologías ágiles son aquellas que permiten adaptar la forma de trabajo a las condiciones del proyecto, lo que aporta flexibilidad e inmediatez en la respuesta para amoldar el proyecto y su desarrollo a las circunstancias específicas del entorno.

## 3.2.1. SUM

Metodología ágil para el desarrollo de videojuegos que adapta la estructura y los roles de SCRUM.

**Figura 1.** Ciclo de vida de SUM aplicado al proyecto del curso.

## 3.2.1.1.Fases y productos de trabajo

|Fase SUM|Pregunta que responde|Producto mínimo|Evidencia|
|---|---|---|---|
|Concepto|¿Qué juego vamos a construir y para quién?|Ficha de concepto + propuesta de valor + plataforma + gameplay central.|1 página + boceto/prototipo de concepto.|
|Planificación|¿Cómo lo desarrollaremos y en cuánto tiempo?|Roles, backlog, estimaciones, iteraciones, hitos, riesgos y herramientas.|Tablero de gestión y cronograma.|
|Elaboración|¿Qué incremento jugable construiremos en cada iteración?|Build jugable incremental + funcionalidades aceptadas.|Repositorio + demo por iteración.|
|Beta|¿El juego funciona y la experiencia es aceptable?|Versión beta, plan de pruebas, reporte de defectos y ajustes.|Registro de pruebas y nueva build.|
|Cierre|¿Qué se entrega y qué aprendimos?|Build final, manual breve, retrospectiva y lecciones aprendidas.|Paquete final y acta de cierre.|
|Riesgos|¿Qué puede impedir cumplir los objetivos?|Registro de riesgos con probabilidad, impacto, mitigación y contingencia.|Matriz actualizada en cada iteración.|

|3.2.1.2. Roles de SUM|||
|---|---|---|
|Rol|Responsabilidad|Asignación recomendada en el curso Docente o representante designado|
|Cliente|Define prioridades, valida características y acepta resultados.|por el equipo, según las reglas del curso.|

|Productor interno|Coordina el proceso, los riesgos, el cronograma y la|1 integrante del equipo.|
|---|---|---|
||comunicación; equivalente funcional al Scrum Master en SUM.||
|Equipo de desarrollo Verificador beta|Implementa el videojuego. Puede contener subroles de programación, diseño de juego, arte gráfico y audio. Prueba el juego y reporta defectos y observaciones de la|Resto del equipo, con subroles explícitos. Persona(s) distinta(s) a quien implementó la característica;|

experiencia. idealmente, usuarios externos en Beta.

## 3.2.2. Framework Design, Play, and Experience (DPE)

Orientado a los videojuegos serios.

**Figura 2.** Design, Play y Experience. Framework (DPE).

|3.2.3. • • • • •|Laboratorio de Desarrollo de Software para Juegos 5M Método: Pasos de Producción Machine: Herramientas|Milieu (ambiente): Todos los elementos involucrados en la producción del juego. Manpower (Mano de obra): El equipo de actores humanos Materiales: documentos, modelo de prototipo, archivos, base de datos. Tabla 1: Diferencia de las metodologías para el desarrollo de videojuegos||
|---|---|---|---|
|Elemento|Qué es||Uso correcto en el laboratorio|
|SUM||Metodología ágil específica para el desarrollo de videojuegos, basada en Scrum y en XP.|Metodología principal para aplicar al proyecto del curso.|
|Scrum|productos complejos.|Framework ágil general para el desarrollo de|Sirve como base conceptual; no sustituye los elementos específicos de SUM en esta práctica.|
|DPE|Framework de diseño orientado especialmente a los serious games.||Complementario si el proyecto tiene un propósito educativo, de salud, de entrenamiento u otro objetivo serio.|
|5M|IV. ACTIVIDADES-Ejemplo didáctico aplicado 4.1 Fase de Concepto|Clasificación de factores (método, mano de obra, máquina, materiales, medio/entorno). Proyecto ejemplo: “Lab Escape” — videojuego 2D top-down en el que el jugador debe escapar de un laboratorio mientras guardias patrullan (Wandering) y lo persiguen al detectarlo (Seeking).|Puede apoyar un análisis causal o de recursos, pero no se considerará una metodología ágil de desarrollo de videojuegos.|
|Nombre del juego||Lab Escape||
|Género||Sigilo / acción 2D top-down||
|Público objetivo||Jugadores casuales de 13+ años||
|Plataforma||PC — Windows||
|Motor||Unity 6||
|Core loop||Explorar → evitar guardias → obtener tarjeta → abrir salida → escapar||
|Mecánica diferencial|||Guardias con estados WANDER y SEEK, conectando los algoritmos del Laboratorio 02|
||Criterio de éxito del prototipo 4.2 Backlog inicial de características José Sulla Torres – Roxana Limache|persecución sin errores críticos En SUM, la especificación del videojuego se expresa mediante características priorizadas y criterios de aceptación. Se pueden formular como historias de usuario, siempre que cada una sea verificable.|El jugador puede completar un nivel de 3–5 minutos y el guardia cambia de patrulla a|

|ID|Historia de usuario / característica|Prioridad|Estimación|Criterio de aceptación|Iteración|
|---|---|---|---|---|---|
|HU-01|Como jugador, quiero moverme con WASD para explorar el nivel.|Alta|3 pts|Movimiento en 8 direcciones sin atravesar muros.|1|
|HU-02|Como jugador, quiero que el guardia patrulle de forma autónoma para sentir el escenario vivo.|Alta|5 pts|El guardia usa Wander y no queda detenido ni sale del mapa.|1|
|HU-03|Como jugador, quiero que el guardia me persiga cuando entro en su radio para generar tensión.|Alta|5 pts|Cambia a Seek cuando la distancia sea ≤ al radio y vuelve a Wander al alejarme.|1|
|HU-04|Como jugador, quiero recoger una tarjeta para abrir la puerta de salida.|Alta|5 pts|La tarjeta desaparece, se registra en inventario y se habilita la puerta.|2|
|HU-05|Como jugador, quiero ver una pantalla de victoria al escapar.|Media|3 pts|Se muestran la victoria y la opción de reiniciar.|2|
|HU-06|Como jugador, quiero recibir alertas cuando me detecten.|Baja|3 pts|El audio se reproduce una vez por cada entrada al estado de persecución.|2|

# 4.3 Requisitos: redacción verificable

|Incorrecto|Mejor formulación RF-03: Si el jugador se encuentra a ≤ 180 px del guardia y|Cómo verificar|
|---|---|---|
|||Prueba funcional con medición de|
|“El guardia será inteligente”.|existe línea de visión, el guardia deberá cambiar de WANDER a SEEK en un máximo de 0.2 s.|distancia y transición de estado.|

RNF-01: La escena del nivel deberá iniciarse en ≤ 3 s en el Cronometrar 5 ejecuciones y “El juego cargará rápido”. equipo de referencia del laboratorio. registrar el tiempo máximo. RNF-02: Un usuario nuevo deberá identificar los controles Prueba con 3 usuarios y registro de “El juego será fácil de usar”. básicos sin ayuda externa en ≤ 60 s. tiempo.

## 4.4. Flujo mínimo de una iteración: planificación, construcción, prueba, revisión y ajuste

1) Seleccionar del backlog solo las características que caben en la iteración.
2) Dividir cada característica en tareas técnicas, de arte, de audio, de diseño y de pruebas.
3) Implementar en ramas o commits identificables e integrarlos de frecuencia.
4) Verificar cada característica contra sus criterios de aceptación.
5) Generar una build jugable al final de la iteración.
6) Realizar una revisión con el cliente y actualizar las prioridades y las estimaciones.
7) Realizar una breve retrospectiva: mantener, dejar de hacer y empezar a hacer.
## 4.5. Configuración de la herramienta de gestión

El tablero debe reflejar el trabajo del equipo. Puede usarse Trello, Jira, GitHub Projects, Asana u otra herramienta equivalente.

**Product Backlog Por hacer (iteración) En progreso En prueba Hecho** HU-05 Victoria HU-03 Seek HU-02 Wander HU-01 Movimiento Setup proyecto Regla: una tarjeta solo pasa a “Hecho” cuando cumple su criterio de aceptación y la Definition of Done del equipo.

# 4.6. Definition of Done (DoD) mínima

- Código integrado en la rama principal sin errores de compilación.
- Funcionalidad probada según sus criterios de aceptación.
- No introduce defectos críticos conocidos.
- *Assets* con nombres y carpetas consistentes.
- *Commit* identificado y tablero actualizado.
- *Build* ejecutable disponible cuando corresponda.
## V. Ejercicios

- Desarrolle la metodología para el desarrollo de los videojuegos de su proyecto.
- Realice el informe de su primer avance de su propuesta de videojuego.
- Súbalo al aula virtual.
## VI. Cuestionario

1. ¿Qué significa la Planificación del Proyecto de Desarrollo de Software de Videojuegos?
2. ¿Qué etapas significativas presenta la metodología de desarrollo de videojuegos?
## VII. Bibliografía y referencias

o [https://learn.g2.com/stages-of-game-development](https://learn.g2.com/stages-of-game-development) o ISO/IEC/IEEE. (2018). ISO/IEC/IEEE 29148:2018 — Systems and software engineering — Life cycle processes — Requirements engineering. o Schwaber, K., & Sutherland, J. (2020). The Scrum Guide o Winn, B.M. The Design, Play, and Experience Framework. Handb. Res. Eff. Electron. Gaming Educ. 2009, 5497, 1010–1024
