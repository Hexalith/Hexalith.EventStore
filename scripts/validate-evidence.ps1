#Requires -Version 5.1
<#
.SYNOPSIS
    Validate operational evidence markdown fixtures and explicit evidence files.
#>
$ErrorActionPreference = 'Stop'

$python = Get-Command python -ErrorAction SilentlyContinue
if (-not $python) {
    $python = Get-Command py -ErrorAction SilentlyContinue
}
if (-not $python) {
    Write-Error "ERROR: Python is required to run scripts/validate-operational-evidence.py."
    exit 1
}

& $python.Source scripts/validate-operational-evidence.py @args
exit $LASTEXITCODE
