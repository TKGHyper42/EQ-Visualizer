<div align="center">

# EQ Visualizer

### Tu música. Tu portada. Un escritorio con vida.

**Wallpaper musical para Windows 11 x64**

</div>

![Concepto visual de EQ Visualizer en tonos melocotón y lila](assets/eq-visualizer-hero.png)

<p align="center"><sub>Ilustración conceptual. EQ usa la portada y la paleta de la canción activa para construir el fondo.</sub></p>

---

## Qué es

EQ Visualizer convierte la sesión multimedia que Windows expone de una aplicación compatible en un wallpaper animado real. La portada, el título y el artista aparecen detrás de los iconos del escritorio, mientras las barras reaccionan al audio que eliges analizar.

No es una ventana flotante ni depende de Lively. EQ integra su propio motor de escritorio con WorkerW, aplica su imagen de fondo en Windows y restaura tu fondo anterior al salir.

## Funciones principales

- **Portada, canción y artista.** Obtiene los metadatos de las sesiones multimedia de Windows, por ejemplo de YouTube en Chrome o Spotify cuando la aplicación los publica.
- **Barras con la paleta de la portada.** El degradado se calcula con los colores dominantes de cada portada. Puedes usar **solo círculo**, **solo franja inferior** o ambos.
- **Tamaño independiente.** Ajusta por separado la altura de las barras circulares y de la franja, de 50 % a 200 %, además de su grosor.
- **Fondo con profundidad.** El desenfoque conserva la portada y el texto nítidos. El efecto de túnel usa anillos en perspectiva coloreados por la portada, sin repetir la carátula.
- **Modo sin música.** Al pausar durante aproximadamente 600 ms, muestra un reloj digital o analógico, una imagen local, ambos, o conserva la portada. Al cerrar o detener una sesión, el cambio se realiza al recibir el evento de Windows.
- **Audio automático o elegido.** Selecciona automáticamente una sesión, una sesión concreta, la mezcla completa de salida o un proceso aislado. Los metadatos y la fuente de audio se configuran de forma independiente.
- **Controles de escritorio.** Icono de bandeja, acceso directo a Ajustes, minimizar, maximizar, restaurar y cerrar. No se crea una ventana nueva cuando cambia la canción.
- **Compatibilidad con RivaTuner.** Puedes cambiar a GDI+ para evitar que el renderizado Direct3D sea detectado como juego.

## Instalación

### Ejecutable portátil

Descarga **`EQVisualizer-win-x64.exe`** desde [Releases](https://github.com/TKGHyper42/EQ-Visualizer/releases), guárdalo en la carpeta donde quieras conservar EQ y ejecútalo. El `.exe` es autocontenido: no requiere el ZIP ni una instalación adicional de .NET.

El archivo **`EQVisualizer-win-x64.zip`** es una alternativa que incluye el mismo ejecutable, la documentación y las licencias. El archivo **`SHA256SUMS.txt`** permite comprobar su integridad. El ejecutable requiere **Windows 11 x64**.

Consulta [COMPATIBILITY.md](COMPATIBILITY.md) para conocer requisitos, límites de la captura aislada y compatibilidad de aplicaciones.

## Uso rápido

1. Abre `EQVisualizer-win-x64.exe`. En Bienvenida elige círculo, franja o ambos.
2. Reproduce música o un video musical en una aplicación que comparta su sesión con Windows.
3. Haz doble clic en el icono de EQ junto al reloj de Windows para abrir **Ajustes**.
4. En **Portada**, activa la información de la canción y elige qué aparece cuando no hay música.
5. En **Audio**, configura la fuente, sensibilidad, grosor y la altura independiente de cada estilo de barras.
6. En **Movimiento**, activa el túnel y ajusta el desenfoque del fondo. Pulsa **Guardar cambios** para aplicarlos al escritorio.

El menú del icono de bandeja permite cambiar el visualizador, elegir el reproductor, seleccionar la fuente de audio, ver el estado y crear un acceso directo de escritorio que abre Ajustes en la instancia existente.

## Audio y detección multimedia

EQ usa dos sistemas separados:

| Para… | Usa… |
| --- | --- |
| Portada, título y artista | **Reproductor**: sesiones multimedia que Windows comparte |
| Movimiento de las barras | **Capturar sonido de**: mezcla de salida o proceso aislado |

El modo **Automático** elige una sesión musical disponible. En modo de mezcla, las barras reaccionan a todo lo que suene en la salida seleccionada. En modo de proceso, eliges manualmente una aplicación de audio para aislarla. El aislamiento por proceso requiere Windows build 20348 o posterior y no se selecciona automáticamente porque una sesión multimedia no expone de forma fiable el proceso de audio correspondiente.

Algunas aplicaciones no publican título, artista o portada a Windows. En esos casos EQ muestra lo que la sesión sí proporcione y mantiene el visualizador según la fuente de audio elegida.

## Ajustes destacados

| Sección | Ajustes |
| --- | --- |
| **Portada** | Tamaño y posición, título y artista, reloj, imagen local y comportamiento sin música |
| **Audio** | Fuente de captura, sensibilidad, suavizado, grosor, altura del círculo y altura de la franja |
| **Movimiento** | Túnel, paralaje, fuerza del movimiento y desenfoque de fondo |
| **Rendimiento** | FPS, escala interna, capas y motor GDI+ de compatibilidad |
| **Interfaz** | Temas claros y oscuros, color de acento y transparencia |

Los controles numéricos usan sliders y admiten un valor exacto junto al deslizador. La vista previa usa una señal simulada y te permite comprobar los cambios antes de guardar.

## Rendimiento, iconos y pantalla completa

EQ deja el visualizador detrás de los iconos y consulta su posición para aplicar contraste local cuando Windows lo permite. Si otra ventana maximizada o a pantalla completa cubre un monitor, pausa el renderizado y la captura de esa pantalla para evitar consumo innecesario. La reproducción de música no se pausa.

Direct3D 11 se utiliza cuando está disponible. Si RivaTuner detecta EQ como juego, activa **Rendimiento → Motor gráfico → Usar GDI+ (compatibilidad)**. Este modo evita Direct3D, aunque puede usar más CPU según la resolución y el límite de FPS.

## Privacidad

EQ analiza niveles de audio localmente para animar las barras. No graba canciones, no transmite audio, no requiere una cuenta de Spotify y no guarda historial musical. Los ajustes se guardan en `%LOCALAPPDATA%/EQVisualizer/settings.json` sin títulos, artistas, portadas ni muestras de audio.

## Salir y restaurar el fondo

Selecciona **Salir** en el icono de bandeja para cerrar EQ y restaurar el fondo de Windows que estaba activo antes de abrirlo. EQ no configura inicio automático ni requiere permisos de administrador.

---

<div align="center">

Creado por **TKGHyper42** · [Licencia MIT](LICENSE)

</div>

