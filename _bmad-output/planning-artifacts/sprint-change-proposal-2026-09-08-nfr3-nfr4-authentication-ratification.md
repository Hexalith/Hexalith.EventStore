# Sprint Change Proposal - NFR3/NFR4 Authentication Ratification

Date: 2026-09-08
Project: eventstore
Planning mode: Incremental
Scope classification: Moderate
Recommended path: Direct Adjustment
Approval: Approved by Administrator on 2026-09-08

## 1. Issue Summary

Commit `8312bced` (2026-09-06, "feat: update acceptance criteria and architectural constraints for authentication and authorization") rewrote NFR3 and NFR4 in `_bmad-output/planning-artifacts/prd.md` with no authorizing artifact. The 2026-09-08 PRD validation run raised this as a critical finding.

Verified on 2026-09-08:

- No `sprint-change-proposal-2026-09-06*.md` exists.
- `sprint-change-proposal-2026-09-07.md` cannot cover the edit: it postdates it, targets the Epic 3 retrospective rejection, and states "No PRD, architecture, epic, or UX content changes."
- The PRD frontmatter still read `status: final` / `updated: 2026-08-16`, and the run memlog had no entry between the 2026-08-16 finalize and this run.
- `8312bced` is a mixed commit. Two of its seven files carry the NFR change (`prd.md`, `epic-5-context.md`) and a third carries the matching Story 5.3 rewrite in `epics.md`; the remaining four are unrelated Story 4.7 review-closure and DW-493/DW-494 content bundled under the same subject.
- The `epics.md` Requirements Inventory NFR3 and NFR4 lines were **not** updated by that commit and still carry the pre-2026-09-06 wording, so `prd.md`, the inventory, and the Story 5.3 acceptance criteria have carried three different renderings of two NFRs.
- The spec `spec-5-3-production-authentication-guards-and-secret-stripping.md` was first committed 2026-09-07, the day after the PRD edit, and cites the new PRD text as its authority. That circularity is why ratification, not silent acceptance, is required.

This is a provenance failure plus one substantive defect, not a scope change. No code, dependency, release, deployment, Git, or submodule mutation is authorized by this proposal.

## 2. Impact Analysis

### Substantive defect in the 2026-09-06 NFR3 text

The rewritten NFR3 reads as though `AllowInsecureSymmetricKey` admits symmetric-key mode in every non-Development environment, Production included. The shipped implementation forbids it in Production unconditionally:

`src/Hexalith.EventStore.ServiceDefaults/Authentication/JwtBearerAuthenticationContract.cs:118-128`

```csharp
if (environment.IsProduction())
{
    return ValidateOptionsResult.Fail(
        $"{sectionName}:SigningKey is forbidden in Production, including when AllowInsecureSymmetricKey is true.");
}

if (!environment.IsDevelopment() && !options.AllowInsecureSymmetricKey)
{
    return ValidateOptionsResult.Fail(
        $"{sectionName}:SigningKey is Development-only unless AllowInsecureSymmetricKey is explicitly enabled in a non-Production environment.");
}
```

The Production branch returns before `AllowInsecureSymmetricKey` is first read, so the option only ever admits environments that are neither Development nor Production. The Story 5.3 spec agrees with the code. As the authoritative NFR, the 2026-09-06 text would justify a later change re-enabling HS256 in Production; it is ratified only as corrected.

Two further gaps in the same text are closed by this proposal:

- It named `AllowInsecureSymmetricKey` but not `AllowedAlgorithms`, the option it also constrains. Authority mode independently requires a nonempty allowlist drawn only from RS256/384/512, PS256/384/512, ES256/384/512, with no implicit default.
- It said "outside Development" generically without naming the host set. Three hosts bind the contract: the EventStore gateway (`Hexalith.EventStore`), `Hexalith.EventStore.Admin.Server.Host`, and `samples/Hexalith.EventStore.Sample.Api`. The `Hexalith.EventStore.Admin.UI` `AddJwtBearer()` call is separate client-side setup and does not bind the contract.

### Artifact impact

- **PRD:** NFR3 is ratified as corrected per §3. NFR4 is ratified as written. Frontmatter `updated` moves to 2026-09-08 and this proposal joins `source_artifacts`.
- **`epics.md`:** the Requirements Inventory NFR3 and NFR4 lines are synchronized to the ratified PRD text. The Story 5.3 acceptance criteria written by `8312bced` already match and are not reopened.
- **Architecture, UX:** no change.
- **Scope:** no FR added, removed, or resized. No epic is reopened or resequenced.

### Story impact

Story 5.3 remains the sole owner of NFR3/NFR4. This proposal does not change its status, which is separately contradictory across three sources and is recorded as blocking item OR2 in PRD §12 for the Epic 5 owner to reconcile. Ratifying the requirement text does not assert the story is done.

## 3. Approved Changes

Ratified NFR3, correcting and replacing the 2026-09-06 text:

> Authentication must fail closed on signing-key posture in every host that binds the platform JWT contract: the EventStore gateway, the Admin Server Host, and the Sample API. Production must always reject symmetric-key mode, including when `AllowInsecureSymmetricKey` is enabled; the break-glass option admits symmetric mode only in environments that are neither Development nor Production. Authority/OIDC discovery must require HTTPS metadata outside Development. `AllowedAlgorithms` must be a nonempty, explicitly configured allowlist with no implicit default: asymmetric mode accepts only the approved RS256/RS384/RS512, PS256/PS384/PS512, and ES256/ES384/ES512 set, and Development or break-glass symmetric mode accepts only HS256. Issuer, audience, signature, and lifetime validation remain mandatory in every mode, with clock skew fixed at 60 seconds. Role and tenant validation also remain mandatory in every mode and are owned by NFR1 and NFR2.

Ratified NFR4, which keeps the 2026-09-06 substance and closes its injection-channel list. The 2026-09-06 text ended with the open category "ephemeral developer tooling", which a scanner cannot enumerate; the ratified text replaces it with a closed list and a change gate:

> No committed configuration, including clearly named Development configuration, may contain a forgeable administrator signing key, username, password, credential, bearer token, decoded JWT payload, or other operational secret. Development and test credentials are injected only through this closed list of channels - .NET user-secrets, environment variables, runtime-generated test fixtures, and the Aspire AppHost parameter/secret mechanism - and cannot be loaded as a non-Development fallback. Adding a channel to this list requires a proposal.

Both lines replace the pre-2026-09-06 wording in the `epics.md` Requirements Inventory.

## 4. Follow-On Work

- **OR8 (PRD §12):** add a guard diffing PRD §7 against the `epics.md` Requirements Inventory, so the two cannot silently diverge again.
- **OR2 (PRD §12):** reconcile Story 5.3's `done` / `in-progress` / `backlog` contradiction before any story closes against NFR3 or NFR4.

## 5. Authority Boundary

This proposal authorizes planning-artifact edits to `prd.md` and the `epics.md` Requirements Inventory only. It authorizes no source change, no test change, no tracker mutation, no release, no deployment, and no Git or submodule operation.
