<#
.SYNOPSIS
  Compiles the mod assembly into Mod\<RwVersion>\Assemblies.
.DESCRIPTION
  Uses the .NET SDK and whatever RimWorld managed assemblies are installed at RimWorldDir. Set the
  RIMWORLD_DIR environment variable (or pass -RimWorldDir) if RimWorld is not in the default Steam
  location. -RwVersion selects which version-gated code path to compile (1.5 or 1.6) and must match
  the game version actually installed at RimWorldDir - only one can be installed at a time, so
  building both requires switching Steam's beta branch between runs (see docs/BUILD.md).
#>
[CmdletBinding()]
param(
    [string]$RimWorldDir = $env:RIMWORLD_DIR,
    [string]$Configuration = "Release",
    [ValidateSet("1.5", "1.6")]
    [string]$RwVersion = "1.5"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "Source\RimCoreDataCenters\RimCoreDataCenters.csproj"

# The mod version in About.xml is the single source of truth; stamp the same number into the assembly.
[xml]$about = Get-Content (Join-Path $root "Mod\About\About.xml") -Raw
$version = $about.ModMetaData.modVersion
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "About.xml modVersion '$version' is not x.y.z" }

$msbuildArgs = @("build", $project, "-c", $Configuration, "-nologo", "-v", "minimal", "/p:Version=$version", "/p:RwVersion=$RwVersion")
if ($RimWorldDir) {
    $msbuildArgs += "/p:RimWorldDir=$RimWorldDir"
}

& dotnet @msbuildArgs
if ($LASTEXITCODE -ne 0) {
    throw "Build failed (dotnet exit code $LASTEXITCODE)."
}

$dll = Join-Path $root "Mod\$RwVersion\Assemblies\RimCoreDataCenters.dll"
if (-not (Test-Path $dll)) {
    throw "Expected assembly not found: $dll"
}
Write-Host "Built $dll ($([math]::Round((Get-Item $dll).Length / 1KB, 1)) KB)"
