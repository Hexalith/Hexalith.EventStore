#Requires -Version 5.1
<#
.SYNOPSIS
    Local documentation validation — mirrors docs-validation.yml CI pipeline.
.DESCRIPTION
    Runs markdownlint, lychee link checking, operational evidence fixture validation, deferred-work governance reporting, and sample build/test locally.
.EXAMPLE
    .\scripts\validate-docs.ps1
#>
$ErrorActionPreference = 'Stop'

$docsGlob = @(
    'docs/**/*.md'
    'README.md'
    'CONTRIBUTING.md'
    'CHANGELOG.md'
    'CODE_OF_CONDUCT.md'
)

# --- Prerequisite checks ---
foreach ($cmd in @('node', 'npx', 'lychee', 'dotnet')) {
    if (-not (Get-Command $cmd -ErrorAction SilentlyContinue)) {
        Write-Error "ERROR: '$cmd' is not installed or not in PATH. See docs/getting-started/prerequisites.md."
        exit 1
    }
}
$pythonFound = $false
foreach ($candidate in @('python', 'python3', 'py')) {
    if (Get-Command $candidate -ErrorAction SilentlyContinue) { $pythonFound = $true; break }
}
if (-not $pythonFound) {
    Write-Error "ERROR: Python is required (any of: python, python3, py). See docs/getting-started/prerequisites.md."
    exit 1
}

# --- Stage 1: Markdown Linting ---
Write-Host "`n=== Stage 1/4: Markdown Linting ===" -ForegroundColor Cyan
npx markdownlint-cli2 @docsGlob
if ($LASTEXITCODE -ne 0) { Write-Error "FAILED: Markdown linting"; exit 1 }
Write-Host "PASSED: Markdown linting" -ForegroundColor Green

# --- Stage 2: Link Checking ---
Write-Host "`n=== Stage 2/4: Link Checking ===" -ForegroundColor Cyan
lychee --config lychee.toml @docsGlob
if ($LASTEXITCODE -ne 0) { Write-Error "FAILED: Link checking"; exit 1 }
Write-Host "PASSED: Link checking" -ForegroundColor Green

# --- Stage 3: Operational Evidence Validator Fixtures ---
Write-Host "`n=== Stage 3/4: Operational Evidence Validator Fixtures ===" -ForegroundColor Cyan
.\scripts\validate-evidence.ps1 --self-test
if ($LASTEXITCODE -ne 0) { Write-Error "FAILED: Operational evidence validator fixtures"; exit 1 }
Write-Host "PASSED: Operational evidence validator fixtures" -ForegroundColor Green

# --- Stage 4: Deferred-Work Governance Report (advisory) ---
Write-Host "`n=== Stage 4/5: Deferred-Work Governance Report (advisory) ===" -ForegroundColor Cyan
.\scripts\check-deferred-work.ps1 --legacy-advisory _bmad-output/implementation-artifacts/deferred-work.md
if ($LASTEXITCODE -ne 0) {
    Write-Warning "ADVISORY: Deferred-work governance reported blocking findings; local docs validation does not fail on legacy ledger findings yet."
}
Write-Host "PASSED: Deferred-work governance advisory report completed" -ForegroundColor Green

# --- Stage 5: Sample Build & Test ---
Write-Host "`n=== Stage 5/5: Sample Build & Test ===" -ForegroundColor Cyan
dotnet restore samples/Hexalith.EventStore.Sample.Tests/
if ($LASTEXITCODE -ne 0) { Write-Error "FAILED: Sample restore (samples)"; exit 1 }
dotnet restore tests/Hexalith.EventStore.Sample.Tests/
if ($LASTEXITCODE -ne 0) { Write-Error "FAILED: Sample restore (tests)"; exit 1 }
dotnet build samples/Hexalith.EventStore.Sample.Tests/ --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) { Write-Error "FAILED: Sample build (samples)"; exit 1 }
dotnet build tests/Hexalith.EventStore.Sample.Tests/ --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) { Write-Error "FAILED: Sample build (tests)"; exit 1 }
dotnet test tests/Hexalith.EventStore.Sample.Tests/ --configuration Release --no-build
if ($LASTEXITCODE -ne 0) { Write-Error "FAILED: Sample tests (tests)"; exit 1 }
dotnet test samples/Hexalith.EventStore.Sample.Tests/ --configuration Release --no-build
if ($LASTEXITCODE -ne 0) { Write-Error "FAILED: Sample tests (samples)"; exit 1 }
Write-Host "PASSED: Sample build & test" -ForegroundColor Green

Write-Host "`n=== All validations passed ===" -ForegroundColor Green
