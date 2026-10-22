[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $RepositoryRoot,

    [Parameter(Mandatory = $true)]
    [string] $CatalogPath,

    [string[]] $ExcludedPath = @(),

    [switch] $ValidateWrapperOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Security servicing pins now live in Builds. Reject every consumer-local version declaration
# and invoke the original shared authority without an exception overlay.
function Assert-EventStoreVersionFreeWrapper {
    param([string] $Path)

    [xml] $wrapper = Get-Content -LiteralPath $Path -Raw
    if (@($wrapper.SelectNodes("//*[local-name()='PackageVersion']")).Count -ne 0) {
        throw 'eventstore_local_package_version_forbidden'
    }
}

function Read-EventStoreCanonicalAuthority {
    param([string] $Path, [string] $ExpectedSha256)

    $utf8 = [Text.UTF8Encoding]::new($false, $true)
    $content = $utf8.GetString([IO.File]::ReadAllBytes($Path)).Replace(
        ([string][char]13 + [char]10), [string][char]10)
    $digest = [Convert]::ToHexString(
        [Security.Cryptography.SHA256]::HashData($utf8.GetBytes($content))).ToLowerInvariant()
    if ($digest -cne $ExpectedSha256) {
        throw 'eventstore_shared_authority_changed'
    }
    return $content
}

$temporaryDirectory = $null
$exitCode = 1
try {
    $resolvedRoot = (Resolve-Path -LiteralPath $RepositoryRoot).ProviderPath
    $resolvedCatalog = (Resolve-Path -LiteralPath $CatalogPath).ProviderPath
    Assert-EventStoreVersionFreeWrapper -Path (Join-Path $resolvedRoot 'Directory.Packages.props')
    if ($ValidateWrapperOnly) {
        [Console]::Out.WriteLine('EventStore version-free package wrapper validation passed.')
        exit 0
    }

    $toolsDirectory = [IO.Path]::GetFullPath((Join-Path (Split-Path $resolvedCatalog -Parent) '../Tools'))
    $authority = Join-Path $toolsDirectory 'validate-consumer-package-authority.ps1'
    $companion = Join-Path $toolsDirectory 'validate-central-package-versions.ps1'
    # Canonical UTF-8/LF hashes accept ordinary Git CRLF checkouts while still
    # rejecting every source-content change before delegation.
    $null = Read-EventStoreCanonicalAuthority -Path $authority -ExpectedSha256 (
        'b20e45e3800e7d5e08770993192b646887097b8187e70f3f629adc741a0441fe')
    $null = Read-EventStoreCanonicalAuthority -Path $companion -ExpectedSha256 (
        '9db2a37db1125ad075efb50477543c73affc23e6030ac1997b2c996adab33438')

    # Exact retained inventories can exceed the OS per-argument length limit.
    # Transport structured arguments outside the checkout, preserving array membership.
    # The original shared authority stays in place; no exception overlay is derived.
    $temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) (
        'eventstore-package-authority-' + [Guid]::NewGuid().ToString('N'))
    [void] [IO.Directory]::CreateDirectory($temporaryDirectory)
    $argumentFile = Join-Path $temporaryDirectory 'arguments.json'
    $parameters = @{
        RepositoryRoot = $resolvedRoot
        CatalogPath = $resolvedCatalog
        ExcludedPath = @($ExcludedPath)
    }
    [IO.File]::WriteAllText($argumentFile, ($parameters | ConvertTo-Json -Depth 4), [Text.UTF8Encoding]::new($false))
    $lf = [string][char]10
    $invocation = '$parameters = Get-Content -LiteralPath ''' + $argumentFile.Replace("'", "''") +
        ''' -Raw | ConvertFrom-Json -AsHashtable' + $lf +
        '& ''' + $authority.Replace("'", "''") + ''' @parameters' + $lf +
        'exit $LASTEXITCODE'
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($invocation))
    $pwsh = Join-Path $PSHOME $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })
    & $pwsh -NoProfile -EncodedCommand $encoded
    $exitCode = $LASTEXITCODE
}
catch {
    [Console]::Error.WriteLine('EventStore exact package authority validation failed.')
    $exitCode = 1
}
finally {
    if ($null -ne $temporaryDirectory -and (Test-Path -LiteralPath $temporaryDirectory)) {
        Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
    }
}

exit $exitCode
