# Política de seguridad / Security Policy

## Versiones y alcance

EQ Visualizer se distribuye actualmente como **beta pública para Windows 11 x64**. Se revisan reportes relativos a las versiones disponibles en la sección oficial de [Releases](https://github.com/TKGHyper42/EQ-Visualizer/releases). No existe un compromiso contractual de soporte, una garantía de seguridad absoluta ni un calendario de parches prometido.

## Reportar una vulnerabilidad

**No publiques detalles explotables, credenciales, archivos personales ni pruebas con datos de terceros en una issue pública.** Envíalos de forma privada a:

**[tkg.codeengineering@gmail.com](mailto:tkg.codeengineering@gmail.com)**

Asunto sugerido: `EQ Visualizer — reporte privado de seguridad`

Incluye, si es posible:

1. Versión de EQ y versión de Windows afectadas.
2. Descripción del problema y pasos mínimos para reproducirlo.
3. Impacto potencial y condiciones necesarias para explotarlo.
4. Una prueba de concepto **sin datos personales, material protegido ni acciones destructivas**.

Se intentará confirmar la recepción dentro de siete días hábiles, aunque no se garantiza un plazo fijo de resolución. Se solicita divulgación coordinada y tiempo razonable para investigar y corregir antes de hacer públicos los detalles. No se autoriza probar vulnerabilidades en sistemas o cuentas ajenas.

## Recomendaciones para usuarios

- Descarga únicamente desde el [repositorio y sus releases oficiales](https://github.com/TKGHyper42/EQ-Visualizer/releases).
- Si la release publica sumas SHA-256, contrasta el archivo descargado con `SHA256SUMS.txt` antes de ejecutarlo. Una suma verifica integridad respecto al manifiesto obtenido, pero **no equivale a una firma digital ni acredita por sí sola la identidad del autor**.
- EQ no necesita ejecutarse con privilegios de administrador para su uso normal.
- Mantén Windows actualizado; al tratarse de una beta, conserva tus propios respaldos y verifica el comportamiento antes de usarlo en entornos críticos.

## Gestión de incidentes

Ante un reporte se evaluarán alcance y reproducibilidad, se priorizará la contención, se preparará una corrección cuando sea viable y se comunicará mediante el repositorio o las notas de una release. No se publicarán automáticamente datos privados del informante. Las dependencias también pueden presentar fallos de seguridad: sus actualizaciones deben revisarse y probarse antes de incorporarse.

**English:** Please send potential vulnerabilities privately to the email above, not to public issues. Include the affected version, reproduction steps and impact, without credentials or personal data. Coordinated disclosure is requested; no fixed remediation SLA is offered.
