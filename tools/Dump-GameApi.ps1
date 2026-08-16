<#
.SYNOPSIS
  Dump type, member and IL-body signatures out of Valheim's assemblies.

.DESCRIPTION
  DESIGN.md section 12 splits the game API into what has been verified and what hasn't, and CLAUDE.md
  requires anything game-facing to be checked before it is built on. This is the tool that check was
  made with, kept in the repo so the claims in section 12 stay reproducible rather than being trusted.

  It reads metadata with Mono.Cecil - which BepInEx already ships, so there is nothing to install -
  and never loads the assemblies into the process, so it works fine against Mono/Unity builds.

  Nothing is copied out of the game folder and nothing is written anywhere.

  Kept ASCII-only on purpose: Windows PowerShell 5.1 reads an unsigned .ps1 as the system ANSI
  codepage, so a stray em dash in a string is a parse error rather than a typo.

.PARAMETER Type
  Show every field, method and nested type of a type. Nested types are reachable by their short name,
  including compiler-generated ones such as <ConnectPortalsCoroutine>d__40.

.PARAMETER Member
  Search every type for members whose name contains this.

.PARAMETER Filter
  Restrict -Type output to members whose name matches this wildcard.

.PARAMETER IL
  Dump the IL body of a named method. Combine with -Type to disambiguate.

.PARAMETER Assembly
  Assembly in the game's Managed folder to read, without the extension. Defaults to assembly_valheim.

.PARAMETER AssemblyPath
  Full path to any other assembly to read instead - Jotunn, or another mod whose plugin GUID needs
  reading off it. Overrides -Assembly.

.EXAMPLE
  ./tools/Dump-GameApi.ps1 -Type TeleportWorld

.EXAMPLE
  ./tools/Dump-GameApi.ps1 -Type Game -IL ConnectPortals

.EXAMPLE
  ./tools/Dump-GameApi.ps1 -Member IsTeleportable
#>
[CmdletBinding()]
param(
    [string]$Type,
    [string]$Member,
    [string]$Filter,
    [string]$IL,
    [string]$Assembly = "assembly_valheim",
    [string]$AssemblyPath
)

$ErrorActionPreference = "Stop"

# Same source of truth as the build: VALHEIM_INSTALL, falling back to Environment.props so a
# contributor who set the path there once doesn't have to set it twice.
$install = $env:VALHEIM_INSTALL
if (-not $install) {
    $props = Join-Path $PSScriptRoot "..\Environment.props"
    if (Test-Path $props) {
        $install = ([xml](Get-Content $props)).Project.PropertyGroup.VALHEIM_INSTALL
    }
}
if (-not $install -or -not (Test-Path $install)) {
    throw "VALHEIM_INSTALL is not set to an existing folder. Set the environment variable, or copy Environment.props.example to Environment.props. See README.md."
}

$managed = Join-Path $install "valheim_Data\Managed"
if (-not (Test-Path $managed)) { $managed = Join-Path $install "Valheim_Data\Managed" }
$cecil = Join-Path $install "BepInEx\core\Mono.Cecil.dll"
if (-not (Test-Path $cecil)) {
    throw "Mono.Cecil.dll not found at '$cecil'. It ships with BepInEx 5, so install BepInEx into the game folder."
}

Add-Type -Path $cecil

$resolver = New-Object Mono.Cecil.DefaultAssemblyResolver
$resolver.AddSearchDirectory($managed)
$readerParams = New-Object Mono.Cecil.ReaderParameters
$readerParams.AssemblyResolver = $resolver

if ($AssemblyPath) {
    $asmPath = $AssemblyPath
    $resolver.AddSearchDirectory((Split-Path -Parent $AssemblyPath))
    if (-not (Test-Path $asmPath)) { throw "No assembly at '$asmPath'." }
}
else {
    $asmPath = Join-Path $managed "$Assembly.dll"
    if (-not (Test-Path $asmPath)) { throw "No assembly '$Assembly.dll' in '$managed'." }
}
$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($asmPath, $readerParams)

# Flattened so -Type and -IL reach nested types, including the state machines behind coroutines.
$allTypes = New-Object System.Collections.ArrayList
function Add-TypesRecursive($types) {
    foreach ($t in $types) {
        [void]$allTypes.Add($t)
        if ($t.HasNestedTypes) { Add-TypesRecursive $t.NestedTypes }
    }
}
Add-TypesRecursive $asm.MainModule.Types

function Format-Params($method) {
    ($method.Parameters | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ", "
}

function Get-Visibility($m) {
    if ($m.IsPublic) { "public" } elseif ($m.IsPrivate) { "private" } else { "internal" }
}

function Show-Type($t) {
    "TYPE  $($t.FullName)  (base: $($t.BaseType))"
    foreach ($f in $t.Fields) {
        if ($Filter -and $f.Name -notlike "*$Filter*") { continue }
        $static = if ($f.IsStatic) { " static" } else { "" }
        "  field   $(Get-Visibility $f)$static $($f.FieldType.Name) $($f.Name)"
    }
    foreach ($m in $t.Methods) {
        if ($Filter -and $m.Name -notlike "*$Filter*") { continue }
        $static = if ($m.IsStatic) { " static" } else { "" }
        "  method  $(Get-Visibility $m)$static $($m.ReturnType.Name) $($m.Name)($(Format-Params $m))"
    }
    foreach ($n in $t.NestedTypes) { "  nested  $($n.Name)" }
}

if ($IL) {
    foreach ($t in $allTypes) {
        if ($Type -and $t.Name -ne $Type) { continue }
        foreach ($m in $t.Methods) {
            if ($m.Name -ne $IL -or -not $m.HasBody) { continue }
            "=== $($t.Name).$($m.Name)($(Format-Params $m)) : $($m.ReturnType.Name) ==="
            foreach ($v in $m.Body.Variables) { "  .local V_$($v.Index) $($v.VariableType.Name)" }
            foreach ($i in $m.Body.Instructions) { "  $i" }
            ""
        }
    }
    return
}

if ($Type) {
    $matched = $allTypes | Where-Object { $_.Name -eq $Type -or $_.FullName -eq $Type }
    if ($matched) {
        $matched | ForEach-Object { Show-Type $_ }
    }
    else {
        "No type named '$Type'. Close matches:"
        $allTypes | Where-Object { $_.Name -like "*$Type*" } | ForEach-Object { "  $($_.FullName)" }
    }
}

if ($Member) {
    foreach ($t in $allTypes) {
        foreach ($m in $t.Methods) {
            if ($m.Name -like "*$Member*") {
                $static = if ($m.IsStatic) { " static" } else { "" }
                "$($t.Name).$($m.Name) ->$static $($m.ReturnType.Name) ($(Format-Params $m))"
            }
        }
        foreach ($f in $t.Fields) {
            if ($f.Name -like "*$Member*") { "$($t.Name).$($f.Name) -> field $($f.FieldType.Name)" }
        }
    }
}
