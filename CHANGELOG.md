# EQ Visualizer — Bitácora técnica

> Registro interno del desarrollo. La primera versión pública prevista es **1.0.0**; las denominaciones V2 y V3 son etapas internas, no lanzamientos públicos.
>
> **Estado actual:** híbrido V2+V3 en validación (2026-11-12). Este documento no equivale a una certificación de funcionamiento en una PC real.

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
