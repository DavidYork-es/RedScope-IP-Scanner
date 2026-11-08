# RedScope IP Scanner Advanced

**Portable IPv4 network discovery and diagnostic utility for Windows**, written in **VB.NET / .NET Framework 4.8**.

**Current version:** `2.1.0.3`  
**Author:** DavidYork · GitHub: [@DavidYork-es](https://github.com/DavidYork-es)  
**Documentation:** English · [Español](README.es.md)

RedScope is designed for inventory and troubleshooting on networks you own or are authorized to assess. It is a Windows Forms application: there is no installer, background service or account requirement.

## Features

- Scan a single IPv4 address, ranges, partial ranges, CIDR networks or multiple ranges.
- Discover devices through ICMP echo and selected TCP ports, including hosts that block ping.
- Configure port profiles, custom ports, timeouts and concurrency; supports up to **1,024 distinct TCP ports** per scan.
- Display reverse DNS, local ARP-derived MAC addresses and manufacturer names from a small **built-in offline OUI lookup table** (not a complete registry).
- Inspect local IPv4 network interfaces, including virtual and disconnected adapters.
- Pause, resume or cancel scans; filter results and export displayed rows to UTF-8 CSV.
- Show unverified, pre-existing ARP cache entries separately from devices confirmed in the current scan.
- Copy addresses or open selected network tools via explicit context-menu actions.
- Optionally query the Internet-facing IP address via HTTPS when the **Public IP** window is opened or refreshed.

## Requirements

- Windows 10/11 with **.NET Framework 4.8** installed. Other Windows versions have not been certified for this release.
- To build: **Visual Studio 2026 Community** (or a compatible MSBuild setup) with **.NET Framework 4.8 targeting pack** and VB.NET desktop build tools.

## Download and run

When a binary release is published, download the archive from [Releases](https://github.com/DavidYork-es/RedScope-IP-Scanner/releases), unpack it and run `RedScope.exe`. The **source-code ZIP is not an executable distribution**.

No administrator privileges are requested by the application manifest. ICMP, ARP, firewall and network permissions imposed by Windows or local policy can affect discovery results.

## Build from source

1. Clone or download this repository.
2. Open `RedScope.sln` in Visual Studio.
3. Select **Release | Any CPU**, then **Rebuild Solution**.
4. The resulting executable is normally located at `RedScope/bin/Release/RedScope.exe`.

On a correctly configured Windows build machine you can alternatively use `COMPILAR_RELEASE.cmd`.

### Keyboard shortcuts

| Action | Shortcut |
| --- | --- |
| Start scanning | `F5` |
| Pause / resume | `F6` |
| Stop | `Esc` |
| Filter / find | `Ctrl+F` |

## Privacy and portability

RedScope does **not** implement its own telemetry, save scan histories, write application preferences to the Windows Registry, or create application configuration directories. Changes made within a session are not retained. Exporting CSV writes only to a location explicitly chosen by the user.

Opening the **Public IP** window initiates an outbound HTTPS request, using `api.ipify.org` first and `checkip.amazonaws.com` as fallback. Those providers necessarily observe the source IP of the request. RedScope does not intentionally send network scan results, MAC addresses or hostnames to these services. Other user-invoked actions (for example, opening a host's HTTP page) may also generate network traffic. Windows itself may update normal operating-system caches during network activity.

## Scope and limitations

- IPv4 scanning only; no full IPv6 host scanner.
- Maximum of 65,536 addresses per scan and 1,024 distinct TCP ports.
- Device visibility depends on firewall rules, routing, VPNs, ARP locality and DNS behavior.
- Manufacturer identification uses a curated subset of OUIs; unknown values remain unknown.
- This is a diagnostic scanner, not a vulnerability assessment or exploitation framework.

**Only scan networks you own or are explicitly permitted to assess.**

## Quality and release status

Source version `2.1.0.3` was reported by the maintainer as successfully compiled in Visual Studio 2026 and functionally tested on Windows on 8 November 2026. No independent Windows build or automated test report is included in this repository. The regression checklist is in `PRUEBAS_V2103.txt`; the history is recorded in `version.mod`.

## Licensing and attribution

Proposed open-source license: **MIT** (`LICENSE`), subject to the maintainer confirming redistribution rights over all included graphics and OUI lookup entries before making the repository public. Project author: **DavidYork** (GitHub **DavidYork-es**). No company is identified as publisher or owner of this project.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for contribution conventions and [SECURITY.md](SECURITY.md) for vulnerability reporting guidance.
