# Privacidad de EQ Visualizer

- **Última actualización:** 18 de noviembre de 2026
- **Aplicación:** EQ Visualizer para Windows 11 x64 (beta pública)
- **Responsable del proyecto:** TKGHyper42 / TKG | Code Engineering
- **Contacto:** [tkg.codeengineering@gmail.com](mailto:tkg.codeengineering@gmail.com)

## Resumen

EQ Visualizer es una aplicación de escritorio que funciona localmente. En la versión examinada no hay cuentas, publicidad, analítica, informes de fallos enviados automáticamente, servicios en la nube ni conexiones de red iniciadas por el código de EQ. **No transmite audio ni datos musicales a un servidor del proyecto.** Esto no describe el comportamiento independiente de Windows o de las aplicaciones que reproducen la música.

La aplicación sí lee información de sesiones multimedia, analiza audio en memoria y **crea determinados archivos en el equipo**. Por ello, sería incorrecto afirmar que no procesa datos o que nunca guarda ninguna imagen de portada.

## Qué información utiliza

| Información | Finalidad | Almacenamiento en EQ |
| --- | --- | --- |
| Título, artista y miniatura que Windows expone en la sesión multimedia | Mostrar la canción y ajustar la apariencia | Durante la ejecución, en memoria; pueden formar parte de la imagen de fondo local descrita abajo |
| Muestras de la salida de audio y valores del espectro | Animar las barras | Procesamiento en memoria; no se crea un archivo de audio ni un historial de escuchas |
| Identificador de la aplicación multimedia y del dispositivo de audio seleccionados | Recuperar las preferencias de selección | En `settings.json`; no se guardan canciones individuales |
| Configuración, atajos y ruta de una imagen local seleccionada por la persona usuaria | Personalizar y recuperar ajustes | En `settings.json`; la imagen de origen no se copia como archivo independiente |
| Ruta del fondo de Windows anterior | Intentar restaurarlo al salir | En `previous-wallpaper.txt` |
| Posiciones de los iconos del escritorio | Mejorar localmente su contraste | Consultadas en memoria; no se guardan como inventario |

## Archivos locales y conservación

EQ utiliza la carpeta `%LOCALAPPDATA%\EQVisualizer\` de la cuenta de Windows:

- `settings.json`: preferencias, identificadores y, si se configura, **la ruta completa** de la imagen de espera. Permanece hasta que la persona usuaria lo elimine.
- `settings.json.bad`: copia de un archivo de ajustes ilegible, creada si hay un problema al cargarlo y no existe ya esa copia. Puede contener las preferencias y rutas antiguas; no se borra automáticamente.
- `music-wallpaper.png`: composición del fondo de Windows que **puede incluir portada, título y artista**. Se sustituye cuando se genera un nuevo fondo y normalmente se elimina al restaurar el fondo anterior. Tras un cierre inesperado o un fallo de restauración, podría permanecer en el disco.
- `previous-wallpaper.txt`: ruta del fondo anterior utilizada para intentar restaurarlo. Puede incluir el nombre de la cuenta en la ruta; no se elimina automáticamente al cerrar EQ.
- Archivos temporales `*.tmp`: pueden existir durante escrituras y, si la aplicación se interrumpe, quedar pendientes de limpieza.

Si eliges **Crear acceso directo en el escritorio**, se crea `EQ Visualizer.lnk` en tu Escritorio. EQ también cambia el fondo de Windows mientras está activo y procura restaurarlo al salir, siempre que Windows aún esté utilizando el fondo aplicado por EQ. El propio Windows puede conservar referencias o historial de fondos por separado.

Los modos de prueba usados por desarrolladores (`--smoke-test` y las utilidades del directorio `tests/`) pueden generar imágenes y diagnósticos en una carpeta de resultados indicada explícitamente. No forman parte del uso ordinario de la aplicación.

## Transmisión, terceros y transferencias

En esta versión, EQ no incorpora SDK de publicidad, métricas, cuentas, pagos ni un backend que reciba la información anterior. Por tanto, **EQ no realiza transferencias internacionales de estos datos a un servicio propio**. Los componentes de Windows y las aplicaciones musicales funcionan bajo sus propias políticas y condiciones; EQ no controla sus conexiones.

NAudio, Vortice, SharpGen y componentes de Microsoft se emplean como dependencias de ejecución; su inclusión no supone, por sí misma, que EQ les envíe contenido musical. Las licencias se documentan en [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).

## Control, eliminación y consultas

Puedes desactivar el análisis de audio desde **Ajustes → Audio**, elegir otra sesión o cerrar EQ. Para eliminar sus archivos locales, primero cierra la aplicación y comprueba que Windows haya restaurado el fondo deseado; después elimina `%LOCALAPPDATA%\EQVisualizer\` desde el Explorador. Esto reinicia las preferencias y elimina las copias locales indicadas. Si creaste un acceso directo, elimínalo por separado. No se garantiza borrar el historial gestionado independientemente por Windows u otras aplicaciones.

Si necesitas consultar el tratamiento descrito o ejercer derechos que correspondan según tu jurisdicción, escribe a [tkg.codeengineering@gmail.com](mailto:tkg.codeengineering@gmail.com). Como esta versión no opera una base de datos de usuarios, el proyecto no puede acceder remotamente a tus archivos locales ni recuperarlos por ti.

## Menores y seguridad

EQ no está diseñado para recopilar información de menores ni realiza perfiles de personas. Los archivos permanecen en el perfil de Windows, **sin cifrado propio adicional**; cualquier persona o software con acceso suficiente a esa cuenta podría consultarlos. Mantén protegida tu sesión y descarga únicamente distribuciones verificables.

## Cambios

Las modificaciones de esta política se publicarán en este repositorio junto con su fecha. Si una versión futura incorpora servicios en línea, métricas, cuentas o nuevas formas de almacenamiento, deberá revisarse esta política antes de distribuirla.
