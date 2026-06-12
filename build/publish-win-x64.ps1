[CmdletBinding()]
param(
  [ValidateSet("Debug", "Release")]
  [string] $Configuration = "Release",

  [ValidateSet("win-x64", "win-arm64", "win-x86")]
  [string] $Runtime = "win-x64",

  [string] $OutputDir,

  [switch] $NoClean,

  [switch] $FrameworkDependent,

  [switch] $NoDotNetBootstrap
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$solution = Join-Path $root "UlanziAdapter.sln"
$project = Join-Path $root "src\UlanziAdapter.App\UlanziAdapter.App.csproj"
$nugetConfig = Join-Path $root "NuGet.config"
$toolsDir = Join-Path $root ".tools"
$localDotNetDir = Join-Path $toolsDir "dotnet"
$isWindowsOs = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)
$dotnetBinary = if ($isWindowsOs) { "dotnet.exe" } else { "dotnet" }
$localDotNet = Join-Path $localDotNetDir $dotnetBinary

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
  $OutputDir = Join-Path $root "artifacts\publish\$Runtime"
}

function Resolve-DotNet {
  $globalDotNet = Get-Command dotnet -ErrorAction SilentlyContinue
  if ($null -ne $globalDotNet) {
    return $globalDotNet.Source
  }

  if (Test-Path $localDotNet) {
    return $localDotNet
  }

  if ($NoDotNetBootstrap) {
    throw "dotnet SDK not found. Install .NET SDK 8 or later, then run this script again: https://dotnet.microsoft.com/download"
  }

  Write-Host "dotnet SDK not found. Downloading local .NET SDK 8 into $localDotNetDir..."
  New-Item -ItemType Directory -Force -Path $toolsDir | Out-Null

  $installArchitecture = switch ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()) {
    "X64" { "x64" }
    "X86" { "x86" }
    "Arm64" { "arm64" }
    default { "x64" }
  }

  if ($isWindowsOs) {
    $installScript = Join-Path $toolsDir "dotnet-install.ps1"
    Invoke-WebRequest `
      -Uri "https://dot.net/v1/dotnet-install.ps1" `
      -OutFile $installScript `
      -UseBasicParsing

    $powershellExe = (Get-Process -Id $PID).Path
    $installArgs = @(
      "-NoProfile",
      "-ExecutionPolicy", "Bypass",
      "-File", $installScript,
      "-Channel", "8.0",
      "-InstallDir", $localDotNetDir,
      "-Architecture", $installArchitecture,
      "-NoPath"
    )

    & $powershellExe @installArgs | Out-Host
  } else {
    $installShellScript = Join-Path $toolsDir "dotnet-install.sh"
    Invoke-WebRequest `
      -Uri "https://dot.net/v1/dotnet-install.sh" `
      -OutFile $installShellScript `
      -UseBasicParsing

    & bash $installShellScript `
      --channel "8.0" `
      --install-dir $localDotNetDir `
      --architecture $installArchitecture `
      --no-path | Out-Host
  }

  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

  if (-not (Test-Path $localDotNet)) {
    throw "Local dotnet bootstrap completed but executable was not found: $localDotNet"
  }

  return $localDotNet
}

$dotnet = Resolve-DotNet

if (-not (Test-Path $solution)) {
  throw "Solution not found: $solution"
}

if (-not (Test-Path $project)) {
  throw "App project not found: $project"
}

if (-not (Test-Path $nugetConfig)) {
  throw "NuGet config not found: $nugetConfig"
}

if (-not $NoClean -and (Test-Path $OutputDir)) {
  Remove-Item $OutputDir -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$selfContained = if ($FrameworkDependent) { "false" } else { "true" }

Write-Host "Restoring packages..."
& $dotnet restore $solution --configfile $nugetConfig
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Restoring runtime assets for $Runtime..."
& $dotnet restore $project -r $Runtime --configfile $nugetConfig
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Building solution..."
& $dotnet build $solution -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Publishing $Runtime executable..."
& $dotnet publish $project `
  -c $Configuration `
  -r $Runtime `
  --self-contained $selfContained `
  --no-restore `
  -o $OutputDir `
  /p:PublishSingleFile=true `
  /p:IncludeNativeLibrariesForSelfExtract=true `
  /p:DebugType=embedded
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$exe = Join-Path $OutputDir "UlanziAdapter.App.exe"
if (-not (Test-Path $exe)) {
  throw "Publish completed but executable was not found: $exe"
}

Write-Host ""
Write-Host "Build completed."
Write-Host "Executable: $exe"
