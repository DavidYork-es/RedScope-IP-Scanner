# Contributing to RedScope

Thank you for considering a contribution. Please respect the current scope: a portable VB.NET / .NET Framework 4.8 Windows Forms network utility.

1. Discuss larger changes in an issue before submitting a pull request.
2. Keep changes focused and compatible with the existing .NET Framework 4.8 project.
3. Document the purpose and rationale of each non-trivial function or block, including relevant edge cases, for human maintenance.
4. Do not introduce application-owned Registry configuration, telemetry, persistent history, third-party binaries, or network calls without an explicit design review.
5. Do not use the following expressions in VB.NET source: `vbCr`, `vbLf`, `vbCrLf`, `vbTab`, `Chr()` or `ChrW()`. Use explicit framework alternatives such as `Environment.NewLine` or `Convert.ToChar()` as appropriate.
6. Rebuild in Visual Studio with **Release | Any CPU** and run the relevant cases in `PRUEBAS_V2103.txt`.
7. Do not post real scan data, private hostnames, credentials, internal addresses, or customer information in issues or pull requests.
8. Do not submit copyrighted graphic assets or OUI datasets unless you have the right to redistribute them under compatible terms.

The human-readable version history is kept in `version.mod`. Do not change assembly versions without agreement from the maintainer.
