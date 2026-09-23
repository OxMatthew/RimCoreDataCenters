<#
.SYNOPSIS
  Regenerates the original placeholder textures, sounds and (with -Preview) the Workshop preview image.
#>
[CmdletBinding()]
param([switch]$Preview)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$assetArgs = @("run", "--project", (Join-Path $root "Tools\AssetGen"), "-c", "Release", "--", $root)
if ($Preview) { $assetArgs += "--preview" }

& dotnet @assetArgs
if ($LASTEXITCODE -ne 0) {
    throw "Asset generation failed (dotnet exit code $LASTEXITCODE)."
}
