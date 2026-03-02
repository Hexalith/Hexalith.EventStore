# Story 14.3: Azure Container Apps Deployment Guide & Configuration

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an operator deploying Hexalith to Azure,
I want a step-by-step walkthrough for deploying the sample application to Azure Container Apps,
so that I can run the system in a cloud-managed environment.

## Acceptance Criteria

1. `docs/guides/deployment-azure-container-apps.md` exists with a complete walkthrough (FR24): DAPR runtime setup for Azure (FR57), step-by-step deployment instructions, Azure resource provisioning (Container Apps Environment, managed DAPR), DAPR component configuration for Azure services, and health/readiness verification (FR26)
2. `samples/deploy/azure/` contains supplementary Bicep templates and DAPR component configs that complement the Aspire publisher output documented in the guide
3. The guide explicitly references what the reader already knows from the Docker Compose and Kubernetes guides and what's new (FR59)
4. The guide explains infrastructure differences between local Docker, Kubernetes, and Azure (FR58)
5. The guide includes resource requirements and scaling guidance (FR63)
6. Event data storage location is documented per backend (FR60)
7. The walkthrough produces a verifiably running system when followed step-by-step (NFR22)
8. The page follows the standard page template with DAPR explained at operational depth

## Tasks / Subtasks

- [x] Task 1: Create `docs/guides/deployment-azure-container-apps.md` (AC: #1, #3, #4, #5, #6, #7, #8)
  - [x] 1.1 Write page header: back-link `[<- Back to Hexalith.EventStore](../../README.md)`, H1 title, intro paragraph, prerequisites blockquote
  - [x] 1.2 Write "What You Already Know" bridge section referencing Docker Compose and Kubernetes guide concepts (FR59) — topology, DAPR building blocks, health endpoints, backend swap. State what's NEW in ACA: managed DAPR (no operator install), Bicep-based IaC, managed identity, Azure-native service integration, Container Apps Environment, Azure Container Registry, built-in scaling rules
  - [x] 1.3 Create "What You'll Deploy" section with Mermaid deployment topology diagram showing: Azure subscription with resource group, Container Apps Environment with managed DAPR, commandapi container app + managed DAPR sidecar, sample container app + managed DAPR sidecar, Azure Container Registry, external state store (Azure Cosmos DB or PostgreSQL Flexible Server), external pub/sub (Azure Service Bus), external OIDC provider (Entra ID)
  - [x] 1.4 Add `<details>` text description for the Mermaid diagram (NFR7 accessibility)
  - [x] 1.5 Write "Prerequisites" section: Azure subscription, Azure CLI (`az`), .NET 10 SDK, Aspire CLI (`dotnet tool install -g Aspire.Cli`), DAPR CLI (for local testing only). Include Azure-specific prerequisites: `az login`, `az extension add --name containerapp`, resource provider registration (`Microsoft.App`, `Microsoft.OperationalInsights`). Note minimum Azure roles needed (Contributor on resource group)
  - [x] 1.6 Write "Generate Azure Deployment Artifacts" section: Aspire ACA publisher command with BOTH bash and PowerShell variants:
    - Bash: `PUBLISH_TARGET=aca EnableKeycloak=false aspire publish --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj -o ./publish-output/azure`
    - PowerShell: `$env:PUBLISH_TARGET='aca'; $env:EnableKeycloak='false'; aspire publish --project src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj -o ./publish-output/azure`
    - Explain `EnableKeycloak=false` is REQUIRED (ACA does not support Keycloak bind mounts; use Entra ID or external OIDC)
    - Provide annotated directory tree of the generated Bicep output (main.bicep, ACR module, Container Apps Environment module, per-service modules)
    - Document which Bicep parameters need manual configuration: resource group name, location, container image tags, environment variables
    - Note: `Aspire.Hosting.Azure.AppContainers` v13.1.2 is a STABLE package (unlike the K8s and Docker publishers which are preview)
  - [x] 1.7 Write "Provision Azure Infrastructure" section with TWO approaches:
    - Approach A (recommended): Deploy the Aspire-generated Bicep via `az deployment group create`
    - Approach B (alternative): Azure CLI commands for step-by-step manual provisioning: create resource group, Container Apps Environment (with `--enable-dapr`), Azure Container Registry, and verify with `az containerapp env show`
    - Both approaches: verify Container Apps Environment is provisioned with DAPR enabled and Log Analytics workspace is connected
  - [x] 1.8 Write "Build and Push Container Images" section: Use `dotnet publish` with container image support (matching K8s guide pattern) OR `docker build`. Push to Azure Container Registry. Show `az acr login`, `docker tag`, `docker push` commands. For Aspire-managed ACR: explain that the Bicep output creates an ACR and the deployment automatically builds and pushes images
  - [x] 1.9 Write "Configure DAPR Components in Azure Container Apps" section: **CRITICAL** — Azure Container Apps has NATIVE managed DAPR. Components are configured at the Container Apps Environment level using a SIMPLIFIED schema (no `apiVersion`, `kind`, `namespace` fields). Show how to create DAPR components via:
    - Azure CLI: `az containerapp env dapr-component set --yaml <file>`
    - Bicep: `Microsoft.App/managedEnvironments/daprComponents` resources
    - Reference the production components in `deploy/dapr/` and explain how to TRANSLATE each YAML to ACA format (strip outer DAPR CRD structure, use `componentType` instead of `type` under spec)
    - For each component: state store (Cosmos DB — Tier 1 recommended), pub/sub (Azure Service Bus topics — Tier 1 recommended), resiliency
    - **CRITICAL: `accesscontrol.yaml` (DAPR Configuration kind) is NOT SUPPORTED in ACA.** Document the ACA-equivalent security approach: (1) component scoping via `scopes` field, (2) Azure-managed mTLS (automatic), (3) ACA network isolation. This replaces the deny-by-default access control pattern from K8s.
    - **Managed Identity for secrets (recommended)**: Use Container Apps managed identity to authenticate to Azure services (Cosmos DB, Service Bus) WITHOUT connection strings. Show `azureClientId` metadata for user-assigned identity or omit for system-assigned. Also show Azure Key Vault secret store component as fallback for non-Azure services
    - Add component scoping: only `commandapi` app-id should have access to state store and pub/sub (D4)
    - Document supported component tiers: Tier 1 (Cosmos DB, Service Bus, Key Vault) = full Microsoft support; Tier 2 (PostgreSQL, Redis, Kafka) = lower priority support
  - [x] 1.10 Write "Configure DAPR Settings on Container Apps" section: Each container app needs DAPR enabled with settings:
    - `az containerapp dapr enable --name commandapi --dapr-app-id commandapi --dapr-app-port 8080 --dapr-app-protocol http`
    - `az containerapp dapr enable --name sample --dapr-app-id sample --dapr-app-port 8080 --dapr-app-protocol http`
    - Or via Bicep: `dapr: { enabled: true, appId: 'commandapi', appPort: 8080, appProtocol: 'http' }`
    - Note: no DAPR operator installation needed — Azure manages the DAPR sidecar lifecycle
  - [x] 1.11 Write "Configure External OIDC Authentication (Entra ID)" section: Create an Entra ID (Azure AD) app registration for commandapi. Set env vars: `Authentication__JwtBearer__Authority`, `Authentication__JwtBearer__Issuer`, `Authentication__JwtBearer__Audience`, `Authentication__JwtBearer__RequireHttpsMetadata=true`. **CRITICAL**: Clear `Authentication__JwtBearer__SigningKey` — if present, the app uses symmetric key validation and ignores OIDC. Provide complete Entra ID walkthrough: create app registration, expose API scope, note authority/issuer/audience values, show token acquisition via `az account get-access-token` or `curl` to the token endpoint. Add troubleshooting: "401 on all requests — check: (a) Is Authority URL reachable? (b) Is SigningKey cleared? (c) Does token `aud` claim match Audience? (d) Is the Entra ID app registration configured with correct redirect URIs?"
  - [x] 1.12 Write "Deploy the Application" section with explicit ordering: (1) Deploy Bicep (creates resource group, ACR, Container Apps Environment), (2) Build and push container images to ACR, (3) Create DAPR components in the environment, (4) Create/update container apps with DAPR settings and environment variables, (5) Verify deployment. Show both Bicep deployment approach and CLI step-by-step. Explain that initial deployment may take 3-5 minutes for environment provisioning
  - [x] 1.13 Write "Verify System Health" section (FR26): Show how to get the container app URL:
    - `az containerapp show --name commandapi --resource-group <rg> --query properties.configuration.ingress.fqdn -o tsv`
    - Check `/health`, `/alive`, `/ready` endpoints using the FQDN
    - Check DAPR sidecar health via container app logs: `az containerapp logs show --name commandapi --resource-group <rg> --type system`
    - Include a Quick Validation Checklist: Environment provisioned? DAPR components loaded? Container apps running? /health returns 200? Can get Entra ID token? Can submit command?
  - [x] 1.14 Write "Send a Test Command" section: Use the container app FQDN (HTTPS), obtain Entra ID token, submit IncrementCounter command via curl/PowerShell, verify event in Cosmos DB or state store
  - [x] 1.15 Write "Where Is My Data?" section (FR60): Explain physical storage per Azure backend — Cosmos DB containers/items, Azure PostgreSQL Flexible Server tables. Composite key pattern `{tenant}||{domain}||{aggregateId}`. Link to deploy/README.md for full backend compatibility matrix
  - [x] 1.16 Write "Resource Requirements & Scaling" section (FR63): Container Apps resource allocation: commandapi 0.5 vCPU / 1Gi memory, sample 0.25 vCPU / 0.5Gi memory. DAPR sidecar resources managed by Azure (no manual configuration needed). Container Apps scaling rules: min/max replicas, HTTP-based scaling, KEDA-based scaling (for Service Bus queue length). **CRITICAL: Actor constraint — commandapi uses DAPR actors for aggregate processing, so `minReplicas` MUST be >= 1 (actors require at least one running instance; scale-to-zero is NOT supported for actor-enabled apps).** Cost estimation guidance: consumption plan vs dedicated plan. Note: Container Apps consumption plan bills per-second of vCPU and memory usage
  - [x] 1.17 Write "Infrastructure Differences: Docker vs Kubernetes vs Azure Container Apps" section (FR58): three-column comparison table — DAPR management (manual containers vs operator-injected vs Azure-managed), component config (file mount vs CRDs vs ACA DAPR components), secret management (.env vs K8s Secrets vs managed identity + Key Vault), networking (Docker network vs K8s Service DNS vs Container Apps Environment internal DNS), scaling (manual vs HPA vs built-in scaling rules), health checks (Docker HEALTHCHECK vs K8s probes vs ACA health probes), image delivery (local build vs registry push vs ACR integration), auth (Keycloak vs external OIDC vs Entra ID native), IaC (docker-compose.yaml vs Helm chart vs Bicep modules)
  - [x] 1.18 Write "Backend Swap" section: Demonstrate switching state store or pub/sub by updating DAPR components in the Container Apps Environment — same zero-code-change principle. Show `az containerapp env dapr-component set` to swap backends
  - [x] 1.19 Write "Troubleshooting" section: Common ACA deployment issues:
    - Container app not starting: check system logs `az containerapp logs show --type system`, image pull failures, insufficient permissions on ACR
    - DAPR component load failures: check environment-level DAPR component status, wrong metadata format (ACA uses different schema than K8s CRDs), missing secrets/managed identity permissions
    - Managed identity issues: identity not assigned, role assignments not propagated (can take up to 10 minutes), wrong resource scope
    - Ingress issues: ingress not enabled, target port mismatch, custom domain configuration
    - Service invocation failures: DAPR app-id mismatch, apps not in same environment, app-to-app communication not enabled
    - 401 on all requests: SigningKey not cleared, Authority URL unreachable, audience mismatch, Entra ID app registration misconfigured
    - Use Azure Portal Container Apps > Monitoring > Log stream for real-time debugging
  - [x] 1.20 Write "Cost Management" section: Brief guidance on ACA consumption vs dedicated pricing, resource right-sizing, scale-to-zero behavior, monitoring costs via Azure Cost Management
  - [x] 1.21 Write "Next Steps" section: Links to deployment progression guide (Story 14-4), DAPR component configuration reference (Story 14-5), security model docs (Story 14-6). Note that production hardening topics (network security groups, private endpoints, WAF, managed identity least-privilege, diagnostic settings) are covered in the Security Model documentation (Story 14-6)
- [x] Task 2: Create supplementary Azure deployment artifacts (AC: #2)
  - [x] 2.1 Create `samples/deploy/azure/` directory
  - [x] 2.2 Document the Aspire ACA publisher approach as the PRIMARY path
  - [x] 2.3 Create `samples/deploy/azure/main.bicep` (or `main.parameters.bicepparam`) as a reference parameter file showing the key parameters to customize from the Aspire-generated Bicep output
  - [x] 2.4 Create `samples/deploy/azure/dapr-components.bicep` as a reference Bicep module showing how to define DAPR state store (Cosmos DB) and pub/sub (Service Bus) components in the Container Apps Environment
  - [x] 2.5 Create `samples/deploy/azure/managed-identity-roles.bicep` as a reference showing the role assignments needed for managed identity to access Cosmos DB and Service Bus
  - [x] 2.6 Add a brief `samples/deploy/azure/README.md` (10 lines or fewer) explaining these are supplementary to the Aspire publisher output — link to `docs/guides/deployment-azure-container-apps.md` for the full guide
- [x] Task 3: Validation (AC: #7)
  - [x] 3.1 Verify the guide structure follows the page template convention (back-link, H1, intro, prerequisites, content, next steps)
  - [x] 3.2 Verify all Mermaid diagrams render correctly
  - [x] 3.3 Verify all code blocks include both bash and PowerShell alternatives where applicable (required for: env var exports, Aspire publish command, pipeline commands; NOT required for platform-neutral commands like `az`, `dotnet`)
  - [x] 3.4 Verify all internal links resolve (to deploy/README.md, quickstart, architecture-overview, Docker Compose guide, K8s guide)
  - [x] 3.5 Run markdownlint on the new files to ensure CI compliance
  - [x] 3.6 Note: Validation tasks 3.1-3.5 are documentation review only in environments without an Azure subscription. The dev agent should note in completion notes which validations were performed and which are deferred to manual operator testing

## Dev Notes

### Architecture Patterns & Constraints

- **Aspire ACA Publisher is the primary manifest generation path.** The AppHost's `Program.cs` (line 82-83) configures the ACA publisher: `builder.AddAzureContainerAppEnvironment("aca")` activated by `PUBLISH_TARGET=aca`. Generated output is Bicep modules.
- **`Aspire.Hosting.Azure.AppContainers` v13.1.2 is STABLE** (unlike `Aspire.Hosting.Kubernetes` and `Aspire.Hosting.Docker` which are preview). This means the generated Bicep output is production-quality.
- **DO NOT create hand-written Bicep as the primary artifact.** The Aspire publisher is the intended path. Reference Bicep in `samples/deploy/azure/` should be supplementary only (DAPR components, managed identity roles, parameter customization).
- **`EnableKeycloak=false` is REQUIRED** for the ACA publisher. Keycloak bind mounts are not supported. Production ACA deployments must use an external OIDC provider (Entra ID recommended).
- **DAPR is MANAGED in Azure Container Apps** — no DAPR operator installation, no sidecar injection annotations, no Sentry for mTLS. Azure manages the DAPR sidecar lifecycle, health, and updates. This is the BIGGEST difference from Kubernetes.
- **DAPR components are environment-level resources** in ACA (unlike K8s CRDs which are namespace-scoped or Docker where they're file-mounted). Configure via Azure CLI `az containerapp env dapr-component set` or Bicep.
- **Generated Bicep does NOT include DAPR configuration.** DAPR components and per-app DAPR settings must be configured separately (same pattern as K8s where DAPR annotations were missing from publisher output).

### Azure Container Apps DAPR Specifics

- **Managed DAPR sidecar**: Azure automatically injects and manages the DAPR sidecar. Enable per container app via `dapr: { enabled: true, appId: '...', appPort: 8080 }` in Bicep or `az containerapp dapr enable`. No operator to install, no annotations to manage.
- **DAPR version in ACA**: Uses format `1.13.6-msft.1` — the `-msft.<number>` suffix indicates Azure-specific security patches. This is NOT the same as the open-source DAPR runtime version. Verify compatibility with DAPR SDK 1.16.1.
- **Component format — DIFFERENT from K8s**: ACA DAPR components use a **simplified schema**. The `apiVersion`, `kind`, and `namespace` fields from standard DAPR YAML are STRIPPED. Components use `componentType`, `version`, `metadata`, `scopes` at the top level. The CLI `az containerapp env dapr-component set --yaml` accepts this simplified format.

  Example ACA component YAML (compare to `deploy/dapr/statestore-cosmosdb.yaml`):
  ```yaml
  componentType: state.azure.cosmosdb
  version: v1
  metadata:
    - name: url
      value: "https://mycosmosdb.documents.azure.com:443/"
    - name: database
      value: "eventstore"
    - name: collection
      value: "actorstate"
    - name: azureClientId
      value: "<managed-identity-client-id>"
  scopes:
    - commandapi
  ```
- **Secret management — CRITICAL difference from K8s**: ACA supports **managed identity** natively. For Azure-native backends (Cosmos DB, Service Bus, Key Vault), use managed identity instead of connection strings. This is MORE SECURE than K8s `secretKeyRef` approach. Priority: (1) Managed identity for Azure services, (2) Azure Key Vault secret store for non-Azure services, (3) ACA secrets as last resort.
  - **System-assigned identity**: Omit `azureClientId` — the sidecar uses the app's system identity automatically
  - **User-assigned identity**: Set `azureClientId` to the managed identity client ID
- **Scoping**: ACA DAPR components support `scopes` to restrict which container apps can access them. Set `scopes: ['commandapi']` on state store and pub/sub components (D4: domain services have zero infrastructure access).
- **No mTLS configuration needed**: Azure manages service-to-service mTLS automatically within a Container Apps Environment. No trust domain, no Sentry, no certificate rotation to configure.
- **Service invocation**: DAPR service invocation works automatically between container apps in the same environment. App-ids must match the container app name.

### CRITICAL ACA Limitations (Verified from Microsoft Docs, Feb 2026)

- **DAPR Configuration spec is NOT SUPPORTED in ACA.** This means `accesscontrol.yaml` (which is a DAPR Configuration kind, not a Component kind) CANNOT be applied directly. In ACA, access control is handled by: (1) component scoping (`scopes` field), (2) Azure-managed mTLS, (3) ACA network security. The guide must document this difference and provide the ACA-equivalent security approach.
- **Actor reminders require `minReplicas >= 1`.** Hexalith.EventStore uses DAPR actors for aggregate processing. Container apps hosting actors CANNOT scale to zero. This impacts cost guidance — commandapi must always have at least 1 replica running.
- **DAPR actor and workflow SDK server extensions are NOT compatible with ACA.** However, the standard `Dapr.Actors` and `Dapr.Actors.AspNetCore` packages (v1.16.1, used by this project) work fine through the managed DAPR sidecar.
- **Only Tier 1 and Tier 2 components are supported.** Tier 1 (fully supported): `state.azure.cosmosdb`, `state.azure.tablestorage`, `pubsub.azure.servicebus.topics`, `secretstores.azure.keyvault`. Tier 2 (lower priority): `state.postgresql`, `state.redis`, `pubsub.kafka`.
- **DAPR Jobs are NOT supported** (not relevant to this project but worth noting).

### Aspire ACA Publisher — CRITICAL DAPR Gap

**The Aspire ACA publisher does NOT generate DAPR configuration.** This is a confirmed gap (GitHub Issue dotnet/aspire#6137, closed as "Not Planned"):
- `WithDaprSidecar()` calls from `CommunityToolkit.Aspire.Hosting.Dapr` are effective for **local development only**
- Generated Bicep will NOT include `dapr: { enabled: true, appId: 'commandapi', ... }` in container app configuration
- Generated Bicep will NOT include `Microsoft.App/managedEnvironments/daprComponents` resources

**The guide must document one of these workarounds:**
1. **Post-deployment CLI**: After Bicep deployment, use `az containerapp dapr enable` and `az containerapp env dapr-component set` to configure DAPR
2. **PublishAsAzureContainerApp callback** (if modifying AppHost is in scope):
   ```csharp
   commandApi.PublishAsAzureContainerApp((module, app) =>
   {
       app.Configuration.Value!.Dapr = new ContainerAppDaprConfiguration
       {
           IsEnabled = true,
           AppId = "commandapi",
           AppProtocol = "http",
           AppPort = 8080
       };
   });
   ```
3. **Supplementary Bicep module**: Maintain a separate `dapr-components.bicep` that adds DAPR config on top of the Aspire-generated infrastructure

The guide should recommend approach 1 (post-deployment CLI) as the simplest and most maintainable, with approach 3 (supplementary Bicep) as the IaC-first alternative. Approach 2 is a code change and should be documented as a note for future AppHost improvement.

### ACA Bicep API Versions

- Container Apps: `Microsoft.App/containerApps@2025-01-01` (GA)
- DAPR Components: `Microsoft.App/managedEnvironments/daprComponents@2025-01-01` (GA) or `2025-10-02-preview` (latest preview)
- Container Apps Environment: `Microsoft.App/managedEnvironments@2025-01-01` (GA)

### Health Check Endpoints

Already implemented in `ServiceDefaults/Extensions.cs`:
- `/health` — full health (200 Healthy/Degraded, 503 Unhealthy)
- `/alive` — liveness probe
- `/ready` — readiness probe

ACA health probes: Container Apps support liveness, readiness, and startup probes similar to Kubernetes. Configure via Bicep `probes` property or Azure CLI. Use the same endpoints as K8s.

DAPR health checks in `CommandApi/HealthChecks/`:
- `dapr-sidecar` → Unhealthy failure status
- `dapr-statestore` → Unhealthy failure status
- `dapr-pubsub` → Degraded failure status
- `dapr-configstore` → Degraded failure status

### DAPR Component Topology (Production)

Production components in `deploy/dapr/` — for ACA, the recommended Azure-native backends are:
- **State store**: `statestore-cosmosdb.yaml` → Azure Cosmos DB (native Azure, global distribution, elastic scale)
- **Pub/sub**: `pubsub-servicebus.yaml` → Azure Service Bus (native Azure, enterprise features)
- **Resiliency**: `resiliency.yaml` → Same retry/timeout/circuit breaker policies
- **Access control**: `accesscontrol.yaml` → Same deny-by-default policies (note: mTLS is Azure-managed, so `DAPR_TRUST_DOMAIN` and `DAPR_NAMESPACE` may not apply in ACA)
- **Subscription**: `subscription-sample-counter.yaml` → Same sample subscription

Alternative non-Azure backends also work in ACA:
- `statestore-postgresql.yaml` → Azure Database for PostgreSQL Flexible Server
- `pubsub-rabbitmq.yaml` → Self-managed or CloudAMQP
- `pubsub-kafka.yaml` → Azure Event Hubs with Kafka protocol or Confluent Cloud

### Configuration Patterns

CommandApi configuration (`src/Hexalith.EventStore.CommandApi/appsettings.json`):
- `EventStore:DomainServices:Registrations` — Domain service routing
- `Authentication:JwtBearer` — JWT auth config
- External OIDC env vars: `Authentication__JwtBearer__Authority`, `Authentication__JwtBearer__Issuer`, `Authentication__JwtBearer__Audience`, `Authentication__JwtBearer__RequireHttpsMetadata`
- **CRITICAL**: If `Authentication__JwtBearer__SigningKey` is present, the app uses symmetric key validation and ignores OIDC Authority. For Entra ID, this MUST be cleared or omitted

### Key Differences: Docker Compose vs Kubernetes vs Azure Container Apps

| Aspect | Docker Compose (14-1) | Kubernetes (14-2) | Azure Container Apps (14-3) |
|--------|----------------------|-------------------|-----------------------------|
| DAPR sidecar | Manual container definitions | Annotation-based auto-injection | Azure-managed (enable per app) |
| Components | File-mounted YAML | Kubernetes CRDs | Environment-level ACA resources |
| Secrets | `.env` file | K8s Secrets + secretKeyRef | Managed identity + Key Vault |
| Networking | Docker network | K8s Service DNS | Container Apps Environment DNS |
| Auth | Keycloak (local) | External OIDC (required) | Entra ID (recommended) |
| Scaling | Manual replicas | HPA / KEDA | Built-in scaling rules + KEDA |
| Health probes | Docker HEALTHCHECK | K8s liveness/readiness probes | ACA probes (same schema as K8s) |
| Manifest source | `aspire publish` → docker-compose.yaml | `aspire publish` → Helm chart | `aspire publish` → Bicep modules |
| mTLS | Not available | DAPR Sentry (manual config) | Azure-managed (automatic) |
| IaC | docker-compose.yaml | Helm + kubectl | Bicep / ARM templates |
| DAPR install | `dapr init` (Docker runtime) | `dapr init -k` (operator) | None (Azure-managed) |

### Page Template Convention

Follow the established documentation page structure (same as Stories 14-1 and 14-2):
1. Back link: `[<- Back to Hexalith.EventStore](../../README.md)`
2. H1 title
3. Opening paragraph explaining what the page covers and who it's for
4. `> **Prerequisites:** [link]` blockquote
5. Content sections with Mermaid diagrams where needed
6. Code blocks with both bash and PowerShell alternatives
7. Notes and tips as blockquotes
8. "Next Steps" section with links to related pages

### Existing Content to Reference (NOT duplicate)

- `deploy/README.md` — Already documents production DAPR components, backend compatibility matrix, per-backend env vars, ACA deployment steps, Aspire publisher commands. Link to this, don't duplicate.
- `docs/getting-started/quickstart.md` — Already covers the Aspire-based local development flow.
- `docs/concepts/architecture-overview.md` — Already covers system topology and DAPR building blocks. Link for context.
- Story 14-1 `docs/guides/deployment-docker-compose.md` — The Docker Compose guide. ACA guide should reference it and build upon shared concepts (FR59).
- Story 14-2 `docs/guides/deployment-kubernetes.md` — The Kubernetes guide. ACA guide should reference it for readers familiar with K8s (FR59).

### Performance Targets (for Resource Requirements section)

- Command submission <50ms p99 (NFR1)
- End-to-end lifecycle <200ms p99 (NFR2)
- DAPR sidecar adds ~1-2ms per building block call (NFR8)
- Default 5-second DAPR sidecar call timeout
- Snapshot every 100 events (manages rehydration to ≤102 reads)
- ACA adds ~5-15ms network latency vs direct container-to-container (Azure internal networking)

### Port Mappings

- CommandApi: 8080 (REST API, used as DAPR app-port)
- DAPR sidecar HTTP: 3500 (managed by Azure — not directly configurable)
- DAPR sidecar gRPC: 50001 (managed by Azure — not directly configurable)
- ACA ingress: 443 (HTTPS, auto-provisioned with Azure-managed TLS certificate)

### Azure-Specific Considerations

- **Resource group**: All ACA resources (environment, container apps, ACR) should be in the same resource group for simplified management
- **Region**: Choose a region that supports Container Apps with DAPR. Most Azure regions support this.
- **Container Registry**: ACR Basic tier is sufficient for development; Standard or Premium for production (geo-replication, private endpoints)
- **Cosmos DB**: Use serverless pricing for development, provisioned throughput for production. The state store uses a single container with the composite key pattern
- **Service Bus**: Basic tier for development (no topics — use Standard or Premium for topic-based pub/sub)
- **Managed Identity**: System-assigned managed identity is simpler for single-app scenarios. Use user-assigned for multi-app shared access
- **Cost**: Consumption plan charges per-second vCPU and memory. Scale-to-zero means zero compute cost when idle. Estimate ~$30-50/month for a dev environment with minimal traffic

### Project Structure Notes

- `docs/guides/` directory exists with Docker Compose and Kubernetes guides already present
- `samples/deploy/` directory exists with `docker-compose/` and `kubernetes/` subdirectories. The `azure/` subdirectory does NOT exist yet and must be created
- `deploy/` directory has production DAPR components and README
- Documentation filename must follow NFR26: descriptive, unabbreviated, hyphen-separated (`deployment-azure-container-apps.md`)

### Security Considerations (for Story 14-6 depth, surface-level here)

The guide should note these topics exist and link to Story 14-6 (Security Model Documentation) for full coverage:
- **Managed Identity**: System-assigned vs user-assigned, principle of least privilege, role assignments for Cosmos DB (DocumentDB Account Contributor), Service Bus (Data Sender/Receiver)
- **Network Security**: VNet integration, private endpoints for Cosmos DB and Service Bus, Container Apps Environment with custom VNet
- **Ingress**: HTTPS-only (Azure-managed TLS), custom domains, IP restrictions, WAF via Azure Front Door
- **DAPR API access**: In ACA, the DAPR sidecar API is only accessible from the app container (no external access by default)
- **Container security**: Azure-managed base images, vulnerability scanning via ACR + Defender for Cloud

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Epic 7, Story 7.3]
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#FR24, FR26, FR57, FR58, FR59, FR60, FR63, NFR7, NFR22, NFR26]
- [Source: _bmad-output/planning-artifacts/architecture-documentation.md#Deployment sections, ACA publisher]
- [Source: _bmad-output/planning-artifacts/architecture.md#D4, D9, D10, D11]
- [Source: src/Hexalith.EventStore.AppHost/Program.cs — Aspire ACA publisher, lines 75, 82-83]
- [Source: src/Hexalith.EventStore.ServiceDefaults/Extensions.cs — Health check endpoints]
- [Source: src/Hexalith.EventStore.CommandApi/HealthChecks/ — DAPR health check implementations]
- [Source: src/Hexalith.EventStore.CommandApi/appsettings.json — Configuration patterns]
- [Source: deploy/README.md — Production DAPR components, ACA deployment steps, Aspire publisher docs]
- [Source: deploy/dapr/ — Production DAPR component YAML files]
- [Source: Directory.Packages.props — Aspire.Hosting.Azure.AppContainers v13.1.2, DAPR SDK v1.16.1]
- [Source: _bmad-output/implementation-artifacts/14-1-docker-compose-deployment-guide-and-configuration.md — Docker Compose story patterns]
- [Source: _bmad-output/implementation-artifacts/14-2-kubernetes-deployment-guide-and-configuration.md — Kubernetes story patterns]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- No debug issues encountered. All documentation tasks completed successfully.

### Completion Notes List

- **Task 1 (subtasks 1.1–1.21):** Created comprehensive `docs/guides/deployment-azure-container-apps.md` (~970 lines) covering:
  - Page header with back-link, H1, intro, prerequisites blockquote (1.1)
  - "What You Already Know" bridge section referencing Docker Compose and K8s guides (1.2)
  - Mermaid deployment topology diagram with ACA-specific Azure resources (1.3)
  - Accessible `<details>` text description for the diagram (1.4)
  - Prerequisites section with Azure-specific setup (az login, extensions, providers) (1.5)
  - Aspire ACA publisher command with bash and PowerShell variants, annotated output (1.6)
  - Two-approach infrastructure provisioning: Bicep deployment and CLI step-by-step (1.7)
  - Container image build and ACR push section (1.8)
  - DAPR component configuration with ACA simplified schema, Cosmos DB state store, Service Bus pub/sub (1.9)
  - Per-app DAPR settings via CLI and Bicep (1.10)
  - Entra ID OIDC authentication walkthrough with troubleshooting (1.11)
  - Explicit deployment ordering (1.12)
  - Health verification with FQDN retrieval and quick validation checklist (1.13)
  - Test command section with curl and PowerShell (1.14)
  - "Where Is My Data?" section for Cosmos DB and PostgreSQL (1.15)
  - Resource requirements, scaling rules, actor constraint, cost estimation (1.16)
  - Three-column Docker vs K8s vs ACA comparison table (1.17)
  - Backend swap section (1.18)
  - Comprehensive troubleshooting section (1.19)
  - Cost management section (1.20)
  - Next steps with links to related stories (1.21)
- **Task 2 (subtasks 2.1–2.6):** Created `samples/deploy/azure/` with 4 files:
  - `main.parameters.bicepparam` — reference parameters for Aspire-generated Bicep
  - `dapr-components.bicep` — reference module for Cosmos DB and Service Bus DAPR components
  - `managed-identity-roles.bicep` — reference module for role assignments
  - `README.md` — brief description linking to the full guide
- **Task 3 (subtasks 3.1–3.6):** Validation updated after review:
  - 3.1: Guide follows page template convention (back-link, H1, intro, prerequisites, content, next steps)
  - 3.2: Mermaid diagram uses valid syntax (rendering verified via structure inspection)
  - 3.3: Code blocks include both bash and PowerShell where applicable (Aspire publish, env var exports, token acquisition, curl)
  - 3.4: All 7 internal links verified to resolve to existing files
  - 3.5: markdownlint-cli2 passes with 0 errors on `docs/guides/deployment-azure-container-apps.md` and `samples/deploy/azure/README.md`
  - 3.6: Documentation-only story — no Azure subscription available for live deployment testing. Live deployment verification remains deferred to manual operator testing.
- **Post-review fixes applied (High/Medium):**
  - Fixed broken supplementary Bicep reference path in `samples/deploy/azure/main.parameters.bicepparam`
  - Fixed Service Bus RBAC assignment scope to namespace-level in `samples/deploy/azure/managed-identity-roles.bicep`
  - Corrected command status key format in `docs/guides/deployment-azure-container-apps.md` to match implementation (`{tenant}:{correlationId}:status`)
  - Removed unrelated local git noise (`.claude/settings.local.json`, `tmpclaude-4bd8-cwd`) from review scope

### File List

- `docs/guides/deployment-azure-container-apps.md` — **NEW** — Main Azure Container Apps deployment guide
- `samples/deploy/azure/main.parameters.bicepparam` — **NEW** — Reference Bicep parameter file
- `samples/deploy/azure/dapr-components.bicep` — **NEW** — Reference DAPR components Bicep module
- `samples/deploy/azure/managed-identity-roles.bicep` — **NEW** — Reference managed identity roles Bicep module
- `samples/deploy/azure/README.md` — **NEW** — Brief README for supplementary artifacts
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — **MODIFIED** — Story status updated
- `_bmad-output/implementation-artifacts/14-3-azure-container-apps-deployment-guide-and-configuration.md` — **MODIFIED** — Story file updated

### Change Log

- 2026-03-02: Created Azure Container Apps deployment guide and supplementary Bicep artifacts (Story 14-3)
- 2026-03-02: Applied senior review fixes (Bicep path, RBAC scope, command status key docs) and re-validated markdownlint

### Senior Developer Review (AI)

- Reviewer: Jerome
- Date: 2026-03-02
- Outcome: Changes requested and fixed in this review pass
- Fixed issues: 5 (3 High, 2 Medium)
- Remaining action items: 0
