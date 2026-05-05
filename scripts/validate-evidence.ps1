#Requires -Version 5.1
<#
.SYNOPSIS
    Validate operational evidence markdown fixtures and explicit evidence files.
#>
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$validator = Join-Path $repoRoot 'scripts/validate-operational-evidence.py'

$python = $null
foreach ($candidate in @('python', 'python3', 'py')) {
    $resolved = Get-Command $candidate -ErrorAction SilentlyContinue
    if ($resolved) { $python = $resolved; break }
}
if (-not $python) {
    Write-Error "ERROR: Python is required to run scripts/validate-operational-evidence.py."
    exit 1
}

& $python.Source $validator @args
exit $LASTEXITCODE
