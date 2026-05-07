# Seeds 4 fake dead-letter entries into Redis for manual UI testing of /health/dead-letters.
# The DAPR Redis state-store stores values as a hash with `data` (JSON) + `version` (ETag).
# This script writes the index keys read by DaprDeadLetterQueryService.
# Run from the repo root after Aspire is running and Redis is reachable.

$ErrorActionPreference = 'Stop'

function Test-RedisContainer {
    $containers = docker ps --format '{{.Names}}' 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Docker not reachable. Make sure Docker Desktop is running."
    }
    if ($containers -notcontains 'dapr_redis') {
        throw "Container 'dapr_redis' not found. Run 'dapr init' or start Aspire first."
    }
}

Test-RedisContainer

# Build 4 varied DLQ entries — covers all 4 failure-category presets + a high-retry one.
# Field names use camelCase to match the JsonNamingPolicy.CamelCase convention used elsewhere
# (cf. admin:stream-activity:all). System.Text.Json defaults to case-insensitive deserialization,
# but we mirror the same convention to stay consistent.
$now = (Get-Date).ToUniversalTime()
$entries = @(
    [ordered]@{
        messageId           = '01HXT0DLQ0000000000000001'
        tenantId            = 'tenant-a'
        domain              = 'counter'
        aggregateId         = 'counter-1'
        correlationId       = '01HXT0DLQ0000000000000C01'
        failureReason       = 'Deserialization failed: unknown command type "BogusCommand"'
        failedAtUtc         = $now.AddMinutes(-30).ToString('o')
        retryCount          = 0
        originalCommandType = 'BogusCommand'
    },
    [ordered]@{
        messageId           = '01HXT0DLQ0000000000000002'
        tenantId            = 'tenant-a'
        domain              = 'counter'
        aggregateId         = 'counter-1'
        correlationId       = '01HXT0DLQ0000000000000C02'
        failureReason       = 'Timeout while waiting for domain service "sample" (10s exceeded)'
        failedAtUtc         = $now.AddMinutes(-20).ToString('o')
        retryCount          = 1
        originalCommandType = 'IncrementCounter'
    },
    [ordered]@{
        messageId           = '01HXT0DLQ0000000000000003'
        tenantId            = 'tenant-a'
        domain              = 'counter'
        aggregateId         = 'counter-1'
        correlationId       = '01HXT0DLQ0000000000000C03'
        failureReason       = 'Authorization rejected: caller lacks tenant claim for tenant-a'
        failedAtUtc         = $now.AddMinutes(-10).ToString('o')
        retryCount          = 2
        originalCommandType = 'ResetCounter'
    },
    [ordered]@{
        messageId           = '01HXT0DLQ0000000000000004'
        tenantId            = 'tenant-a'
        domain              = 'counter'
        aggregateId         = 'counter-1'
        correlationId       = '01HXT0DLQ0000000000000C04'
        failureReason       = 'Aggregate handler threw NullReferenceException at sequence 19'
        failedAtUtc         = $now.AddMinutes(-2).ToString('o')
        retryCount          = 4
        originalCommandType = 'DecrementCounter'
    }
)

# Compact JSON, no BOM, no newlines (matches what DaprClient writes).
$json = $entries | ConvertTo-Json -Depth 5 -Compress

# Two index keys: ":all" (no tenant filter) and ":tenant-a" (when user types tenant-a in the filter).
$keys = @('admin:dead-letters:all', 'admin:dead-letters:tenant-a')

foreach ($key in $keys) {
    # Write the JSON to a temp file as UTF-8 WITHOUT BOM, then `redis-cli -x < file` so the
    # value reaches Redis byte-perfect. Piping a PowerShell string to `docker exec -i` adds a
    # UTF-8 BOM that some JSON deserializers reject.
    $tmp = New-TemporaryFile
    try {
        [System.IO.File]::WriteAllBytes($tmp.FullName, [System.Text.UTF8Encoding]::new($false).GetBytes($json))
        Get-Content -LiteralPath $tmp.FullName -Raw -Encoding Byte | Out-Null  # touch to ensure flush
        cmd /c "docker exec -i dapr_redis redis-cli -x HSET `"$key`" data < `"$($tmp.FullName)`"" | Out-Null
        docker exec dapr_redis redis-cli HSET $key version 1 | Out-Null
    } finally {
        Remove-Item -LiteralPath $tmp.FullName -ErrorAction SilentlyContinue
    }
    Write-Host "Seeded $key ($($entries.Count) entries)" -ForegroundColor Green
}

Write-Host ""
Write-Host "Done. Refresh https://localhost:8093/health/dead-letters to see the entries." -ForegroundColor Cyan
Write-Host ""
Write-Host "To clear the seeded entries afterwards:" -ForegroundColor Yellow
Write-Host '  docker exec dapr_redis redis-cli DEL admin:dead-letters:all admin:dead-letters:tenant-a'
