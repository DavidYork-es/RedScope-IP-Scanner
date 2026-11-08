# RedScope IP Scanner Advanced — v2.1.0.3

First packaged release candidate of RedScope, developed by **DavidYork** (GitHub: **@DavidYork-es**).

## English

RedScope is a portable IPv4 network discovery and diagnostics tool for Windows, developed in **VB.NET / .NET Framework 4.8**.

**Included features**
- IPv4 hosts, ranges and CIDR scanning.
- ICMP/TCP discovery, configurable ports and timeout/concurrency limits.
- Local network interface selection, DNS and ARP/MAC/OUI inspection.
- Pause, resume and cancel; CSV export.
- On-demand external IP lookup via HTTPS.

**Installation**
1. Download `RedScope_v2.1.0.3_Windows_Portable.zip` from this release.
2. Extract the archive to a folder.
3. Run `RedScope.exe` on Windows with .NET Framework 4.8 installed.

No installation is required. The executable is not digitally signed. Windows SmartScreen or antivirus software may display a warning. This release does not require administrator privileges by manifest; network and firewall policies can still affect its operation.

**Verification:** compare the downloaded ZIP's SHA-256 hash with `SHA256SUMS.txt`. Source files are available from the repository.

## Español

Primera versión empaquetada de RedScope IP Scanner Advanced, desarrollada por **DavidYork** (@DavidYork-es).

**Incluye:** descubrimiento IPv4 mediante ICMP/TCP, escaneo por rangos/CIDR, selección de interfaces, DNS/ARP/MAC/fabricantes, controles de pausa/parada, exportación CSV y consulta opcional de IP pública.

**Instalación:** descarga `RedScope_v2.1.0.3_Windows_Portable.zip`, descomprímelo y ejecuta `RedScope.exe`. Se necesita Windows y .NET Framework 4.8; no requiere instalador. El EXE no está firmado digitalmente, por lo que SmartScreen podría mostrar una advertencia.

**Integridad:** verifica el SHA-256 del ZIP mediante `SHA256SUMS.txt`.

## Notes / Notas

- The maintainer reported successful compilation and functional validation of version 2.1.0.3. The release archive, if built through GitHub Actions, is generated from the committed source and must still be tested on an end-user Windows machine.
- Maintainer: **DavidYork**. No company is associated with this project.
- Only scan networks you own or are explicitly authorized to inspect.
- The repository and release must remain **private** until distribution rights for the included graphics and curated OUI entries are confirmed.
