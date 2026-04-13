# USS Desktop

USS Desktop is a portable Windows application for deterministic Arduino-based build and flash workflows.

The target users are non-technical operators, electricians, and support staff who need a simple and reliable way to:

- open a project folder
- compile it with pinned tools
- flash it to a connected board
- inspect the result without using Arduino IDE directly

## Current Direction

- Platform: Windows only
- UI stack: WPF on .NET 10
- Build/flash engine for v1: `arduino-cli`
- Project families in scope for v1:
  - Arduino ESP32
  - Arduino STM32
- Out of scope for v1:
  - STM32CubeIDE-native projects
  - raw `esptool` UX
  - raw `STM32CubeProgrammer` UX
  - non-Arduino firmware pipelines

## Core Product Principles

- Zero-setup for operators: one portable app, no IDE installation required.
- Deterministic builds: pinned SDK/tool versions, reproducible output.
- Project-local outputs: binaries are written into the opened project `build` directory.
- Operator-first UX: simple flows, clear errors, no disappearing consoles.
- One source of truth: GUI and future CLI use the same backend rules.

## Planned Repository Layout

- `docs/` product, architecture, ADRs, verification strategy
- `src/` WPF application and supporting libraries
- `tests/` automated tests and test assets

## Documentation Map

- `docs/Product-Vision.md`
- `docs/Architecture.md`
- `docs/Configuration-Model.md`
- `docs/Tech-Stack.md`
- `docs/Test-and-Verification.md`
- `docs/Roadmap-v1.md`
- `docs/ADR/ADR-001-dotnet10-wpf-arduino-cli.md`

## Status

This repository is currently in the documentation-first phase.

The next implementation step after this baseline is:

1. create the solution structure under `src/`
2. implement the configuration model
3. implement the first operator UI shell
4. wire `arduino-cli` build and upload operations behind application services

## Bundled Arduino CLI

USS Desktop expects a pinned external toolset under `toolsets/arduino-cli-<version>-win64/`.

User settings, recent projects, and logs are stored under `%LocalAppData%\USS Desktop` by default so each Windows user has separate app state. Mutable Arduino CLI state is stored under `%USERPROFILE%\.uss\arduino-cli` by default to keep toolchain paths short. Set `USS_DESKTOP_APP_DATA_ROOT` to override the USS Desktop user-data root. Set `USS_DESKTOP_LOCAL_DATA_ROOT` only when Arduino CLI data should live in a different folder.

Download the pinned Arduino CLI release:

```powershell
.\scripts\Install-ArduinoCli.ps1
```

Publish the desktop app with the bundled toolset copied next to the app output:

```powershell
.\scripts\Publish-Portable.ps1
```

Release publishes are self-contained for `win-x64`. Create a GitHub release by pushing a semantic version tag:

```powershell
git tag v0.1.0
git push origin v0.1.0
```

The GitHub workflow stamps release builds from the tag name and publishes `USS.Desktop-win-x64.zip` with the bundled updater.
