<#
.SYNOPSIS
  Copies a mod package into the RimWorld Mods folder (mirror copy, replaces the previous install).
.PARAMETER Source
  Folder containing the mod package (About, Defs, 1.5, ...). Defaults to the dev copy (Mod\).
  Pass Release\RimCoreDataCenters to install the exact release package instead.
#>
[CmdletBinding()]
param(
    [string]$Source,
    [string]$RimWorldDir = $env:RIMWORLD_DIR
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if (-not $Source) { $Source = Join-Path $root "Mod" }
if (-not $RimWorldDir) { $RimWorldDir = "C:\Program Files (x86)\Steam\steamapps\common\RimWorld" }

if (-not (Test-Path (Join-Path $Source "About\About.xml"))) {
    throw "Not a mod package (About\About.xml missing): $Source"
}
$modsDir = Join-Path $RimWorldDir "Mods"
if (-not (Test-Path $modsDir)) { throw "RimWorld Mods folder not found: $modsDir" }

$target = Join-Path $modsDir "RimCoreDataCenters"
# Only ever touch our own folder; never anything else in Mods.
if ((Split-Path -Leaf $target) -ne "RimCoreDataCenters") { throw "Refusing to write outside the mod folder." }

& robocopy $Source $target /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE" }
Write-Host "Installed $Source -> $target"
