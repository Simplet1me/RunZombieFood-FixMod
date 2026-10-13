# Compatible with Windows PowerShell 5.1. Version comes from BepInPlugin.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = $PSScriptRoot
$source = Get-Content -LiteralPath (Join-Path $root 'src\HighRefreshFix.cs') -Raw
$match = [regex]::Match($source, '\[BepInPlugin\("rz\.high-refresh-fix",\s*"[^"]+",\s*"(?<version>\d+\.\d+\.\d+)"\)\]')
if (-not $match.Success) { throw 'Cannot read a major.minor.patch version from BepInPlugin in src/HighRefreshFix.cs.' }
$version = $match.Groups['version'].Value
$releases = Join-Path $root 'releases'
$destination = Join-Path $releases $version

# Compile in staging so failed builds never appear as completed releases.
$staging = Join-Path $releases ('.staging-' + [Guid]::NewGuid().ToString('N'))
$package = Join-Path $staging 'package'
$plugins = Join-Path $package 'BepInEx\plugins'
$previousRelease = $null
try {
    & (Join-Path $root 'build.ps1') -OutputDirectory $plugins
    Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination (Join-Path $package 'README.md')
    $archiveName = "HighRefreshFix-$version.zip"
    $archive = Join-Path $staging $archiveName
    Compress-Archive -Path (Join-Path $package '*') -DestinationPath $archive
    $dll = Join-Path $plugins 'HighRefreshFix.dll'
    $manifest = [ordered]@{
        version = $version
        createdUtc = [DateTime]::UtcNow.ToString('o')
        loader = 'BepInEx 5.4.23.2 x86'
        archive = $archiveName
        archiveSha256 = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
        dllSha256 = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash
        sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | Sort-Object Name | ForEach-Object {
            [ordered]@{ file = $_.Name; sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
        })
    }
    $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $staging 'manifest.json') -Encoding UTF8
    # Only replace a release after compilation and packaging have succeeded.
    $releaseRoot = [IO.Path]::GetFullPath($releases).TrimEnd('\') + '\'
    foreach ($path in @($staging, $destination)) {
        if (-not [IO.Path]::GetFullPath($path).StartsWith($releaseRoot, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Release path escapes output directory: $path"
        }
    }
    if (Test-Path -LiteralPath $destination) {
        $history = Join-Path $releases '.history'
        New-Item -ItemType Directory -Force $history | Out-Null
        $backup = Join-Path $history ("$version-" + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff') + '-' + [Guid]::NewGuid().ToString('N'))
        if (-not [IO.Path]::GetFullPath($backup).StartsWith($releaseRoot, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Backup path escapes output directory: $backup"
        }
        # Directory.Move fails if the destination exists, instead of nesting directories.
        [IO.Directory]::Move($destination, $backup)
        $previousRelease = $backup
    }
    try {
        [IO.Directory]::Move($staging, $destination)
    } catch {
        if ($previousRelease -and -not (Test-Path -LiteralPath $destination)) {
            [IO.Directory]::Move($previousRelease, $destination)
        }
        throw
    }
} catch {
    # Keep failed staging output for diagnosis; never delete existing release files.
    throw "Release failed. Any intermediate files are in $staging. $($_.Exception.Message)"
}

# Numeric sorting: 1.10.0 correctly comes after 1.9.0.
$versions = @(Get-ChildItem -LiteralPath $releases -Directory |
    Where-Object { $_.Name -match '^\d+\.\d+\.\d+$' -and (Test-Path -LiteralPath (Join-Path $_.FullName 'manifest.json')) } |
    Sort-Object { [version]$_.Name } -Descending)
$index = @('# Local releases', '', 'Newest version first. These are local build artifacts.', '')
foreach ($entry in $versions) {
    $index += "- [$($entry.Name)]($($entry.Name)/HighRefreshFix-$($entry.Name).zip)"
}
$index | Set-Content -LiteralPath (Join-Path $releases 'README.md') -Encoding UTF8
Write-Host "Release ready: $destination"
Write-Host "Archive: $(Join-Path $destination $archiveName)"
if ($previousRelease) { Write-Host "Previous build preserved: $previousRelease" }
