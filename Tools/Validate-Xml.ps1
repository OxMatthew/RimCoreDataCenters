<#
.SYNOPSIS
  Validates the mod package XML: well-formedness, About.xml required fields, def-name prefixes,
  and cross references (every Keyed key used in C# exists, every defName referenced by another def exists).
.PARAMETER Package
  The mod package folder to validate (default: Mod\).
#>
[CmdletBinding()]
param([string]$Package)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if (-not $Package) { $Package = Join-Path $root "Mod" }
$errors = New-Object System.Collections.Generic.List[string]
function Fail([string]$msg) { $script:errors.Add($msg); Write-Host "  FAIL: $msg" -ForegroundColor Red }

Write-Host "Validating XML in $Package"

# 1. Well-formedness of every XML file.
$xmlFiles = Get-ChildItem $Package -Recurse -Filter *.xml
foreach ($f in $xmlFiles) {
    try {
        $doc = New-Object System.Xml.XmlDocument
        $doc.PreserveWhitespace = $false
        $doc.Load($f.FullName)
    } catch {
        Fail "$($f.FullName.Substring($Package.Length + 1)): $($_.Exception.InnerException.Message)"
    }
}
Write-Host "  checked $($xmlFiles.Count) XML files for well-formedness"

# 2. About.xml
$aboutPath = Join-Path $Package "About\About.xml"
if (-not (Test-Path $aboutPath)) {
    Fail "About\About.xml is missing"
} else {
    [xml]$about = Get-Content $aboutPath -Raw
    $meta = $about.ModMetaData
    foreach ($field in "name", "author", "packageId", "description") {
        if ([string]::IsNullOrWhiteSpace($meta.$field)) { Fail "About.xml: <$field> is missing or empty" }
    }
    if ($meta.packageId -notmatch '^[A-Za-z0-9]+(\.[A-Za-z0-9_\-]+)+$') { Fail "About.xml: packageId '$($meta.packageId)' has an invalid format" }
    if ($meta.packageId -match '(?i)ludeon') { Fail "About.xml: packageId must not contain 'Ludeon'" }
    if (-not $meta.supportedVersions.li) { Fail "About.xml: <supportedVersions> is missing" }
    if ($meta.modVersion -notmatch '^\d+\.\d+\.\d+$') { Fail "About.xml: <modVersion> '$($meta.modVersion)' is not semantic (x.y.z)" }
    Write-Host "  About.xml: $($meta.name) $($meta.modVersion), packageId $($meta.packageId), supports $($meta.supportedVersions.li -join ', ')"
}

# 3. Def names: every def defined by this mod carries the RCDC_ prefix, and none are duplicated.
$defNames = @{}
$defNamesAny = @{}
$defFiles = Get-ChildItem (Join-Path $Package "Defs") -Recurse -Filter *.xml -ErrorAction SilentlyContinue
foreach ($f in $defFiles) {
    [xml]$doc = Get-Content $f.FullName -Raw
    foreach ($def in $doc.Defs.ChildNodes) {
        if ($def.NodeType -ne "Element") { continue }
        $name = $def.defName
        if ([string]::IsNullOrEmpty($name)) { continue }
        if ($name -notlike "RCDC_*") { Fail "$($f.Name): defName '$name' is missing the RCDC_ prefix" }
        $typedKey = "$($def.LocalName):$name"
        if ($defNames.ContainsKey($typedKey)) { Fail "$($f.Name): duplicate $($def.LocalName) defName '$name'" }
        $defNames[$typedKey] = $true
        $defNamesAny[$name] = $true
    }
}
Write-Host "  found $($defNames.Count) defs (all prefixed RCDC_)"

# 4. Every RCDC_* token used inside defs, patches or C# must be defined (defs), or be a Keyed string.
$keyed = @{}
$keyedFiles = Get-ChildItem (Join-Path $Package "Languages") -Recurse -Filter *.xml -ErrorAction SilentlyContinue
foreach ($f in $keyedFiles) {
    [xml]$doc = Get-Content $f.FullName -Raw
    foreach ($n in $doc.LanguageData.ChildNodes) { if ($n.NodeType -eq "Element") { $keyed[$n.LocalName] = $true } }
}
Write-Host "  found $($keyed.Count) keyed strings"

$codeFiles = Get-ChildItem (Join-Path $root "Source") -Recurse -Filter *.cs
$used = @{}
foreach ($f in $codeFiles) {
    $text = Get-Content $f.FullName -Raw
    foreach ($m in [regex]::Matches($text, '(?<!MakeToil\()"(RCDC_[A-Za-z0-9_]+)"')) { $used[$m.Groups[1].Value] = $f.Name }
}
foreach ($key in $used.Keys) {
    if ($key -like "RCDC_Status_*" -or $key -like "RCDC_DetailNet_*") { continue } # built dynamically, checked below
    if ($key.EndsWith("_")) { continue } # a key prefix that C# completes at run time (checked below)
    if ($key -in @("RCDC_SelfTest")) { continue } # save-file name used by the self-test, not a def or string key
    if (-not $keyed.ContainsKey($key) -and -not $defNamesAny.ContainsKey($key)) { Fail "C# ($($used[$key])) references '$key' which is neither a keyed string nor a def" }
}
foreach ($s in "Operational", "NoPower", "NoNetwork", "TooHot", "MaintenanceRequired", "OutputFull") {
    if (-not $keyed.ContainsKey("RCDC_Status_$s")) { Fail "missing keyed string RCDC_Status_$s" }
}
foreach ($s in "None", "NoCoreInRange", "CoreFull", "CoreOffline") {
    if (-not $keyed.ContainsKey("RCDC_DetailNet_$s")) { Fail "missing keyed string RCDC_DetailNet_$s" }
}
# The AI core builds these keys at run time; every one must exist.
foreach ($s in "Offline", "Overheated", "Rebooting", "Sulking", "Online") {
    if (-not $keyed.ContainsKey("RCDC_AiStatus_$s")) { Fail "missing keyed string RCDC_AiStatus_$s" }
}
foreach ($s in "Balanced", "Efficiency", "Stewardship", "Curiosity") {
    foreach ($p in "RCDC_AiDirective_", "RCDC_AiDirectiveDesc_") { if (-not $keyed.ContainsKey("$p$s")) { Fail "missing keyed string $p$s" } }
}
foreach ($s in "None", "ComputeLoan", "Overclock", "Diagnostics") {
    if (-not $keyed.ContainsKey("RCDC_AiBoon_$s")) { Fail "missing keyed string RCDC_AiBoon_$s" }
}
foreach ($s in "ComputeLoan", "Overclock", "Diagnostics") {
    foreach ($p in "Label", "Text", "Accept", "Decline") { if (-not $keyed.ContainsKey("RCDC_AiReq_${s}_$p")) { Fail "missing keyed string RCDC_AiReq_${s}_$p" } }
}
$aiLines = @{ Boot = 3; Idle_Warm = 4; Idle_Neutral = 4; Idle_Cold = 4; Overheat = 3; Service = 3; Milestone = 3; GlitchReboot = 2; GlitchSulk = 2; GlitchCache = 2; Back = 2; Balanced = 2; Efficiency = 2; Stewardship = 2; Curiosity = 2; Thanks = 2; Declined = 2; Intrusion = 2 }
foreach ($topic in $aiLines.Keys) {
    for ($i = 1; $i -le $aiLines[$topic]; $i++) { if (-not $keyed.ContainsKey("RCDC_AiLine_${topic}_$i")) { Fail "missing keyed string RCDC_AiLine_${topic}_$i" } }
}

# 5. Textures referenced by defs exist.
$texRoot = Join-Path $Package "Textures"
foreach ($f in $defFiles) {
    [xml]$doc = Get-Content $f.FullName -Raw
    foreach ($node in $doc.SelectNodes("//texPath")) {
        $tex = $node.InnerText
        if ($tex -notlike "RCDC/*") { continue }
        $base = Join-Path $texRoot $tex
        $multi = ($node.ParentNode.graphicClass -eq "Graphic_Multi")
        $needed = if ($multi) { "north", "east", "south", "west" | ForEach-Object { "${base}_$_.png" } } else { @("$base.png") }
        foreach ($n in $needed) { if (-not (Test-Path $n)) { Fail "$($f.Name): texture missing: $($n.Substring($Package.Length + 1))" } }
    }
    # Architect-menu icons (uiIconPath) are single textures.
    foreach ($node in $doc.SelectNodes("//uiIconPath")) {
        $icon = $node.InnerText
        if ($icon -notlike "RCDC/*") { continue }
        $p = Join-Path $texRoot "$icon.png"
        if (-not (Test-Path $p)) { Fail "$($f.Name): menu icon missing: $($p.Substring($Package.Length + 1))" }
    }
}
# Every research project referenced as a prerequisite by an RCDC def exists (this mod's own or vanilla).
$rcdcResearch = @{}
foreach ($f in $defFiles) {
    [xml]$doc = Get-Content $f.FullName -Raw
    foreach ($rp in $doc.SelectNodes("//ResearchProjectDef")) { $rcdcResearch[$rp.defName] = $true }
}
foreach ($f in $defFiles) {
    [xml]$doc = Get-Content $f.FullName -Raw
    foreach ($li in $doc.SelectNodes("//prerequisites/li | //researchPrerequisites/li")) {
        $name = $li.InnerText
        if ($name -like "RCDC_*" -and -not $rcdcResearch.ContainsKey($name)) { Fail "$($f.Name): research prerequisite '$name' is not defined" }
    }
}
# Sounds referenced by defs exist.
foreach ($f in $defFiles) {
    [xml]$doc = Get-Content $f.FullName -Raw
    foreach ($node in $doc.SelectNodes("//clipPath")) {
        $p = Join-Path (Join-Path $Package "Sounds") $node.InnerText
        if (-not (@(".wav", ".ogg", ".mp3") | Where-Object { Test-Path "$p$_" })) { Fail "$($f.Name): sound clip missing: $($node.InnerText)" }
    }
}

if ($errors.Count -gt 0) {
    Write-Host "XML validation FAILED with $($errors.Count) problem(s)." -ForegroundColor Red
    exit 1
}
Write-Host "XML validation passed." -ForegroundColor Green
exit 0
