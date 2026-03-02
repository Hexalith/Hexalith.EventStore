#Requires -Version 5.1
<#
.SYNOPSIS
    Local documentation validation — mirrors docs-validation.yml CI pipeline.
.DESCRIPTION
    Runs markdownlint, lychee link checking, and sample build/test locally.
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

# --- Stage 1: Markdown Linting ---
Write-Host "`n=== Stage 1/3: Markdown Linting ===" -ForegroundColor Cyan
npx markdownlint-cli2 @docsGlob
if ($LASTEXITCODE -ne 0) { Write-Error "FAILED: Markdown linting"; exit 1 }
Write-Host "PASSED: Markdown linting" -ForegroundColor Green

# --- Stage 2: Link Checking ---
Write-Host "`n=== Stage 2/3: Link Checking ===" -ForegroundColor Cyan
lychee --config lychee.toml @docsGlob
if ($LASTEXITCODE -ne 0) { Write-Error "FAILED: Link checking"; exit 1 }
Write-Host "PASSED: Link checking" -ForegroundColor Green

# --- Stage 3: Sample Build & Test ---
Write-Host "`n=== Stage 3/3: Sample Build & Test ===" -ForegroundColor Cyan
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
