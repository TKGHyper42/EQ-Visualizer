# EQ Visualizer — Bitácora técnica

> Registro interno del desarrollo. La primera versión pública prevista es **1.0.0**; las denominaciones V2 y V3 son etapas internas, no lanzamientos públicos.
>
> **Estado actual:** híbrido V2+V3 en validación (2026-11-12). Este documento no equivale a una certificación de funcionamiento en una PC real.

## 2026-11-13 — Correcciones locales de transiciones y Ajustes

- La primera ejecución muestra Bienvenida antes de crear y adjuntar el wallpaper a Explorer. Bienvenida ahora aparece con el icono de EQ en la barra de tareas, evitando que quede oculta detrás de otras ventanas mientras el render está pausado por una ventana maximizada. WindowsReview cubre la presencia en la barra y el orden de arranque.

- El menú Debug de la bandeja muestra FPS de presentación, estado y renderizador por monitor, además de CPU y RAM del proceso. Las muestras se actualizan cada segundo mientras el panel está abierto.

- Se reutilizan direcciones de barras circulares y vértices compartidos al construir la escena. La comparación local produjo los mismos vértices con y sin túnel. CoreChecks y la prueba x64 con Direct3D por hardware pasaron.

- Ajustes usa el icono asociado al ejecutable y EQ registra una identidad estable para la barra de tareas.
- Portada incluye la opacidad del fondo del recuadro de título y artista y una sombra opcional para el texto. Audio incluye cuatro modos de degradado y un control de intensidad para ambos estilos de barras. Los controles se reflejan en la vista previa.

- Seguimiento del rebote de portada: durante los cambios de canción con EQ enlazado a Explorer, se actualiza la superficie animada y se conserva el fondo estático de respaldo. Si se pierde el enlace con Explorer, se genera un respaldo de la canción activa.
- Guardar cambios aplica la configuración y mantiene Ajustes abierto. Cancelar descarta las ediciones posteriores al último guardado.

- Base comprobada y respaldada antes de editar: `43aae9c`, rama `patch/v2-v3-win11-x64-20261111`.
- Reproducción: Spotify emitió dos eventos de metadatos de Deutschland en 80 ms. El fondo estático se escribió aproximadamente cuatro segundos después. La causa completa del salto visual requiere validación interactiva.
- Metadatos: las notificaciones con el mismo título, artista y contenido de portada conservan la escena existente. Un cambio real de portada continúa actualizándola. Los estados transitorios Changing y Opened conservan el último estado de reproducción confirmado.
- Fondo estático: renderizado de una copia independiente en segundo plano, descarte de resultados atrasados y restauración asíncrona durante la reproducción. Al salir se espera la restauración. Tras aplicar un fondo se solicita repintar las superficies y revisar su posición bajo los iconos.
- Ajustes: apertura como ventana independiente, con una sola instancia y cierre explícito al guardar o cancelar. Se evita el bucle modal que deshabilita otras ventanas del proceso.
- Dibujo: corregidos los rectángulos de tamaño cero o negativo y el radio cero. Una prueba reprodujo ArgumentException en GraphicsPath.AddArc antes de la corrección.
- Regresiones: dimensiones mínimas, iconos nativos pequeño y grande, metadatos duplicados, estados transitorios, restauración pendiente supersedida y renderizado estático mientras se reemplaza la escena.
- Verificación local: CoreChecks y WindowsReview aprobados, incluidos todos los temas, secciones y regresiones nuevas. Publicación Windows x64 correcta. Smoke test del ejecutable publicado aprobado con Direct3D por hardware, GDI+, blur, bienvenida, maximizar/minimizar/restaurar y guardar/cancelar Ajustes.
- Prueba real: compilación corregida ejecutándose. Cambio observado a Ich will: un único cambio de archivo aproximadamente 400 ms después del primer registro del título. WindowFromPoint devolvió SysListView32 durante esa observación. Cancelar cerró Ajustes por su ruta real.
- Aceptación pendiente: salto visual completo entre pistas, clics del escritorio durante las transiciones, presentación real del icono en la barra de tareas, RTSS y audio real con distintas fuentes.

