# DW19b ST3 — Tier 3 Keycloak container-reuse runtime validation.
# Runs the Keycloak-enabled Tier 3 collections twice under KEYCLOAK_TEST_REUSE=true and
# captures the Keycloak container identity (Id, CreatedAt, DCP lifecycle-key label) after
# each run, plus the wall-clock delta. Container end-state is the proof (R2-A6), not exit codes.
$ErrorActionPreference = 'Continue'

$art = Join-Path $PSScriptRoot '.'
$proj = 'D:\Hexalith.EventStore\tests\Hexalith.EventStore.IntegrationTests\Hexalith.EventStore.IntegrationTests.csproj'
$filter = 'FullyQualifiedName~KeycloakE2E|FullyQualifiedName~DaprAccessControlE2E|FullyQualifiedName~KeycloakAuthentication'

$env:KEYCLOAK_TEST_REUSE = 'true'
# Do NOT set CI / SKIP_PREREQUISITE_CHECK — let the AppHost prerequisite probes run as in real dev.

function Capture-Keycloak([string]$tag) {
    $f = Join-Path $art "docker-$tag.txt"
    "=== $tag captured $(Get-Date -Format o) ===" | Out-File -FilePath $f -Encoding utf8
    'docker ps -a (keycloak): ID|CreatedAt|Status|Names' | Out-File -FilePath $f -Append -Encoding utf8
    docker ps -a --filter 'name=keycloak' --format '{{.ID}}|{{.CreatedAt}}|{{.Status}}|{{.Names}}' 2>&1 | Out-File -FilePath $f -Append -Encoding utf8
    $id = (docker ps -aqf 'name=keycloak' | Select-Object -First 1)
    if ($id) {
        '' | Out-File -FilePath $f -Append -Encoding utf8
        "docker inspect Id/Created for ${id}:" | Out-File -FilePath $f -Append -Encoding utf8
        docker inspect --format 'Id={{.Id}}{{"`n"}}Created={{.Created}}{{"`n"}}State={{.State.Status}}{{"`n"}}StartedAt={{.State.StartedAt}}' $id 2>&1 | Out-File -FilePath $f -Append -Encoding utf8
        '' | Out-File -FilePath $f -Append -Encoding utf8
        'Config.Labels (contains DCP lifecycle-key / reuse hash):' | Out-File -FilePath $f -Append -Encoding utf8
        docker inspect --format '{{json .Config.Labels}}' $id 2>&1 | Out-File -FilePath $f -Append -Encoding utf8
        '' | Out-File -FilePath $f -Append -Encoding utf8
        'HostConfig.PortBindings (proxyless fixed host ports):' | Out-File -FilePath $f -Append -Encoding utf8
        docker inspect --format '{{json .HostConfig.PortBindings}}' $id 2>&1 | Out-File -FilePath $f -Append -Encoding utf8
    } else {
        '(no keycloak container present — persistent container did NOT survive the test process)' | Out-File -FilePath $f -Append -Encoding utf8
    }
    Get-Content $f
}

# Ensure run 1 is a TRUE cold start.
$stale = docker ps -aqf 'name=keycloak'
if ($stale) { docker rm -f $stale 2>&1 | Out-Null }
Capture-Keycloak 'before-run1'

# ---- RUN 1 (cold) ----
$sw1 = [System.Diagnostics.Stopwatch]::StartNew()
dotnet test $proj -c Release --no-build --filter $filter --logger 'console;verbosity=normal' `
    > (Join-Path $art 'run1.log') 2>&1
$run1Exit = $LASTEXITCODE
$sw1.Stop()
Capture-Keycloak 'after-run1'

# ---- RUN 2 (warm, expect reuse) ----
$sw2 = [System.Diagnostics.Stopwatch]::StartNew()
dotnet test $proj -c Release --no-build --filter $filter --logger 'console;verbosity=normal' `
    > (Join-Path $art 'run2.log') 2>&1
$run2Exit = $LASTEXITCODE
$sw2.Stop()
Capture-Keycloak 'after-run2'

# ---- Summary ----
$summary = Join-Path $art 'SUMMARY.txt'
@"
DW19b ST3 reuse validation — $(Get-Date -Format o)
KEYCLOAK_TEST_REUSE = true
filter = $filter

Run 1 (cold): exit=$run1Exit  wall=$($sw1.Elapsed.ToString())
Run 2 (warm): exit=$run2Exit  wall=$($sw2.Elapsed.ToString())

Pass/fail lines:
$(Select-String -Path (Join-Path $art 'run1.log') -Pattern 'Passed!|Failed!|Passed:|Failed:|error|Total tests' | Select-Object -Last 12 | ForEach-Object { 'R1> ' + $_.Line })
$(Select-String -Path (Join-Path $art 'run2.log') -Pattern 'Passed!|Failed!|Passed:|Failed:|error|Total tests' | Select-Object -Last 12 | ForEach-Object { 'R2> ' + $_.Line })

Compare docker-after-run1.txt vs docker-after-run2.txt for identical Id/Created/lifecycle-key.
"@ | Out-File -FilePath $summary -Encoding utf8
Get-Content $summary
Write-Host 'DW19B_VALIDATION_DONE'
