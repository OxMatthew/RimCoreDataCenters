<#
.SYNOPSIS
  Builds a clean, Workshop-ready package in Release\RimCoreDataCenters.
.DESCRIPTION
  1. Reads the version from Mod\About\About.xml (the single source of truth).
  2. Builds the assembly for -RwVersion (must match whatever RimWorld version is actually installed
     at RimWorldDir), validates the XML.
  3. Copies ONLY allow-listed content into Release\RimCoreDataCenters (no source, no symbols, no dev files).
  4. Scans every packaged file (including the DLL bytes) for private data: user name, computer name,
     absolute local paths, e-mail addresses, credential-looking strings, and for forbidden dependencies.
  5. Writes a SHA-256 manifest and a zip so the tested package can be proven identical to the shipped one.
  The script never publishes anything.

  Only one RimWorld version can be installed at RimWorldDir at a time, so a single run builds ONE
  version's assembly (whichever -RwVersion says, default 1.5). The package step then includes every
  version folder under Mod\ that About.xml's supportedVersions lists AND that already has a built
  assembly - so to ship both 1.5 and 1.6, build 1.5 on a 1.5 install, then build 1.6 on a 1.6 install
  (-SkipBuild the second time is not needed; each run only rebuilds its own -RwVersion), then run
  this script once more with -SkipBuild to package both without touching either assembly.
#>
[CmdletBinding()]
param(
    [string]$RimWorldDir = $env:RIMWORLD_DIR,
    [ValidateSet("1.5", "1.6")]
    [string]$RwVersion = "1.5",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$mod = Join-Path $root "Mod"
$releaseRoot = Join-Path $root "Release"
$packageName = "RimCoreDataCenters"
$package = Join-Path $releaseRoot $packageName

[xml]$about = Get-Content (Join-Path $mod "About\About.xml") -Raw
$version = $about.ModMetaData.modVersion
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "About.xml modVersion '$version' is not x.y.z" }
Write-Host "== Releasing $($about.ModMetaData.name) $version (packageId $($about.ModMetaData.packageId))"

# ---- 1. build + validate ---------------------------------------------------------------------
if (-not $SkipBuild) {
    $buildArgs = @{ RwVersion = $RwVersion }
    if ($RimWorldDir) { $buildArgs.RimWorldDir = $RimWorldDir }
    & (Join-Path $PSScriptRoot "Build.ps1") @buildArgs
}
& (Join-Path $PSScriptRoot "Validate-Xml.ps1") -Package $mod
if ($LASTEXITCODE -ne 0) { throw "XML validation failed." }

# ---- 2. assemble the package from an allow-list -------------------------------------------------
if (Test-Path $releaseRoot) {
    if ((Split-Path -Leaf $releaseRoot) -ne "Release") { throw "Refusing to delete an unexpected path." }
    Remove-Item -Recurse -Force $releaseRoot
}
New-Item -ItemType Directory -Force $package | Out-Null

# Package every version folder About.xml declares support for AND that already has a built
# assembly (only the version just built with -RwVersion is guaranteed fresh; others must have
# been built in an earlier run against their own matching RimWorld install - see script header).
$declaredVersions = @($about.ModMetaData.supportedVersions.li)
$versionFolders = @($declaredVersions | Where-Object { Test-Path (Join-Path $mod "$_\Assemblies\RimCoreDataCenters.dll") })
$missingVersions = @($declaredVersions | Where-Object { $versionFolders -notcontains $_ })
if ($missingVersions.Count -gt 0) {
    Write-Host "  NOTE: About.xml declares $($missingVersions -join ', ') but no built assembly was found under Mod\<version>\Assemblies\ for it - packaging without it." -ForegroundColor Yellow
}
if ($versionFolders.Count -eq 0) { throw "No version folder has a built assembly; nothing to package." }
Write-Host "  packaging version folder(s): $($versionFolders -join ', ')"

$allowedFolders = @("About", "Defs", "Patches", "Languages", "Textures", "Sounds") + $versionFolders
$allowedExtensions = @(".xml", ".png", ".wav", ".dll", ".txt", ".md")
foreach ($folder in $allowedFolders) {
    $src = Join-Path $mod $folder
    if (-not (Test-Path $src)) { throw "Expected folder missing from Mod\: $folder" }
    Get-ChildItem $src -Recurse -File | ForEach-Object {
        $rel = $_.FullName.Substring($mod.Length + 1)
        if ($allowedExtensions -notcontains $_.Extension.ToLower()) {
            throw "Refusing to package unexpected file type: $rel"
        }
        if ($_.Name -in @("PublishedFileId.txt")) {
            throw "PublishedFileId.txt must not be in the dev copy (created by the game on first publish): $rel"
        }
        $dest = Join-Path $package $rel
        New-Item -ItemType Directory -Force (Split-Path -Parent $dest) | Out-Null
        Copy-Item $_.FullName $dest
    }
}
Copy-Item (Join-Path $root "LICENSE") (Join-Path $package "LICENSE.txt")
Copy-Item (Join-Path $root "NOTICE.md") (Join-Path $package "NOTICE.md")
Copy-Item (Join-Path $root "CHANGELOG.md") (Join-Path $package "CHANGELOG.md")

