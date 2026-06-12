[CmdletBinding()]
param(
  [string] $InfPath = ".\drivers\UlanziAdapter.Filter\UlanziAdapter.Filter.inf"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $InfPath)) {
  throw "INF not found: $InfPath"
}

Write-Host "Installing driver package. This requires Administrator privileges and a signed/test-signed package."
pnputil /add-driver $InfPath /install
