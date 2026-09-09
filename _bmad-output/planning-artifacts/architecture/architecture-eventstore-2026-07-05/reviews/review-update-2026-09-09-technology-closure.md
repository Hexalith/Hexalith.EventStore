# Technology Currentness / Reality-Check Closure


> **Anchor note (added 2026-09-09, code review Story 4.15 Group F).** The line numbers originally cited in
> this file were computed against a pre-final draft of the spine and are offset by a non-uniform amount (AD-8 by
> 21 lines, AD-16 by 25, AD-26 by 32), so they resolve to the wrong decision. Citations that could be mapped
> unambiguously have been re-anchored to **AD identifiers**, which are stable. Any residual bare `:NNN`
> reference in this file is unreliable — resolve it by the AD or section named in the surrounding prose, not by
> the number. `ARCHITECTURE-SPINE.md` is a symlink to `_bmad-output/planning-artifacts/architecture.md`.

**Artifact:** `ARCHITECTURE-SPINE.md`  
**Reviewed:** 2026-09-09  
**Verdict:** **PASS** — no critical or high-severity technology/current-reality finding remains.

## Remediation Verification

- **Tenants topology — closed.** Structural Seed lines 360-380 now include `tenants` and `tenants-api`, show `tenants` using the Redis-backed state/pub-sub node, and show `tenants-api` as service-invocation-only to EventStore. This matches `Program.cs:151-188` and the shared-component wiring in `HexalithEventStoreDomainModuleExtensions.cs:60-79`.
- **CodeCoverage — closed.** Stack line 327 now identifies repository pin `18.10.0` as the current public NuGet version. The official [NuGet version index](https://api.nuget.org/v3-flatcontainer/microsoft.codecoverage/index.json) currently ends at `18.10.0`.

## Later-Edit Reality Check

- The added payload-protection package boundary explicitly says both projects are future and nonexistent; filesystem inspection confirms both are absent.
- The canonical production profile and routing catalog are identified as required future artifacts and repeated under Deferred; `deploy/dapr/production-profile.yaml` and `deploy/dapr/eventstore-routing-catalog.json` are absent, so the spine does not present them as delivered.
- Repository/runtime facts remain accurate: SDK `10.0.400`; Aspire `13.5.3`; Dapr .NET SDK `1.18.5`; CI Dapr runtime `1.18.2`; deployment examples `1.18.0`; Redis local components; PostgreSQL v1 and durable-broker production templates. Official sources still support the stated target facts: [.NET 10.0.12 / SDK 10.0.401](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json), [Dapr runtime 1.18.3](https://github.com/dapr/dapr/releases/tag/v1.18.3), [PostgreSQL v1/v2 compatibility](https://docs.dapr.io/reference/components-reference/supported-state-stores/setup-postgresql-v2/), [OpenBao through the Vault component](https://docs.dapr.io/reference/components-reference/supported-secret-stores/openbao/), and [`APP_API_TOKEN` app-channel authentication](https://docs.dapr.io/operations/security/app-api-token/).

## Severity Summary

| Severity | Count |
| --- | ---: |
| Critical | 0 |
| High | 0 |
| Medium | 0 |
| Low | 0 |

