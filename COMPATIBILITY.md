# Compatibilidad de EQ Visualizer

El ejecutable incluye .NET 10 LTS. No requiere instalar Spotify, iniciar sesión en sus APIs ni descargar un runtime aparte.

| Sistema | Versión del ejecutable | Alcance |
| --- | --- | --- |
| Windows 11 | x64 | Plataforma de soporte; pendiente completar la validación de escritorio y audio |

Las ediciones N de Windows 11 pueden necesitar Media Feature Pack.

## Elegir música y audio

En la bandeja, **Reproductor** permite elegir una sesión musical que Windows exponga. Se aceptan sesiones declaradas como música o, si no declaran tipo, con artista. Algunas aplicaciones no publican estos datos y no aparecerán. Si la sesión musical no proporciona una miniatura, se muestra «Sin portada» con el título y el artista disponibles.

**Capturar sonido de** elige entre la mezcla completa y un proceso de audio concreto. En modo mezcla, **Salida de audio** elige el dispositivo: pueden reaccionar los efectos a sonidos de otras aplicaciones. En modo proceso, selecciona manualmente el PID de la aplicación de música; la salida elegida no interviene. **Reproductor** elige por separado título, artista y portada: cambiarlo no cambia automáticamente el PID de captura. Puedes desactivar el análisis en Ajustes → Audio. No se reproduce, registra en archivos ni transmite sonido; tampoco se guarda historial musical ni se incorpora telemetría.

La captura aislada usa el loopback por proceso de Windows e incluye sus procesos hijos. Si falla, **Estado** muestra el error y puedes seleccionar la mezcla manualmente. El PID elegido dura solo esta ejecución para evitar reutilizar por error un identificador después de reiniciar. Una sesión multimedia no proporciona un PID fiable de su árbol de audio. Aún falta validar el aislamiento con una aplicación musical real y dos fuentes de sonido simultáneas.

## Ajustes y atajos

Desde la bandeja abre **Ajustes**: portada, posición, tamaño, franja inferior, contraste, sensibilidad, suavizado, túnel, paralaje, FPS, escala interna y capas. El recuadro muestra la geometría real con una señal de audio simulada y la portada de la sesión actual, si está disponible; no prueba la captura en vivo ni la integración WorkerW. Los cambios del wallpaper se aplican al guardar. La interfaz inicia oscura, con acento menta y opacidad del 96 %. El color de acento se aplica a la interfaz al guardar, y la transparencia de la ventana de ajustes (80–100 %) se previsualiza en ella. Los ajustes son globales; cada pantalla adapta la escena a su tamaño y área de trabajo. **Estado** muestra el motor activo, la detección de iconos y posibles errores de audio.

En **Portada → Cuando no hay música**, puedes ocultar el visualizador, mostrar un reloj, una imagen local o ambos. Con el reloj o la imagen activados, una pausa de unos 600 ms muestra esa escena. Al detener la reproducción o cerrar la sesión, el cambio se hace al recibir el evento de Windows. Al reanudar, vuelve la portada. Con el modo sin música desactivado, la portada se mantiene durante la pausa.

El reloj se actualiza por minuto. La imagen se lee del archivo elegido (máximo 20 MB y 8192 píxeles por lado). La vista previa permite alternar entre la escena musical y el modo sin música antes de guardar.

El reloj puede ser digital en una tarjeta redondeada o analógico circular. En ambos estilos se actualiza cada minuto visible; el estilo se cambia en Portada y aparece en la vista previa antes de guardar.

El apartado **Interfaz** ofrece cinco temas oscuros y cuatro claros. Carbón es el predeterminado. Aurora combina un fondo carbón con acento melocotón y tonos lila. Cada paleta mantiene contraste de al menos 7:1 en texto principal y 4,5:1 en texto secundario, según la comparación de colores del proyecto. Los acentos personalizados se ajustan si no alcanzan 4,5:1 contra la superficie. Esto no sustituye una revisión de accesibilidad con usuarios reales.

En **Visualizador** elige **Solo círculo**, **Solo franja inferior** o **Círculo y franja**. También puedes cambiarlo directamente desde el menú de la bandeja. El círculo sigue a la portada; la franja mantiene su posición sobre la barra de tareas. La portada y el túnel son independientes del modo de barras elegido.

En **Audio**, los deslizadores **Altura del círculo** y **Altura de la franja** ajustan por separado la longitud de las barras entre 50 % y 200 %. En **Movimiento**, el túnel opcional usa anillos en perspectiva con los colores de la portada. Viene desactivado en nuevas instalaciones.

