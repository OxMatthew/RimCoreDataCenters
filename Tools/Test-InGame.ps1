<#
.SYNOPSIS
  Launches RimWorld with ONLY Core + this mod, in an isolated, throwaway profile, and runs the
  in-game self-test. Never touches your real RimWorld settings, saves or mod list.
.DESCRIPTION
  RimWorld is started with -savedatafolder pointing at .testprofile (inside this repository), a
  dedicated log file, -quicktest (auto-generates a small test colony) and -rcdc-selftest (makes
  the mod run its scripted checks and then quit the game). Results are read from the log file.
  Only the process started by this script is ever stopped.
.PARAMETER Seconds
  Give up waiting after this many seconds.
.PARAMETER NoSelfTest
  Just boot the game with the mod (no scripted checks) and leave it open.
.PARAMETER Scene
  Build a working demo data center for screenshots and leave the game open.
#>
[CmdletBinding()]
param(
    [int]$Seconds = 900,
    [switch]$NoSelfTest,
    [switch]$Scene,
    [string]$RimWorldDir = $env:RIMWORLD_DIR,
    [string]$ProfileName = ".testprofile",
    [string]$ExpectPackage
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if (-not $RimWorldDir) { $RimWorldDir = "C:\Program Files (x86)\Steam\steamapps\common\RimWorld" }
$exe = Join-Path $RimWorldDir "RimWorldWin64.exe"
if (-not (Test-Path $exe)) { throw "RimWorld executable not found: $exe" }

function Get-TreeHash([string]$dir) {
    $lines = Get-ChildItem $dir -Recurse -File | Sort-Object FullName | ForEach-Object {
        "$($_.FullName.Substring($dir.Length + 1).Replace('\', '/')):$((Get-FileHash $_.FullName -Algorithm SHA256).Hash)"
    }
    $sha = [System.Security.Cryptography.SHA256]::Create()
    return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes(($lines -join "`n")))) -replace '-', '').ToLower()
}

if ($ExpectPackage) {
    # Prove the game is loading exactly the release package (byte-for-byte), not a stale or dev copy.
    $installed = Join-Path $RimWorldDir "Mods\RimCoreDataCenters"
    $expected = Get-TreeHash $ExpectPackage
    $actual = Get-TreeHash $installed
    if ($expected -ne $actual) { throw "Installed mod differs from the release package ($installed vs $ExpectPackage). Run Install-ToGame.ps1 -Source <package> first." }
    Write-Host "Installed mod is byte-identical to the release package (tree hash $($expected.Substring(0, 16))...)"
}

$profile = Join-Path $root $ProfileName
$config = Join-Path $profile "Config"
if (Test-Path $profile) {
    # Fresh profile every run so results are reproducible. Only ever delete our own profile folder.
    if ((Split-Path -Leaf $profile) -ne $ProfileName) { throw "Refusing to delete unexpected path." }
    Remove-Item -Recurse -Force $profile
}
New-Item -ItemType Directory -Force $config | Out-Null

$version = (Get-Content (Join-Path $RimWorldDir "Version.txt") -Raw).Trim()
@"
<?xml version="1.0" encoding="utf-8"?>
<ModsConfigData>
  <version>$version</version>
  <activeMods>
    <li>ludeon.rimworld</li>
    <li>rimcore.datacenters</li>
  </activeMods>
  <knownExpansions />
</ModsConfigData>
"@ | Set-Content -Encoding UTF8 (Join-Path $config "ModsConfig.xml")

@"
<?xml version="1.0" encoding="utf-8"?>
<Prefs>
  <devMode>True</devMode>
  <runInBackground>True</runInBackground>
  <volumeMaster>0</volumeMaster>
  <adaptiveTrainingEnabled>False</adaptiveTrainingEnabled>
  <pauseOnError>False</pauseOnError>
  <pauseOnLoad>False</pauseOnLoad>
  <automaticPauseMode>Never</automaticPauseMode>
  <screenWidth>1600</screenWidth>
  <screenHeight>900</screenHeight>
  <fullscreen>False</fullscreen>
  <autosaveIntervalDays>999</autosaveIntervalDays>
  <openLogOnWarnings>False</openLogOnWarnings>
</Prefs>
"@ | Set-Content -Encoding UTF8 (Join-Path $config "Prefs.xml")

$log = Join-Path $profile "Player.log"
$gameArgs = @("-screen-fullscreen", "0", "-screen-width", "1600", "-screen-height", "900", "-logFile", "`"$log`"",
              "`"-savedatafolder=$profile`"", "-quicktest")
if (-not $NoSelfTest -and -not $Scene) { $gameArgs += "-rcdc-selftest" }
if ($Scene) { $gameArgs += "-rcdc-scene" }

Write-Host "Launching RimWorld (isolated profile: $profile)"
$proc = Start-Process -FilePath $exe -ArgumentList $gameArgs -PassThru -WorkingDirectory $RimWorldDir
Write-Host "Started test instance, PID $($proc.Id)"

if ($NoSelfTest -or $Scene) {
    Write-Host "Game left running for manual inspection. Log: $log"
    return
}

$deadline = (Get-Date).AddSeconds($Seconds)
$result = $null
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 5
    if (Test-Path $log) {
        $text = Get-Content $log -Raw -ErrorAction SilentlyContinue
        if ($text -match "SELFTEST RESULT") { $result = $true; break }
    }
    if ($proc.HasExited) { break }
}
Start-Sleep -Seconds 3
if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
Write-Host "Log file: $log"
if (Test-Path $log) {
    Get-Content $log | Select-String -Pattern "SELFTEST" | ForEach-Object { $_.Line }
}
if (-not $result) { Write-Warning "Self-test did not report a result (timed out or the game exited early)." ; exit 2 }
