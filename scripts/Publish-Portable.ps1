param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Output = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$projectPath = Join-Path $repositoryRoot "src\USS.Desktop.App\USS.Desktop.App.csproj"
$manifest = Get-Content -Raw (Join-Path $repositoryRoot "toolsets\arduino-cli.manifest.json") | ConvertFrom-Json
$expectedVersion = $manifest.arduinoCli.version

& (Join-Path $PSScriptRoot "Install-ArduinoCli.ps1")

$resolvedOutput =
    if (-not [string]::IsNullOrWhiteSpace($Output)) {
        [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $Output))
    }
    else {
        Join-Path $repositoryRoot ("src\USS.Desktop.App\bin\{0}\net10.0-windows\{1}\publish" -f $Configuration, $Runtime)
    }

if (Test-Path $resolvedOutput) {
    Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
}

dotnet restore $projectPath --runtime $Runtime
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed for runtime '$Runtime'."
}

$publishArguments = @(
    "publish",
    $projectPath,
    "--configuration", $Configuration,
    "--runtime", $Runtime,
    "--self-contained", "false",
    "--no-restore"
)

if (-not [string]::IsNullOrWhiteSpace($Output)) {
    $publishArguments += @("--output", $Output)
}

dotnet @publishArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed."
}

$bundledCliPath = Join-Path $resolvedOutput ("toolsets\arduino-cli-{0}-win64\arduino-cli.exe" -f $expectedVersion)
if (-not (Test-Path $bundledCliPath)) {
    throw "Bundled Arduino CLI was not copied to publish output: $bundledCliPath"
}

Write-Host "Bundled Arduino CLI copied to $bundledCliPath"
