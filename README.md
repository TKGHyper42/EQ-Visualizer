# EQ Visualizer

Prototipo privado para Windows. Dibuja una animación en una ventana WorkerW detrás de los iconos y usa el nivel de la salida de audio predeterminada para modularla. No reemplaza el archivo del fondo actual. Esta versión aún no detecta canciones, artistas ni portadas.

## Probar

Descarga el artefacto `EQVisualizer-win-x64` o `EQVisualizer-win-x86` de la ejecución de Actions asociada al PR, extráelo y ejecuta `EQVisualizer.exe`. Es una compilación de prueba, no un Release. Haz clic derecho en el icono de bandeja para activar o desactivar la animación, elegir 30/45/60 FPS, variar intensidad o salir. Doble clic en el icono alterna la animación. Los ajustes se guardan en `%LOCALAPPDATA%\EQVisualizer\settings.json`. Cerrar normalmente retira la ventana; el fondo existente no se modifica.

## Estado y límites

- El prototipo dibuja por GDI+ en CPU, con un temporizador limitado a 45 FPS por defecto. Direct3D 11, motores OpenGL y Vulkan, calidad y escala interna siguen pendientes. No se han medido FPS, GPU, CPU ni RAM en hardware Windows.
- La captura WASAPI loopback sirve solo para calcular un nivel RMS en memoria. No reproduce, graba ni transmite audio. Captura la salida general del dispositivo predeterminado, incluidos sonidos ajenos al reproductor. No hay aislamiento por sesión o proceso ni selector de dispositivo todavía.
- La animación actual es geométrica; no muestra ni anima portada. Están pendientes Windows GlobalSystemMediaTransportControlsSessionManager para metadatos/portada, selección de sesiones, efectos de profundidad de portada y paralaje.
- La ventana se integra mediante WorkerW, una técnica no contractual del Explorador. El programa se cierra si no consigue integrarse. Recuperación tras reinicio del Explorador, bloqueo de sesión, suspensión y detección robusta de pantalla completa siguen pendientes. La pausa actual solo detecta una ventana frontal que cubre casi todo un monitor.
- Se crea una ventana por monitor, usando su resolución. Falta validar posición y escala con distintas configuraciones HiDPI. No hay ajuste de contraste para los iconos: su ubicación real no se detecta. No se afirma legibilidad adaptativa.
- No hay hotkeys globales ni notificaciones de estado, salvo errores de captura o integración. No se guarda historial musical ni hay telemetría.
- Objetivo de compatibilidad: Windows 10 x86/x64 y Windows 11 x64. La API WASAPI y WinForms existen en esas plataformas; la compatibilidad completa del prototipo no se ha comprobado. Windows 10 1607 es una referencia mínima para el futuro aislamiento por proceso mediante process loopback, cuya API se introdujo en Windows 10 2004 (build 19041); versiones anteriores necesitarían una ruta limitada de captura general. Aún no se implementa dicho aislamiento.

## Arquitectura prevista

Separar captura de audio, sesiones multimedia, render, integración de escritorio y ajustes. El prototipo mantiene esas piezas como clases independientes en `Program.cs`; se extraerán al incorporar D3D11 y sesiones. La ruta prioritaria es D3D11. No se ofrecen selectores de motores no implementados.

## Dependencias y privacidad

.NET 10 Windows Desktop y NAudio 2.2.1 (MIT). La compilación publica un ejecutable autocontenido x86 o x64. No se distribuyen canciones, portadas ni recursos de otros proyectos. El código propio conserva la licencia MIT del repositorio y su atribución. La compilación de CI comprueba compilación y empaquetado; no verifica visualmente el wallpaper ni el audio en un escritorio Windows real.
