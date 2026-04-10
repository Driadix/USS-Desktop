param(
    [string]$Version,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RepositoryRoot {
    return [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
}

function Get-PinnedManifest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot
    )

    $manifestPath = Join-Path $RepositoryRoot "toolsets\arduino-cli.manifest.json"
    return Get-Content -Raw $manifestPath | ConvertFrom-Json
}

function Get-ReleaseMetadata {
    param(
        [Parameter(Mandatory = $true)]
        [string]$VersionValue
    )

    $release = Invoke-RestMethod -UseBasicParsing "https://api.github.com/repos/arduino/arduino-cli/releases/tags/v$VersionValue"
    $assetName = "arduino-cli_${VersionValue}_Windows_64bit.zip"
    $asset = $release.assets | Where-Object { $_.name -eq $assetName } | Select-Object -First 1

    if ($null -eq $asset) {
        throw "Release v$VersionValue does not contain asset '$assetName'."
    }

    $digest = $asset.digest
    if ([string]::IsNullOrWhiteSpace($digest) -or -not $digest.StartsWith("sha256:", [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Release v$VersionValue does not expose a SHA-256 digest for '$assetName'."
    }

    return [pscustomobject]@{
        Version = $VersionValue
        AssetName = $asset.name
        DownloadUrl = $asset.browser_download_url
        Sha256 = $digest.Substring(7).ToLowerInvariant()
        ReleaseUrl = $release.html_url
    }
}

$repositoryRoot = Get-RepositoryRoot
$manifest = Get-PinnedManifest -RepositoryRoot $repositoryRoot
$pinned = $manifest.arduinoCli

$metadata =
    if ([string]::IsNullOrWhiteSpace($Version) -or $Version -eq $pinned.version) {
        [pscustomobject]@{
            Version = $pinned.version
            AssetName = $pinned.assetName
            DownloadUrl = $pinned.downloadUrl
            Sha256 = $pinned.sha256.ToLowerInvariant()
            ReleaseUrl = $pinned.releaseUrl
        }
    }
    else {
        Get-ReleaseMetadata -VersionValue $Version
    }

$toolsetsRoot = Join-Path $repositoryRoot "toolsets"
$destinationPath = Join-Path $toolsetsRoot ("arduino-cli-{0}-win64" -f $metadata.Version)
$executablePath = Join-Path $destinationPath "arduino-cli.exe"

if ((Test-Path $executablePath) -and -not $Force.IsPresent) {
    Write-Host "arduino-cli $($metadata.Version) is already installed at $destinationPath"
    & $executablePath version
    exit 0
}

$temporaryZipPath = Join-Path ([System.IO.Path]::GetTempPath()) ("arduino-cli-{0}-{1}.zip" -f $metadata.Version, [Guid]::NewGuid().ToString("N"))
$stagingPath = Join-Path ([System.IO.Path]::GetTempPath()) ("arduino-cli-{0}-{1}" -f $metadata.Version, [Guid]::NewGuid().ToString("N"))
$stagedExecutablePath = Join-Path $stagingPath "arduino-cli.exe"

Write-Host "Downloading Arduino CLI $($metadata.Version) from $($metadata.ReleaseUrl)"
$ProgressPreference = 'SilentlyContinue'
try {
    Invoke-WebRequest -UseBasicParsing -Uri $metadata.DownloadUrl -OutFile $temporaryZipPath

    $downloadHash = (Get-FileHash -LiteralPath $temporaryZipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($downloadHash -ne $metadata.Sha256) {
        throw "Checksum mismatch for '$($metadata.AssetName)'. Expected '$($metadata.Sha256)', actual '$downloadHash'."
    }

    New-Item -ItemType Directory -Path $stagingPath -Force | Out-Null
    Expand-Archive -LiteralPath $temporaryZipPath -DestinationPath $stagingPath -Force

    if (-not (Test-Path $stagedExecutablePath)) {
        throw "arduino-cli.exe was not found after extracting '$($metadata.AssetName)'."
    }

    if (Test-Path $destinationPath) {
        Remove-Item -LiteralPath $destinationPath -Recurse -Force
    }

    Move-Item -LiteralPath $stagingPath -Destination $destinationPath
}
finally {
    if (Test-Path $temporaryZipPath) {
        Remove-Item -LiteralPath $temporaryZipPath -Force
    }

    if (Test-Path $stagingPath) {
        Remove-Item -LiteralPath $stagingPath -Recurse -Force
    }
}

Write-Host "Installed Arduino CLI $($metadata.Version) to $destinationPath"
& $executablePath version