**Portada → Información de la canción** muestra el título y el artista bajo la portada y viene activado en nuevas instalaciones. Si está activado, el texto también forma parte de la imagen de fondo que genera EQ. **Audio → Grosor de barras** permite ajustar el ancho del círculo y de la franja entre 50 % y 300 %. El valor inicial es 160 %.

Las barras usan un degradado con colores dominantes de la portada, realzados para mejorar el contraste y con contorno oscuro local. Las portadas monocromas mantienen una paleta neutra. **Movimiento → Desenfoque del fondo** ajusta el blur entre 0 % y 100 %; 0 % lo desactiva. Solo se desenfoca la copia de fondo, conservando la portada principal y el texto nítidos. El fondo se recalcula cuando cambia la portada o este ajuste, evitando un filtro de blur continuo por fotograma.

- `Ctrl + Alt + V`: activar o desactivar el wallpaper.
- `Ctrl + Alt + E`: alternar túnel y paralaje.
- Ambos atajos y sus modificadores se pueden cambiar.
- **Salir** cierra el wallpaper y restaura la imagen de fondo previa si Windows sigue usando la de EQ.

Los ajustes se guardan en `%LOCALAPPDATA%\EQVisualizer\settings.json`, sin títulos, artistas, portadas ni muestras de audio **en ese archivo**. Sí puede guardar el identificador del reproductor, el dispositivo de audio y la ruta completa de una imagen seleccionada. Si falla la lectura de los ajustes, puede conservarse una copia `settings.json.bad`. El programa no registra inicio automático ni requiere administrador.

## Fondo, iconos y pausas

EQ aplica una imagen con la portada de la canción como fondo de Windows. La imagen se guarda como `music-wallpaper.png` junto a los ajustes, **puede incluir título y artista**, y se sustituye cuando cambia la canción. La restauración recupera la imagen anterior si Windows todavía tiene aplicado el fondo de EQ; normalmente elimina esa imagen, pero podría permanecer tras un fallo o cierre inesperado. La ruta del fondo anterior se conserva en `previous-wallpaper.txt`. Consulta [Privacidad](PRIVACY.md) para conocer la conservación y la limpieza manual. Las presentaciones de diapositivas y los fondos distintos por monitor requieren sus propios ajustes de Windows.

WorkerW permite dibujar las barras detrás de los iconos. Es una integración no documentada por Microsoft y puede cambiar con las versiones del Explorador. El programa comprueba la relación y reintenta al perderla. Si otra ventana maximizada o fullscreen cubre un monitor, EQ pausa la animación de esa pantalla. Al bloquear o suspender la sesión detiene la animación y libera la captura cuando no queda una pantalla activa.

El contraste adaptativo consulta mediante accesibilidad las posiciones de los iconos del Explorador aproximadamente cada dos segundos y aplica sombreado local según una estimación de luminosidad de la portada. No se usan bandas fijas. Puede existir retraso al arrastrar iconos, y algunos escritorios o configuraciones no exponen posiciones. **Estado** indica esa limitación; **Oscurecer imagen completa** es la alternativa explícita. No se garantiza contraste perfecto en todas las composiciones ni shells alternativos.

El límite inicial es 45 FPS. La escala interna parte de cada monitor. Direct3D 11 usa GPU si está disponible. Si RivaTuner detecta EQ por el uso de Direct3D, selecciona **Rendimiento → Motor gráfico → Usar GDI+ (compatibilidad)**. GDI+ dibuja en CPU y su rendimiento depende del tamaño del escritorio y el límite de FPS. **Estado** muestra el motor activo.

## Qué se comprueba

Las comprobaciones automáticas cubren formatos de muestras, bandas de frecuencia, geometría, compilación x64, shaders integrados, carga y retirada de portada, dibujo, cambio de tamaño y lectura de píxeles en un runner Windows. La validación en un equipo real todavía incluye Spotify, WorkerW, recuperación del Explorador, accesibilidad de iconos, bloqueo, suspensión, varios monitores, FPS y consumo.

Referencias de compatibilidad: [sesiones multimedia](https://learn.microsoft.com/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessionmanager), [captura por proceso](https://learn.microsoft.com/windows/win32/api/audioclientactivationparams/ns-audioclientactivationparams-audioclient_activation_params), [.NET 10 y sistemas operativos](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md).
