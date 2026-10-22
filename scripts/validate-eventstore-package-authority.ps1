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
    $arguments = @(
        '-NoProfile', '-File', $authority,
        '-RepositoryRoot', $resolvedRoot, '-CatalogPath', $resolvedCatalog
    )
    if ($ExcludedPath.Count -gt 0) {
        $arguments += @('-ExcludedPath', [string]::Join(';', $ExcludedPath))
    }
    $pwsh = Join-Path $PSHOME $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })
    & $pwsh @arguments
    $exitCode = $LASTEXITCODE
}
catch {
    [Console]::Error.WriteLine('EventStore exact package authority validation failed.')
    $exitCode = 1
}

exit $exitCode
