# Compatibilidad de EQ Visualizer

El ejecutable incluye .NET 10 LTS. No requiere instalar Spotify, iniciar sesión en sus APIs ni descargar un runtime aparte.

| Sistema | Versión del ejecutable | Alcance |
| --- | --- | --- |
| Windows 11 | x64 | Objetivo principal; pendiente validar escritorio y audio en un equipo real |
| Windows 10 1809, compilación 17763 o posterior | x64, x86 | Mínimo de las APIs de sesiones multimedia elegidas; soporte limitado y sin validación en hardware Windows 10 |

El ciclo de soporte del runtime también depende de la edición y mantenimiento de Windows. El mínimo de una API no implica soporte vigente del sistema ni una garantía de funcionamiento. El artefacto x86 se comprueba bajo Windows x64 en CI; no equivale a una prueba en un equipo de 32 bits. Las ediciones N pueden necesitar Media Feature Pack. Windows ARM, OpenGL y Vulkan no están implementados ni validados.

## Elegir música y audio

En la bandeja, **Reproductor** permite elegir una sesión musical que Windows exponga. Se aceptan sesiones declaradas como música o, si no declaran tipo, con artista. Algunas aplicaciones no publican estos datos y no aparecerán. Una sesión de vídeo no se presenta como música. Si no hay portada, EQ Visualizer deja visible el fondo anterior.

**Salida de audio** elige el dispositivo cuya mezcla se analiza. Elegir un reproductor NO aísla su audio: pueden reaccionar los efectos a otras aplicaciones de esa salida. Puedes desactivar el análisis en Ajustes → Audio. No se reproduce, registra en archivos ni transmite sonido; tampoco se guarda historial musical ni se incorpora telemetría.

El aislamiento por proceso aún no está implementado. La API de activación de captura por proceso exige Windows build 20348 o posterior; Windows 10 de escritorio habitual llega a 19045 y necesita esta alternativa limitada de mezcla. La identidad de una sesión multimedia no proporciona directamente un PID fiable de su árbol de audio; no se simula esa relación.

## Ajustes y atajos

Desde la bandeja abre **Ajustes**: portada, posición, tamaño, franja inferior, contraste, sensibilidad, suavizado, túnel, paralaje, FPS, escala interna y capas. El recuadro muestra la geometría real con una señal de audio simulada y la portada de la sesión actual, si está disponible; no prueba la captura en vivo ni la integración WorkerW. Los cambios del wallpaper se aplican al guardar. El color de acento se aplica a la interfaz al guardar, y la transparencia de la ventana de ajustes (80–100 %) se previsualiza en ella. Los ajustes son globales; cada pantalla adapta la escena a su tamaño y área de trabajo. **Estado** muestra el motor activo, la detección de iconos y posibles errores de audio.

Sin sesión musical puedes dejar el wallpaper oculto, mostrar un reloj, una imagen local o los dos. El reloj se actualiza periódicamente sin renderizado continuo y este modo no dibuja barras con audio simulado. La imagen se lee del archivo elegido (máximo 20 MB y 8192 píxeles por lado); no se copia al directorio de la aplicación ni a la compilación. La vista previa permite alternar entre la escena musical y el modo sin música antes de guardar.

En **Visualizador** elige **Solo círculo**, **Solo franja inferior** o **Círculo y franja**. También puedes cambiarlo directamente desde el menú de la bandeja. El círculo sigue a la portada; la franja mantiene su posición sobre la barra de tareas. La portada y el túnel son independientes del modo de barras elegido.

Activa **Portada → Información de la canción** para mostrar título y artista bajo la portada. Los datos proceden de la sesión musical y permanecen solo en memoria; no se incluyen canciones, portadas o nombres reales como recursos de la aplicación. **Audio → Grosor de barras** permite ajustar el ancho del círculo y de la franja entre 50 % y 300 %. El valor inicial es 160 % para mejorar su visibilidad.

Las barras usan un degradado con colores dominantes de la portada, realzados para mejorar el contraste y con contorno oscuro local. Las portadas monocromas mantienen una paleta neutra. **Movimiento → Desenfoque del fondo** ajusta el blur entre 0 % y 100 %; 0 % lo desactiva. Solo se desenfoca la copia de fondo, conservando la portada principal y el texto nítidos. El fondo se recalcula cuando cambia la portada o este ajuste, evitando un filtro de blur continuo por fotograma.

- `Ctrl + Alt + V`: activar o desactivar el wallpaper.
- `Ctrl + Alt + E`: alternar túnel y paralaje.
- Ambos atajos y sus modificadores se pueden cambiar.
- **Salir** cierra las ventanas del wallpaper y deja visible el fondo previo.

Los ajustes se guardan en `%LOCALAPPDATA%/EQVisualizer/settings.json`, sin títulos, artistas, portadas ni muestras de audio. El programa no registra inicio automático ni requiere administrador.

## Fondo, iconos y pausas

WorkerW permite dibujar detrás de los iconos; es una integración no documentada por Microsoft y puede cambiar con las versiones del Explorador. El programa comprueba la relación y reintenta al perderla. No cambia el archivo ni el ajuste del fondo de Windows. Si detecta otra ventana maximizada o fullscreen cubriendo un monitor, pausa esa pantalla. Al bloquear o suspender la sesión detiene la animación y libera la captura cuando no queda una pantalla activa. La portada queda estática al pausar la música.

El contraste adaptativo consulta mediante accesibilidad las posiciones de los iconos del Explorador aproximadamente cada dos segundos y aplica sombreado local según una estimación de luminosidad de la portada. No se usan bandas fijas. Puede existir retraso al arrastrar iconos, y algunos escritorios o configuraciones no exponen posiciones. **Estado** indica esa limitación; **Oscurecer imagen completa** es la alternativa explícita. No se garantiza contraste perfecto en todas las composiciones ni shells alternativos.

El límite inicial es 45 FPS; no es una medición ni garantía. La escala interna parte de cada monitor, nunca de una resolución 4K fija. Direct3D 11 usa GPU si está disponible; WARP y GDI+ son alternativas que pueden consumir más CPU. Varios monitores usan recursos gráficos separados. Se puede reducir escala y capas para bajar el consumo.

## Qué se comprueba

Las comprobaciones automáticas cubren formatos de muestras, bandas de frecuencia, geometría, publicación x64/x86, shaders integrados, carga y retirada de portada, dibujo, cambio de tamaño y lectura de píxeles en un runner Windows. Las imágenes sintéticas de esas pruebas no representan una sesión musical real. No prueban por sí solas Spotify, WorkerW, recuperación del Explorador, accesibilidad de iconos, bloqueo, suspensión, varios monitores ni FPS o consumo en hardware de usuario. Esas pruebas siguen pendientes antes de considerar una versión final.

Referencias de compatibilidad: [sesiones multimedia](https://learn.microsoft.com/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessionmanager), [captura por proceso](https://learn.microsoft.com/windows/win32/api/audioclientactivationparams/ns-audioclientactivationparams-audioclient_activation_params), [.NET 10 y sistemas operativos](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md).
