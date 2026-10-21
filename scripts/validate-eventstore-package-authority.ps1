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

# The owner approved precisely these five EventStore overrides on 2026-10-16.
# This adapter leaves the shared authority files and every other check intact.
# SignalR.Common uses Update because the current shared catalog already declares it.
function Get-EventStoreApprovedPackagePins {
    $pins = [System.Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
    $pins.Add('Microsoft.AspNetCore.SignalR.Common', 'Update')
    $pins.Add('System.Security.Cryptography.Pkcs', 'Include')
    $pins.Add('System.Security.Cryptography.Xml', 'Include')
    $pins.Add('Microsoft.Extensions.DependencyInjection.Abstractions', 'Update')
    $pins.Add('Microsoft.Extensions.Options', 'Update')
    return ,$pins
}

function Test-EventStoreApprovedPackagePin {
    param([System.Xml.XmlElement] $Node)

    if ($Node.LocalName -cne 'PackageVersion' -or
        $Node.NamespaceURI.Length -ne 0 -or
        $Node.Attributes.Count -ne 2 -or
        $Node.ChildNodes.Count -ne 0 -or
        -not $Node.HasAttribute('Version') -or
        $Node.GetAttribute('Version') -cne '10.0.13' -or
        $Node.HasAttribute('Include') -eq $Node.HasAttribute('Update')) {
        return $false
    }

    $group = $Node.ParentNode
    $project = $group.ParentNode
    if ($group.LocalName -cne 'ItemGroup' -or $group.NamespaceURI.Length -ne 0 -or
        $group.Attributes.Count -ne 0 -or
        $project.LocalName -cne 'Project' -or $project.NamespaceURI.Length -ne 0 -or
        $project.Attributes.Count -ne 0 -or
        $project.ParentNode.NodeType -ne [System.Xml.XmlNodeType]::Document) {
        return $false
    }

    $action = if ($Node.HasAttribute('Include')) { 'Include' } else { 'Update' }
    $identity = $Node.GetAttribute($action)
    $pins = Get-EventStoreApprovedPackagePins
    return $pins.ContainsKey($identity) -and $pins[$identity] -ceq $action
}

function Assert-EventStoreApprovedRootPackagePins {
    param([string] $Path)

    [xml] $wrapper = Get-Content -LiteralPath $Path -Raw
    $nodes = @($wrapper.SelectNodes("//*[local-name()='PackageVersion']"))
    if ($nodes.Count -ne 5) {
        throw 'eventstore_package_exception_invalid'
    }

    $seen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($node in $nodes) {
        if (-not (Test-EventStoreApprovedPackagePin -Node $node)) {
            throw 'eventstore_package_exception_invalid'
        }

        $action = if ($node.HasAttribute('Include')) { 'Include' } else { 'Update' }
        if (-not $seen.Add($node.GetAttribute($action))) {
            throw 'eventstore_package_exception_invalid'
        }
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
    Assert-EventStoreApprovedRootPackagePins -Path (Join-Path $resolvedRoot 'Directory.Packages.props')
    if ($ValidateWrapperOnly) {
        [Console]::Out.WriteLine('EventStore exact five-pin exception validation passed.')
        exit 0
    }

    $toolsDirectory = [IO.Path]::GetFullPath((Join-Path (Split-Path $resolvedCatalog -Parent) '../Tools'))
    $authority = Join-Path $toolsDirectory 'validate-consumer-package-authority.ps1'
    $companion = Join-Path $toolsDirectory 'validate-central-package-versions.ps1'
    # Canonical UTF-8/LF hashes accept ordinary Git CRLF checkouts while still
    # rejecting every source-content change. Only verified content is derived.
    $source = Read-EventStoreCanonicalAuthority -Path $authority -ExpectedSha256 (
        'b20e45e3800e7d5e08770993192b646887097b8187e70f3f629adc741a0441fe')
    $companionSource = Read-EventStoreCanonicalAuthority -Path $companion -ExpectedSha256 (
        '9db2a37db1125ad075efb50477543c73affc23e6030ac1997b2c996adab33438')
    $rawAnchor = @'
    foreach ($packageVersion in @($xml.SelectNodes("//*[local-name()='PackageVersion']"))) {
'@
    $effectiveAnchor = @'
    foreach ($authoritativePackage in $authoritativePackages.GetEnumerator()) {
'@
    foreach ($anchor in @($rawAnchor, $effectiveAnchor)) {
        if ([regex]::Matches($source, [regex]::Escape($anchor)).Count -ne 1) {
            throw 'eventstore_shared_authority_changed'
        }
    }

    # Reuse these exact policy functions in the derived process. No other
    # shared code is replaced, and all original effective checks still run.
    $tokens = $null
    $parseErrors = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseFile(
        $PSCommandPath, [ref] $tokens, [ref] $parseErrors)
    if ($parseErrors.Count -ne 0) {
        throw 'eventstore_package_adapter_invalid'
    }
    $names = @(
        'Get-EventStoreApprovedPackagePins',
        'Test-EventStoreApprovedPackagePin',
        'Assert-EventStoreApprovedRootPackagePins'
    )
    $functions = @($ast.FindAll({
        param($node)
        $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
            $node.Name -cin $names
    }, $false))
    if ($functions.Count -ne 3) {
        throw 'eventstore_package_adapter_invalid'
    }
    $lf = [string][char]10
    $policy = [string]::Join($lf, @($functions | ForEach-Object { $_.Extent.Text }))
    $rawPolicy = @'
        if ([StringComparer]::Ordinal.Equals(
                [IO.Path]::GetFullPath($resolvedXmlPath),
                [IO.Path]::GetFullPath($wrapperPath)) -and
            (Test-EventStoreApprovedPackagePin -Node $packageVersion)) {
            continue
        }
'@
    $effectivePolicy = @'
    foreach ($approvedPin in (Get-EventStoreApprovedPackagePins).GetEnumerator()) {
        $authoritativePackages[$approvedPin.Key] = '10.0.13'
    }
'@
    $source = $source.Replace($rawAnchor, $policy + $lf + $rawAnchor + $lf + $rawPolicy)
    $source = $source.Replace($effectiveAnchor, $effectivePolicy + $lf + $effectiveAnchor)

    $temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) (
        'eventstore-package-authority-' + [Guid]::NewGuid().ToString('N'))
    [void] [IO.Directory]::CreateDirectory($temporaryDirectory)
    $derivedAuthority = Join-Path $temporaryDirectory 'validate-consumer-package-authority.ps1'
    [IO.File]::WriteAllText($derivedAuthority, $source, [Text.UTF8Encoding]::new($false))
    # The canonical companion stays beside the derived script, so its original
    # PSScriptRoot lookup resolves exact hash-verified content. CatalogPath below
    # remains the original shared authority, never a rewritten catalog.
    [IO.File]::WriteAllText(
        (Join-Path $temporaryDirectory 'validate-central-package-versions.ps1'),
        $companionSource, [Text.UTF8Encoding]::new($false))
    # Exact retained inventories can exceed the OS per-argument length limit.
    # Transport structured arguments outside the checkout, preserving array membership.
    $argumentFile = Join-Path $temporaryDirectory 'arguments.json'
    $parameters = @{
        RepositoryRoot = $resolvedRoot
        CatalogPath = $resolvedCatalog
        ExcludedPath = @($ExcludedPath)
    }
    [IO.File]::WriteAllText($argumentFile, ($parameters | ConvertTo-Json -Depth 4), [Text.UTF8Encoding]::new($false))
    $invocation = '$parameters = Get-Content -LiteralPath ''' + $argumentFile.Replace("'", "''") +
        ''' -Raw | ConvertFrom-Json -AsHashtable' + $lf +
        '& ''' + $derivedAuthority.Replace("'", "''") + ''' @parameters' + $lf +
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
