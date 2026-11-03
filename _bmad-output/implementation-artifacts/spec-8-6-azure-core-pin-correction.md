---
title: 'Story 8.6 Azure.Core pin correction candidate'
type: 'security-architecture-amendment-candidate'
created: '2026-11-03'
status: 'approved-authorized'
implementation_authorized: true
approval_date: '2026-11-03'
approval_source: 'Interactive project owner: approve'
assurance: 'single-maintainer-attested'
candidate_normative_sha256: '360da00f91d3134054e4ec3f18c0b96921a36911a89d5935e7d59409634ca07e'
story_key: '8-6-azure-key-vault-production-adapter-conformance'
---

# Story 8.6 Azure.Core Pin Correction Candidate

The interactive project owner approved this exact correction on 2026-11-03 and directed continuation of Story 8.6. The original exception and this supplementary correction jointly govern local implementation. No catalog or runtime edit had been applied at approval.

<!-- HX-PP-AZURE-CORE-CORRECTION-BEGIN -->

## Authority and exact correction

This candidate supplements only section 4 of the approved [Azure SDK exception](spec-8-6-crypto-boundary-amendment.md), normative SHA-256 `fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394`. It has no implementation authority until the project owner approves this exact normative digest. Preserve the original approval bytes and owning story frozen block, SHA-256 `7fe1ee3af25c7199c16a510fc9e5cac210581fe94ef5030a364c04525c86cbff`.

If approved, replace only the proposed Azure.Core 1.54.0 dependency with **Azure.Core 1.64.0**. The exact two catalog additions in `references/Hexalith.Builds/Props/Directory.Packages.props` become:

```xml
<PackageVersion Include="Azure.Core" Version="1.64.0" />
<PackageVersion Include="Azure.Security.KeyVault.Keys" Version="4.10.2" />
```

Azure.Identity remains 1.21.0. Application and test references stay versionless. Do not change the root import wrapper, central transitive pinning, any other catalog row, or the 14-package release inventory. The catalog before these additions remains SHA-256 `bade7f129df947377a7527c31f2fdc335e310700924c6acf2c88f79ea53fd1a1`, at Builds gitlink `6585f5860e87c146c26aa066d3a52b94eb56f271`.

The replacement Azure.Core nupkg SHA-256 is `9be017250bb72642f895364aa86b28e2d752097b9c5d5d8cc1112a2e27c513b7`; its nuspec declares source commit `254a1b693af968afee40958e77d0762b44a1b13c`. The inspected net10.0 assembly version is 1.64.0. Keys and Identity retain their approved package/source identities. Only Azure.Core's dependency version, package identity and source identity supersede the old section 4 values.

## Preserved scope and implementation conditions

All original operation, path, API-version, credential, custody, buffer, diagnostic, retry, reconciliation, predecessor, ownership and acceptance restrictions continue to apply. Engine normative/effective authority, wire/state schemas, Contracts, Server enablement and packability do not change. No approval of Azure mutations, live qualification, production, release, later stories or G5 follows from this correction.

The SDK defaults require deliberate handling: the inspected bearer-token source can process a CAE challenge and resend independently of the ordinary retry policy. The existing requirement that crypto 401 is denial therefore still requires suppressing challenge-driven authentication/replay and testing the emitted request count. Retry zero alone supplies no proof. This observation does not relax the original fixed-scope, bounded-budget or no-leak requirements or authorize additional provider capability.

Before dependency introduction, record the accepted boundary exception with both the original and correction normative digests. Include this approved correction in the owning story's context and approval notes without rewriting the original approval subject. Run affected API, dependency, regression, diagnostic and conformance checks from the original design. A restore pass supplies dependency evidence only; any new incompatible graph or implementation gap returns for disposition.

## Decision requested

Approve this exact correction for additive local Story 8.6 implementation, including the revised Azure.Core catalog addition in the Builds submodule and retention of the approved predecessor disposition. Approval must identify the human accurately and remains single-maintainer-attested unless separate reviewers actually participate. Until then, the adapter/catalog changes and acceptance claims remain pending.

<!-- HX-PP-AZURE-CORE-CORRECTION-END -->

## Evidence and limitations

Owning source HEAD: `b46fa93e2926c9be39096ae76e920d38d338db8d`; .NET SDK 10.0.403. The [owning story](spec-8-6-azure-key-vault-production-adapter-conformance.md) records the exact restore and authority commands and results. Disposable probe inputs/logs remain under `/tmp/story86-catalog-compat-yy973wev/`.

| Probe | Result |
|---|---|
| Unchanged central catalog with Aspire.Hosting.Azure.AppContainers 13.6.4 | Restore succeeds; Azure.Core resolves to 1.64.0 |
| Same project plus the two originally approved catalog additions | Restore fails NU1109; AppContainers requires Azure.Core >=1.62.0, conflicting with 1.54.0 |
| Corrected catalog with versionless Core, Identity, Keys and AppContainers references | Restore succeeds with Core 1.64.0, Identity 1.21.0 and Keys 4.10.2 |
| Control versus corrected probe resolved libraries | No library/version removed or replaced; only Keys 4.10.2 added |
| Approval subjects | All 11 identity checks pass; both independent golden tools pass V001–V003 |

This is an isolated compatibility graph, not a full solution restore, adapter test result, CI run or Azure conformance receipt.

API inspection used dotnet-inspect 0.26.0 against net10.0. `diff --package Azure.Core@1.54.0..1.64.0 --tfm net10.0 --breaking --json` reports two removed ConfigurationExtensions.WithAzureCredential overloads. `rg -n 'WithAzureCredential' src tests samples -g '*.cs'` found no use. The four-argument HttpPipelineBuilder.Build API has an exact match; member inspection confirms the public bearer-token constructors, HttpMessage.BufferResponse and abstract RequestContent disposal/length/write members. These checks cover the design's selected extension seams, not all SDK behavior.

The source review fetched [BearerTokenAuthenticationPolicy.cs](https://github.com/Azure/azure-sdk-for-net/blob/254a1b693af968afee40958e77d0762b44a1b13c/sdk/core/Azure.Core/src/Pipeline/BearerTokenAuthenticationPolicy.cs) and [HttpPipelineBuilder.cs](https://github.com/Azure/azure-sdk-for-net/blob/254a1b693af968afee40958e77d0762b44a1b13c/sdk/core/Azure.Core/src/Pipeline/HttpPipelineBuilder.cs) from the package-declared commit. Retained source SHA-256 values are respectively `136e4fbaf8d55d01dc5e53c6852b5a08822dff5a5034d47648c0fd4a6d76df18` and `8ffafbcdb30e1af57b76e3488c9ae51b1b38c131ac1f6af68a298f50ac1a2d0a`. Review confirmed the independent CAE challenge resend and builder hooks for explicit retry/diagnostic/custom-transport settings. Transport isolation, actual cancellation/clearing, hidden authentication work and no-leak behavior remain implementation/test obligations.

The normative digest covers the exact UTF-8 LF bytes after the begin marker line and before the end marker.

## Approval record

Approved on 2026-11-03 by Administrator, the interactive project owner, through the reply “approve” to the explicit request to approve this prepared correction and continue implementation. The approved normative SHA-256 is `360da00f91d3134054e4ec3f18c0b96921a36911a89d5935e7d59409634ca07e`. This is a single-maintainer-attested owner decision. It covers additive local Story 8.6 implementation, Core 1.64.0 and Keys 4.10.2 catalog additions in Builds, and retention of the original predecessor disposition. Original approval bytes and the owning frozen block remain unchanged. Azure authority, live qualification, production, release and successor gates remain separate.
