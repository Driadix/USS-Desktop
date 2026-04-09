# Tech Stack

## Chosen Stack

- Language: C#
- Runtime: .NET 10
- UI: WPF
- Build tooling: `dotnet` CLI
- Configuration format: YAML
- Source control: Git

## Why .NET 10 + WPF

This is the most straightforward stack for the current problem constraints:

- Windows-only target
- need for a polished desktop UI
- strong process execution support
- simple portable deployment model
- mature diagnostics and logging ecosystem

## Proposed Libraries

### Required

- `YamlDotNet` for `uss.yaml` parsing/serialization
- `CommunityToolkit.Mvvm` for MVVM structure
- `Serilog` for logs

### Optional

- `CliWrap` if process execution code becomes verbose
- `FluentAssertions` for tests
- `xUnit` or `NUnit` for automated test projects

## Packaging Direction

Portable distribution should look like this:

- `uss.exe`
- `toolsets/arduino-cli-<version>/`
- `toolsets/...`
- app-owned cache directories

To the operator it behaves like one portable tool, even if the internal layout is folder-based.

## Tooling Direction for v1

The only build/flash runtime adapter in v1 is `arduino-cli`.

That means:

- compile goes through `arduino-cli compile`
- upload goes through `arduino-cli upload`
- prebuilt artifact upload also goes through `arduino-cli upload`

This keeps the first version conceptually small and supportable.
