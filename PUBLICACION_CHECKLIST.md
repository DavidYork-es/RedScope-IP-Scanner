# RedScope 2.1.0.3 — checklist previo a GitHub público

**Este documento no es una autorización de publicación.** El proyecto se preparó localmente, sin subir archivos a GitHub.

## Ya comprobado

- [x] El ZIP fuente original supera comprobación CRC.
- [x] Los 15 archivos `.vb` figuran en el proyecto.
- [x] `AssemblyCompany` vacío; atribución a DavidYork.
- [x] No se encontraron las expresiones VB.NET excluidas (`vbCr`, `vbLf`, `vbCrLf`, `vbTab`, `Chr`, `ChrW`).
- [x] No se encontraron patrones evidentes de credenciales ni referencias empresariales identificadas en una revisión estática.
- [x] El autor comunica compilación correcta y pruebas funcionales completadas en Windows para 2.1.0.3.
- [x] Preparados README en inglés y castellano, licencia MIT propuesta, exclusiones de Git y políticas de contribución.

## Por confirmar antes de hacer público

- [ ] El titular de los derechos confirma que puede distribuir todo el código bajo MIT.
- [ ] Confirmar titularidad/licencia redistribuible de los recursos `RedScope.ico`, `RedScope.svg` y `RedScope-preview.png`.
- [ ] Confirmar procedencia y derechos de reutilización de las entradas de `Core/OuiResolver.vb`.
- [ ] El responsable acepta expresamente incluir `LICENSE` con la licencia MIT.
- [ ] Revisar manualmente todas las carpetas justo antes del primer commit.
- [ ] Elegir si los documentos de pruebas internas o de firma deben publicarse: por defecto se incluyen sin datos privados.
- [ ] Realizar el primer commit en la rama `main` del repositorio privado con autorización expresa.
- [ ] Compilar la misma fuente del commit para crear un ZIP **binario** con `RedScope.exe`.
- [ ] Calcular SHA-256 del ZIP binario y crear una GitHub Release `v2.1.0.3`.
- [ ] Cambiar a Public solo con autorización expresa y tras revisar el historial del repositorio.

**Aviso:** GitHub publica el código y no elimina las alertas de SmartScreen. Una firma de código puede ayudar a acreditar el editor, pero no garantiza la ausencia de advertencias.
