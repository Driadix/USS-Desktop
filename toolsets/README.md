# Toolsets

Bundled external tools are kept out of Git history and downloaded from pinned upstream releases.

Current expectation for USS Desktop:

- `toolsets/arduino-cli-<version>-win64/arduino-cli.exe`

To download the pinned Arduino CLI into this folder, run:

```powershell
.\scripts\Install-ArduinoCli.ps1
```

To publish the desktop app with the bundled CLI copied next to the executable, run:

```powershell
.\scripts\Publish-Portable.ps1
```
