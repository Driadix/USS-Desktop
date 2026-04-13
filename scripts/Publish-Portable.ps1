param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Output = "",
    [string]$Version = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$projectPath = Join-Path $repositoryRoot "src\USS.Desktop.App\USS.Desktop.App.csproj"
$updaterProjectPath = Join-Path $repositoryRoot "src\USS.Desktop.Updater\USS.Desktop.Updater.csproj"
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
$selfContained = $Configuration -eq "Release"
$publishVersion = $Version.Trim().TrimStart("v")
$versionBuildArguments = @()
if (-not [string]::IsNullOrWhiteSpace($publishVersion)) {
    $versionParts = $publishVersion.Split(".")
    if ($versionParts.Count -ne 3) {
        throw "Version must have major.minor.patch format. Received '$Version'."
    }

    $fourPartVersion = "$publishVersion.0"
    $versionBuildArguments = @(
        "-p:Version=$publishVersion",
        "-p:AssemblyVersion=$fourPartVersion",
        "-p:FileVersion=$fourPartVersion",
        "-p:InformationalVersion=$publishVersion",
        "-p:IncludeSourceRevisionInInformationalVersion=false"
    )
}

if (Test-Path $resolvedOutput) {
    Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
}

dotnet restore $projectPath --runtime $Runtime
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed for runtime '$Runtime'."
}

dotnet restore $updaterProjectPath --runtime $Runtime
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed for updater runtime '$Runtime'."
}

$publishArguments = @(
    "publish",
    $projectPath,
    "--configuration", $Configuration,
    "--runtime", $Runtime,
    "--self-contained", $selfContained.ToString().ToLowerInvariant(),
    "--no-restore"
)
$publishArguments += $versionBuildArguments

if (-not [string]::IsNullOrWhiteSpace($Output)) {
    $publishArguments += @("--output", $resolvedOutput)
}

dotnet @publishArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed."
}

$updaterOutput = Join-Path $resolvedOutput "updater"
$updaterPublishArguments = @(
    "publish",
    $updaterProjectPath,
    "--configuration", $Configuration,
    "--runtime", $Runtime,
    "--self-contained", $selfContained.ToString().ToLowerInvariant(),
    "--output", $updaterOutput,
    "--no-restore"
)
$updaterPublishArguments += $versionBuildArguments

if ($selfContained) {
    $updaterPublishArguments += @("-p:PublishSingleFile=true")
}

dotnet @updaterPublishArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed for updater."
}

$updaterExecutablePath = Join-Path $updaterOutput "USS.Desktop.Updater.exe"
if (-not (Test-Path $updaterExecutablePath)) {
    throw "Updater executable was not copied to publish output: $updaterExecutablePath"
}

$bundledCliPath = Join-Path $resolvedOutput ("toolsets\arduino-cli-{0}-win64\arduino-cli.exe" -f $expectedVersion)
if (-not (Test-Path $bundledCliPath)) {
    throw "Bundled Arduino CLI was not copied to publish output: $bundledCliPath"
}

Write-Host "Bundled Arduino CLI copied to $bundledCliPath"
Write-Host "Updater copied to $updaterExecutablePath"
