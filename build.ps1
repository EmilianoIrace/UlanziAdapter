$ErrorActionPreference = "Stop"

$publishScript = Join-Path $PSScriptRoot "build\publish-win-x64.ps1"

if (-not (Test-Path $publishScript)) {
  throw "Build script not found: $publishScript"
}

& $publishScript @args
exit $LASTEXITCODE
