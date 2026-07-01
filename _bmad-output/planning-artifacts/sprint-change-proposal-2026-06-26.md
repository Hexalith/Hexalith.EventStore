# Sprint Change Proposal - 2026-06-26

## 1. Issue Summary

The Aspire AppHost registered the Keycloak identity provider resource with the service name `keycloak`. The requested correction is to expose that Aspire service as `security` while preserving Keycloak as the implementation technology.

Trigger: direct user instruction, `$bmad-correct-course the keycloak service name in aspire should be security`.

Evidence from `aspire describe` before the change showed the container display name `keycloak`, `OTEL_SERVICE_NAME=keycloak`, and dependent resources waiting for `keycloak`.

## 2. Impact Analysis

Epic Impact: No epic scope change identified. PRD and epic artifacts were not present under `_bmad-output/planning-artifacts`, so this was treated as a minor direct implementation correction.

Story Impact: No new story required. Existing AppHost and integration fixture behavior needed alignment.

Artifact Conflicts: No PRD, architecture, or UX changes required. The change affects the AppHost app model and integration test fixtures that resolve the Aspire resource by name.

Technical Impact: Rename the Aspire Keycloak resource from `keycloak` to `security`; update test resource lookups from `keycloak` to `security`. Keycloak realm import, OIDC authority generation, ports, dependencies, and auth behavior remain unchanged.

## 3. Recommended Approach

Recommended path: Direct Adjustment.

Rationale: The change is confined to the Aspire resource identity and test harness references. It does not require rollback, MVP review, or backlog restructuring.

Effort: Low.

Risk: Low. Main risk is stale hard-coded resource lookups in tests or tooling; this was mitigated by searching for direct `"keycloak"` resource-name lookups and restarting Aspire to verify the resource display name.

## 4. Detailed Change Proposals

### AppHost

File: `src/Hexalith.EventStore.AppHost/Program.cs`

OLD:

```csharp
keycloak = builder.AddKeycloak("keycloak", keycloakHttpPort)
```

NEW:

```csharp
const string SecurityResourceName = "security";

keycloak = builder.AddKeycloak(SecurityResourceName, keycloakHttpPort)
```

Justification: Aspire service/resource name should be `security`, while the variable and implementation remain Keycloak-specific.

### Integration Fixtures

Files:

- `tests/Hexalith.EventStore.IntegrationTests/Security/AspireTopologyFixture.cs`
- `tests/Hexalith.EventStore.IntegrationTests/Fixtures/KeycloakAuthFixture.cs`

OLD:

```csharp
_app.GetEndpoint("keycloak", "http");
_app.CreateHttpClient("keycloak");
WaitForResourceHealthyAsync("keycloak", cts.Token);
```

NEW:

```csharp
private const string SecurityResourceName = "security";

_app.GetEndpoint(SecurityResourceName, "http");
_app.CreateHttpClient(SecurityResourceName);
WaitForResourceHealthyAsync(SecurityResourceName, cts.Token);
```

Justification: Tests should address the Aspire resource by its app-model name while preserving Keycloak-specific token and realm behavior.

## 5. Implementation Handoff

Scope: Minor.

Route to: Developer agent for direct implementation.

Success criteria:

- `aspire describe` shows the Keycloak container display name as `security`.
- Dependent resources wait on `security`, not `keycloak`.
- Release build succeeds.
- AppHost tests pass.

Verification completed:

- `dotnet restore Hexalith.EventStore.slnx`
- `dotnet build Hexalith.EventStore.slnx --configuration Release --no-restore`
- `dotnet test tests/Hexalith.EventStore.AppHost.Tests/ --configuration Release --no-build`
- `aspire start --apphost src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj --non-interactive --format Json`
- `aspire describe --format Json`

Note: `aspire describe` confirmed the resource display name and `OTEL_SERVICE_NAME` are now `security`. The Keycloak health check still reported the same TLS readiness-probe failure pattern observed before the rename; that is outside the scope of this naming correction.

## 6. Approval

- [x] Approved / ratified — 2026-07-01
- [ ] Approved with changes (noted below)
- [ ] Rejected / revise

Notes: Approved by Administrator. The direct Developer implementation and verification evidence in §5 are accepted.