# ---- 3. structure checks ------------------------------------------------------------------------
foreach ($required in @("About\About.xml", "About\Preview.png", "LICENSE.txt", "NOTICE.md")) {
    if (-not (Test-Path (Join-Path $package $required))) { throw "Package is missing $required" }
}
$assemblies = Get-ChildItem $package -Recurse -Filter *.dll
if ($assemblies.Count -ne $versionFolders.Count) { throw "Expected $($versionFolders.Count) assembly/assemblies (one per packaged version), found $($assemblies.Count)." }
$asmNames = @()
foreach ($vf in $versionFolders) {
    $dll = Join-Path $package "$vf\Assemblies\RimCoreDataCenters.dll"
    if (-not (Test-Path $dll)) { throw "Package is missing $vf\Assemblies\RimCoreDataCenters.dll" }
    $asmName = [System.Reflection.AssemblyName]::GetAssemblyName($dll)
    if ($asmName.Version.ToString(3) -ne $version) {
        throw "$vf assembly version $($asmName.Version) does not match About.xml modVersion $version. Rebuild."
    }
    $asmNames += "$vf=$($asmName.Version)"
}
$preview = Get-Item (Join-Path $package "About\Preview.png")
if ($preview.Length -gt 1MB) { throw "Preview.png is $([math]::Round($preview.Length/1KB)) KB; RimWorld/Steam previews must stay under 1 MB." }
Write-Host "  structure ok; assemblies $($asmNames -join ', '); preview $([math]::Round($preview.Length/1KB)) KB"

# ---- 4. privacy / licensing scan --------------------------------------------------------------
$forbidden = New-Object System.Collections.Generic.List[string]
foreach ($token in @($env:USERNAME, $env:COMPUTERNAME, (Split-Path -Leaf $env:USERPROFILE), $env:USERDOMAIN)) {
    if ($token -and $token.Length -ge 3 -and $forbidden -notcontains $token) { $forbidden.Add($token) }
}
Write-Host "  scanning $((Get-ChildItem $package -Recurse -File).Count) files for $($forbidden.Count) private identifiers, absolute paths, addresses and secrets"

$patterns = @(
    @{ Name = "absolute Windows path";  Regex = '[A-Za-z]:\\(Users|Program Files|dev|jarvis|Windows|Documents)' },
    @{ Name = "absolute Unix path";     Regex = '/(Users|home)/[A-Za-z0-9_.-]+/' },
    @{ Name = "e-mail address";         Regex = '[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}' },
    # "PublicKeyToken=" is standard .NET assembly-reference metadata, not a credential.
    @{ Name = "credential keyword";     Regex = '(?i)(password|passwd|api[_-]?key|secret|(?<!PublicKey)token|steamguard|sentry)\s*[:=]' },
    @{ Name = "private key material";   Regex = '-----BEGIN [A-Z ]*PRIVATE KEY-----' },
    @{ Name = "Steam id / auth url";    Regex = '(?i)(7656119\d{10}|steamcommunity\.com/profiles/)' }
)
$problems = New-Object System.Collections.Generic.List[string]
foreach ($file in Get-ChildItem $package -Recurse -File) {
    $rel = $file.FullName.Substring($package.Length + 1)
    $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
    # Search as ASCII and as UTF-16 (the .NET assembly stores string literals as UTF-16).
    $ascii = [System.Text.Encoding]::ASCII.GetString($bytes)
    $utf16 = [System.Text.Encoding]::Unicode.GetString($bytes)
    $utf16b = [System.Text.Encoding]::Unicode.GetString($bytes, 1, $bytes.Length - 1)
    foreach ($view in @($ascii, $utf16, $utf16b)) {
        foreach ($token in $forbidden) {
            if ($view.IndexOf($token, [StringComparison]::OrdinalIgnoreCase) -ge 0) { $problems.Add("$rel contains the private identifier '$token'") }
        }
        if ($file.Extension -ne ".png" -and $file.Extension -ne ".wav") {
            foreach ($p in $patterns) {
                $m = [regex]::Match($view, $p.Regex)
                if ($m.Success) { $problems.Add("$rel matches: $($p.Name) [$($m.Value)]") }
            }
        }
    }
    foreach ($bad in @("0Harmony", "HugsLib", "MonoMod", "Newtonsoft", "System.Net.Http", "System.Diagnostics.Process", "UnityWebRequest", "WebClient", "HttpWebRequest")) {
        if ($file.Extension -eq ".dll" -and ($ascii.Contains($bad) -or $utf16.Contains($bad))) { $problems.Add("$rel references forbidden dependency/API '$bad'") }
    }
}
$problems = @($problems | Sort-Object -Unique)
if ($problems.Count -gt 0) {
    $problems | ForEach-Object { Write-Host "  PROBLEM: $_" -ForegroundColor Red }
    throw "Privacy/licensing scan failed with $($problems.Count) problem(s). Nothing was released."
}
Write-Host "  privacy and dependency scan clean"

# ---- 5. manifest + zip -----------------------------------------------------------------------
$manifest = Join-Path $releaseRoot "$packageName-$version.sha256.txt"
Get-ChildItem $package -Recurse -File | Sort-Object FullName | ForEach-Object {
    $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower()
    "$hash  $($_.FullName.Substring($package.Length + 1).Replace('\', '/'))"
} | Set-Content -Encoding ASCII $manifest
$zip = Join-Path $releaseRoot "$packageName-$version.zip"
Compress-Archive -Path $package -DestinationPath $zip -CompressionLevel Optimal
Write-Host "== Release package ready: $package"
Write-Host "   files: $((Get-ChildItem $package -Recurse -File).Count)   manifest: $manifest"
Write-Host "   archive: $zip"