## 2026-11-11 — Híbrido V2 + V3, revisión final

- **Rama:** `patch/v2-v3-win11-x64-20261111` (sin fusión con `main`).
- **Commit comprobado:** `ab3956f7e72eae002852367f071b7a8899fcd52d`.
- **Base:** motor Direct3D 11 / superficie Windows de V2, con interfaz, geometría y ajustes de V3.
- **Funciones integradas en código:** visualizador circular e inferior; estilos independientes (altura, grosor, opacidad, extremos y ancho de franja); túnel; paralaje del cursor; bienvenida y ajustes; GDI+ de compatibilidad.
- **Corrección:** selección de modo al reabrir Bienvenida, inicializando los radio buttons después de pertenecer al mismo contenedor (`811572f`).
- **Prueba agregada:** regresión de los tres modos y persistencia de la selección (`ab3956f`).
- **CI:** [ejecución 43002963607](https://github.com/TKGHyper42/EQ-Visualizer/actions/runs/43002963607) — `success`: pruebas de geometría, revisión Windows en runner, publicación y smoke test con renderizador hardware.
- **Artefacto privado:** [EQVisualizer-win-x64, ID 13847007291](https://github.com/TKGHyper42/EQ-Visualizer/actions/runs/43002963607/artifacts/13847007291).

### Verificaciones pendientes antes de 1.0.0

1. Probar captura de audio real y respuesta de las barras con un reproductor (incluido Spotify).
2. Comprobar que Explorer y los iconos mantengan clic simple, doble clic y menú contextual.
3. Confirmar pausa en pantalla completa, bloqueo, restauración de sesión y cambios de monitor.
4. Medir FPS, CPU, GPU y RAM durante una sesión prolongada en Windows 11.
5. Revisar RTSS/overlays y las opciones de bienvenida en el dispositivo real.

**Importante:** los smoke tests no ejercitan audio real ni la integración interactiva completa con Explorer. Estado: **candidato a prueba; no liberación estable**.

## 2026-11-11 — Integración inicial

- **Commit:** `c23e182e37d83fb2b4b0b6bb88148ccf9d572003`.
- Integración aislada del renderizador de V2 con el trabajo de V3, conservando `main`.
- [CI 42996282595](https://github.com/TKGHyper42/EQ-Visualizer/actions/runs/42996282595): aprobado.
- Faltaban pruebas adicionales de la ventana Bienvenida, añadidas posteriormente.

## 2026-11-10 a 2026-11-11 — Rescate y diagnóstico

- Se preservó el trabajo V2 de Codex en `rescue/codex-v2-wip-20261111`, commit `e448bd8`. Estado: copia de rescate, no release.
- En las pruebas de V3 anteriores al híbrido, se reportó apertura fluida pero ausencia del visualizador, del túnel, del paralaje y de la bienvenida. La integración V2+V3 se inició para recuperar esas funciones sin sustituir la versión principal.
- Historial y resultados reproducibles: commits y ejecuciones de GitHub Actions en el repositorio privado.

## Política de versiones y control de cambios

- `main`: versión original conservada; no fusionar sin autorización.
- `patch/v2-v3-win11-x64-20261111`: candidato interno; no crear ramas por auditoría o parche menores.
- Versiones públicas: empezar por `1.0.0` **solo** tras aceptación funcional.
- Registrar cada corrección futura con fecha, síntoma, causa, commit, prueba y resultado; distinguir claramente **verificado por CI**, **verificado en Windows real** y **pendiente**.
- La app **TKG Backlog** presenta una copia de lectura de los hitos en `eq-visualizer-log.json` de su propio repositorio; actualizar ambas fuentes al registrar hitos nuevos.
