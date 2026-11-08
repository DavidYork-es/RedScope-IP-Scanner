# RedScope IP Scanner Advanced

**Utilidad portátil para descubrimiento y diagnóstico de redes IPv4 en Windows**, desarrollada en **VB.NET / .NET Framework 4.8**.

**Versión actual:** `2.1.0.3`  
**Autor:** DavidYork · GitHub: [@DavidYork-es](https://github.com/DavidYork-es)  
**Documentación:** [English](README.md) · Español

RedScope está diseñado para inventario y diagnóstico de redes propias o expresamente autorizadas. Es una aplicación Windows Forms sin instalador, servicio en segundo plano ni necesidad de cuenta.

## Funciones

- Escaneo de una IPv4, rangos completos o abreviados, redes CIDR y varios rangos combinados.
- Descubrimiento ICMP y TCP, incluso cuando el equipo filtra el ping.
- Perfiles y puertos personalizados, timeouts y concurrencia; hasta **1.024 puertos TCP distintos** por escaneo.
- DNS inverso, MAC obtenidas mediante ARP local y fabricante desde una **tabla OUI reducida y embebida**, sin consultas de MAC a Internet.
- Selección de tarjetas de red IPv4, incluidas interfaces virtuales y desconectadas.
- Pausar, reanudar, detener, filtrar resultados y exportar filas visibles a CSV UTF-8.
- Diferenciación de rastros preexistentes de caché ARP, no confirmados por el escaneo actual.
- Acciones explícitas de menú contextual para copiar direcciones o abrir utilidades de red.
- Consulta opcional de IP pública por HTTPS al abrir o actualizar la ventana **IP PÚBLICA**.

## Requisitos

- Windows 10/11 con **.NET Framework 4.8**. No se ha certificado esta versión para otros sistemas Windows.
- Para compilar: **Visual Studio 2026 Community** (o MSBuild compatible), con las herramientas de VB.NET y el **Targeting Pack de .NET Framework 4.8**.

## Descargar y ejecutar

Cuando exista una versión binaria publicada, descarga el archivo desde [Releases](https://github.com/DavidYork-es/RedScope-IP-Scanner/releases), descomprímelo y ejecuta `RedScope.exe`. El **ZIP del código fuente no incluye un ejecutable**.

El manifiesto no solicita permisos de administrador. El funcionamiento de ICMP, ARP y TCP depende de las restricciones del sistema y de la red.

## Compilación desde el código fuente

1. Clonar o descargar este repositorio.
2. Abrir `RedScope.sln` en Visual Studio.
3. Seleccionar **Release | Any CPU** y **Recompilar solución**.
4. Encontrar el ejecutable normalmente en `RedScope/bin/Release/RedScope.exe`.

También puede utilizarse `COMPILAR_RELEASE.cmd` en un equipo Windows correctamente configurado.

### Teclas rápidas

| Acción | Tecla |
| --- | --- |
| Iniciar | `F5` |
| Pausar / reanudar | `F6` |
| Detener | `Esc` |
| Buscar / filtrar | `Ctrl+F` |

## Privacidad y portabilidad

RedScope **no incorpora telemetría propia**, ni conserva históricos de escaneos o preferencias, ni escribe configuración de la aplicación en el Registro de Windows. Los ajustes de una sesión se descartan al cerrar. La exportación CSV solo escribe en una ubicación elegida expresamente por el usuario.

Al abrir **IP PÚBLICA** se realiza una petición HTTPS a `api.ipify.org` o, si falla, a `checkip.amazonaws.com`. El proveedor observa inevitablemente la IP de origen. RedScope no envía intencionadamente resultados de escaneos, MAC o nombres DNS a esos servicios. Otras acciones solicitadas por el usuario (como abrir HTTP de un equipo) también pueden generar tráfico. Windows puede actualizar sus cachés habituales por la actividad de red.

## Límites

- Escaneo IPv4; sin escáner integral de hosts IPv6.
- Hasta 65.536 direcciones por ejecución y 1.024 puertos TCP distintos.
- Los cortafuegos, la topología, las VPN, el ARP y el DNS condicionan el descubrimiento.
- La base OUI es parcial; los fabricantes no identificados permanecen desconocidos.
- Es una herramienta de diagnóstico, no un escáner de vulnerabilidades.

**Utiliza la herramienta únicamente en redes propias o para las que dispongas de autorización.**

## Estado de las pruebas

El responsable del proyecto confirmó el 8 de noviembre de 2026 que la versión `2.1.0.3` había compilado correctamente con Visual Studio 2026 y superado sus pruebas funcionales en Windows. No se adjunta un informe independiente de compilación ni una batería automatizada de pruebas. El plan de regresión está en `PRUEBAS_V2103.txt` y el historial en `version.mod`.

## Licencia y autoría

Licencia libre propuesta: **MIT** (`LICENSE`), pendiente de confirmación de los derechos de redistribución de los recursos gráficos y las entradas OUI antes de abrir el repositorio al público. Autor: **DavidYork** (GitHub **DavidYork-es**). No figura ninguna empresa como editora o propietaria.

## Colaboraciones

Consulta [CONTRIBUTING.md](CONTRIBUTING.md) y [SECURITY.md](SECURITY.md).
