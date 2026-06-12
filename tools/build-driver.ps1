[CmdletBinding()]
param(
  [ValidateSet("Debug", "Release")]
  [string] $Configuration = "Release",

  [ValidateSet("x64")]
  [string] $Platform = "x64"
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $root "drivers\UlanziAdapter.Filter\UlanziAdapter.Filter.vcxproj"

if (-not (Test-Path $project)) {
  throw "Driver project not found: $project"
}

$msbuild = Get-Command msbuild.exe -ErrorAction SilentlyContinue
if ($null -eq $msbuild) {
  $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
  if (Test-Path $vswhere) {
    $msbuildPath = & $vswhere -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
    if ($msbuildPath) {
      $msbuild = Get-Item $msbuildPath
    }
  }
}

if ($null -eq $msbuild) {
  throw "MSBuild not found. Install Visual Studio with the Windows Driver Kit, then run from Developer PowerShell."
}

& $msbuild.FullName $project /p:Configuration=$Configuration /p:Platform=$Platform /m
exit $LASTEXITCODE
