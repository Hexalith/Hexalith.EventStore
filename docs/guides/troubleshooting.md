[<- Back to Hexalith.EventStore](../../README.md)

# Troubleshooting Guide

This page covers common errors you may encounter during quickstart setup, DAPR integration, and deployment of Hexalith.EventStore. Issues are organized by symptom so you can quickly find the problem you are experiencing and follow step-by-step resolution instructions.

> **Prerequisites:** [Prerequisites and Local Dev Environment](../getting-started/prerequisites.md), [Quickstart Guide](../getting-started/quickstart.md)

## Quickstart Errors

These issues typically occur when setting up the project for the first time or running the sample application locally.

### Docker Daemon Not Running

**Symptom:** You see connection refused errors when starting the Aspire AppHost or running Docker commands:

```text
Cannot connect to the Docker daemon at unix:///var/run/docker.sock. Is the docker daemon running?
```

**Probable Cause:** Docker Desktop is not started, or the Docker service is not running. On Windows with WSL2, the WSL2 backend may need a restart.

**Resolution:**

1. Check if Docker is running:

    ```bash
    $ docker info
    ```

2. If Docker is not running, start Docker Desktop or the Docker service:

    ```bash
    # Linux
    $ sudo systemctl start docker

    # Windows (PowerShell, admin)
    $ Start-Service docker
    ```

3. If using WSL2 on Windows and Docker Desktop shows errors, restart the WSL2 backend:

    ```bash
    $ wsl --shutdown
    ```

    Then reopen Docker Desktop and wait for it to fully initialize.

4. Verify Docker is operational:

    ```bash
    $ docker run --rm hello-world
    ```

### Port Conflicts

**Symptom:** The application fails to start with an "address already in use" error:

```text
System.IO.IOException: Failed to bind to address http://localhost:8080: address already in use.
```

**Probable Cause:** Another process is using one of the default ports. The Aspire AppHost allocates ports for the Command API (8080), Keycloak (8180), Redis (6379), and PostgreSQL (5432).

**Resolution:**

1. Identify which process is using the port:

    ```bash
    # Linux / macOS
    $ lsof -i :8080

    # Windows (PowerShell)
    $ netstat -ano | findstr :8080
    ```

2. Stop the conflicting process, or change the port in the AppHost configuration. For Keycloak, the port is set in `src/Hexalith.EventStore.AppHost/Program.cs`:

    ```csharp
    // Change 8180 to an available port
    builder.AddKeycloak("keycloak", 8180)
    ```

3. Restart the AppHost:

    ```bash
    $ dotnet run --project src/Hexalith.EventStore.AppHost
    ```

### DAPR Sidecar Timeout

**Symptom:** The application starts but commands fail with HTTP 500 errors. Logs show DAPR sidecar connection failures:

```text
Dapr sidecar is not responding on http://localhost:3500
```

**Probable Cause:** DAPR (Distributed Application Runtime — a portable runtime for building microservices) is not initialized, or its component files are not loaded. DAPR runs as a sidecar process (a small helper application running alongside your main app) that handles state storage, messaging, and other infrastructure concerns.

**Resolution:**

1. Verify DAPR is installed:

    ```bash
    $ dapr --version
    ```

2. If DAPR is not installed, follow the [DAPR installation guide](https://docs.dapr.io/getting-started/install-dapr-cli/).

3. Initialize DAPR for local development. Use slim mode for unit/integration tests, or full mode for end-to-end tests:

    ```bash
    # Slim mode (no Docker containers, for Tier 2 tests)
    $ dapr init --slim

    # Full mode (with Redis and Zipkin containers, for Tier 3 tests)
    $ dapr init
    ```

4. Verify DAPR components are loaded:

    ```bash
    $ ls ~/.dapr/components/
    ```

5. Check DAPR sidecar logs for component loading errors:

    ```bash
    $ dapr logs --app-id commandapi
    ```

### .NET SDK Version Mismatch

**Symptom:** Build errors or unexpected runtime failures. Common error messages include:

```text
The current .NET SDK does not support targeting .NET 10.0. Either target .NET 9.0, or use a version of the .NET SDK that supports .NET 10.0.
```

**Probable Cause:** The project requires .NET 10 SDK version 10.0.102 as specified in `global.json`. An older or incompatible SDK version is installed.

**Resolution:**

1. Check your installed SDK versions:

    ```bash
    $ dotnet --list-sdks
    ```

2. Verify the required version in `global.json`:

    ```json
    {
      "sdk": {
        "version": "10.0.102",
        "rollForward": "latestPatch"
      }
    }
    ```

3. If the required SDK is not installed, download it from the [.NET download page](https://dotnet.microsoft.com/download/dotnet/10.0).

4. Verify the correct SDK is active:

    ```bash
    $ dotnet --version
    # Expected: 10.0.102 or later patch
    ```

### Sample Build Failure

**Symptom:** Restore or build errors when compiling the solution:

```text
error NU1101: Unable to find package Hexalith.EventStore.Contracts.
```

**Probable Cause:** NuGet packages have not been restored, the local package cache is stale, or package versions in `Directory.Packages.props` are outdated.

**Resolution:**

1. Restore all NuGet packages using the solution file:

    ```bash
    $ dotnet restore Hexalith.EventStore.slnx
    ```

2. If restore fails, clear the local NuGet cache and retry:

    ```bash
    $ dotnet nuget locals all --clear
    $ dotnet restore Hexalith.EventStore.slnx
    ```

3. Build in Release configuration to verify everything compiles:

    ```bash
    $ dotnet build Hexalith.EventStore.slnx --configuration Release
    ```

4. If specific package versions are not found, check `Directory.Packages.props` at the repository root for the centralized version definitions.

## DAPR Integration Issues

These issues occur when the application is running but DAPR sidecar communication or component configuration is incorrect. DAPR uses a sidecar architecture where each application instance has a companion DAPR process that handles infrastructure concerns like state management, publish/subscribe messaging, and service invocation.

### Sidecar Injection Failure

**Symptom:** The DAPR sidecar does not start alongside your application. You see only `1/1` ready containers in Kubernetes instead of `2/2`, or no DAPR logs appear in local development.

**Probable Cause:** DAPR is not installed, the Aspire AppHost is not configured to add DAPR sidecars, or in Kubernetes the DAPR operator is not injecting sidecars.

**Resolution:**

1. Verify DAPR CLI is installed and initialized:

    ```bash
    $ dapr --version
    $ dapr status
    ```

2. In local development with Aspire, verify the AppHost configures DAPR sidecars. Check `src/Hexalith.EventStore.AppHost/Program.cs` for `.WithDaprSidecar()` calls.

3. In Kubernetes, verify DAPR annotations are present on your pod spec:

    ```yaml
    annotations:
      dapr.io/enabled: "true"
      dapr.io/app-id: "commandapi"
      dapr.io/app-port: "8080"
    ```

4. Verify the DAPR operator is running on the cluster:

    ```bash
    $ dapr status -k
    ```

5. Check DAPR sidecar logs for initialization errors:

    ```bash
    # Kubernetes
    $ kubectl logs <pod-name> -c daprd
    ```

### State Store Connection Timeout

**Symptom:** State store operations (reading/writing aggregate state, snapshots) time out or return errors:

```text
error connecting to state store: dial tcp 127.0.0.1:6379: connect: connection refused
```

**Probable Cause:** The state store backend (Redis, PostgreSQL, or CosmosDB) is not running, the connection string is wrong, or the DAPR component YAML is misconfigured. DAPR uses state store components to persist data — the actual backend is configured in component YAML files, not in application code.

**Resolution:**

1. Verify the state store backend is running:

    ```bash
    # Redis
    $ docker ps | grep redis
    $ redis-cli ping
    # Expected: PONG

    # PostgreSQL
    $ docker ps | grep postgres
    $ pg_isready -h localhost -p 5432
    ```

2. Check the DAPR state store component configuration. For local development, verify the component YAML in your DAPR components directory:

    ```yaml
    # Example: statestore-postgresql.yaml
    apiVersion: dapr.io/v1alpha1
    kind: Component
    metadata:
      name: statestore
    spec:
      type: state.postgresql
      metadata:
        - name: connectionString
          value: "host=localhost;port=5432;..."
    ```

3. Test connectivity to the backend directly to rule out network issues.

4. Consult the [DAPR Component Configuration Reference](dapr-component-reference.md) for backend-specific configuration details.

### Pub/Sub Message Loss

**Symptom:** Events are published by the Command API but subscribers never receive them. No errors appear in the publisher logs, but subscriber handlers are never invoked.

**Probable Cause:** The topic name does not match between publisher and subscriber, the subscriber is not registered, or DAPR component scoping restricts access. Hexalith.EventStore uses the topic naming pattern `{tenant}.{domain}.events` for event distribution.

**Resolution:**

1. Verify topic naming matches the expected pattern. The publisher sends to topics following the `{tenant}.{domain}.events` convention:

    ```text
    # Example: tenant "acme", domain "Counter"
    Topic: acme.Counter.events
    ```

2. Check that the subscriber application is registered to receive messages on the correct topic. Verify subscription configuration:

    ```yaml
    # Example: subscription-sample-counter.yaml
    apiVersion: dapr.io/v2alpha1
    kind: Subscription
    metadata:
      name: sample-counter-subscription
    spec:
      topic: "{tenant}.Counter.events"
      routes:
        default: /api/events
      pubsubname: pubsub
    ```

3. Verify DAPR component scoping allows both the publisher and subscriber to access the pub/sub component. Check that the `scopes` field in the component YAML includes both app IDs.

4. Inspect the dead-letter topic for failed deliveries. Failed messages route to `deadletter.{tenant}.{domain}.events` and include the full command payload, error details, and correlation ID:

    ```bash
    $ dapr logs --app-id commandapi | grep "deadletter"
    ```

5. Consult the [DAPR Component Configuration Reference](dapr-component-reference.md) for pub/sub backend configuration.

### Actor Activation Conflict

**Symptom:** Actor calls fail with concurrency errors or unexpected behavior when multiple requests target the same aggregate:

```text
Actor reentrant call not allowed for actor type AggregateActor
```

**Probable Cause:** Hexalith.EventStore uses DAPR actors (virtual actors that maintain state and process messages one at a time) with a single-writer model. Each aggregate is represented by a single actor instance, and reentrancy is disabled by design to guarantee event ordering. Concurrent calls to the same aggregate will be serialized, and conflicting operations may be rejected.

**Resolution:**

1. Understand the single-writer model: only one command is processed at a time per aggregate. This is by design to ensure event stream consistency.

2. If you see activation conflicts, check for concurrent calls to the same aggregate ID. Avoid sending multiple commands to the same aggregate simultaneously.

3. Verify DAPR actor configuration in the AppHost. The actor runtime settings control turn-based concurrency:

    ```bash
    # Check actor configuration
    $ dapr logs --app-id commandapi | grep "actor"
    ```

4. If you need to handle high-throughput scenarios, consider using the optimistic concurrency conflict handling endpoint (see [Command API Reference](../reference/command-api.md)).

### Component Configuration Mismatch

**Symptom:** DAPR logs show component loading errors at startup:

```text
error loading component statestore: component statestore is not initialized
```

**Probable Cause:** The DAPR component YAML file has syntax errors, references missing secrets, or specifies a wrong component type. DAPR components are configured via YAML files that define backends for state stores, pub/sub, and other building blocks.

**Resolution:**

1. Validate the YAML syntax of your component files. Common issues include incorrect indentation and missing required fields:

    ```bash
    # Kubernetes: validate manifest syntax client-side
    $ kubectl apply --dry-run=client -f <component.yaml>

    # Local/self-hosted: start DAPR with your resources path and inspect sidecar logs for load errors
    $ dapr run --app-id config-check --resources-path <components-path> -- dotnet --info
    ```

2. Verify the component type matches the installed backend. Available state store components in this project:

    | Component File | Type | Backend |
    |---------------|------|---------|
    | `statestore-postgresql.yaml` | `state.postgresql` | PostgreSQL |
    | `statestore-cosmosdb.yaml` | `state.azure.cosmosdb` | Azure Cosmos DB |

3. Check that all secret references resolve. If your component YAML references secrets, verify they exist:

    ```bash
    # Kubernetes
    $ kubectl get secret <secret-name> -o jsonpath='{.data}'
    ```

4. Verify component files are in the correct directory. For local development, DAPR loads components from `~/.dapr/components/` or the path configured in the Aspire AppHost.

5. Consult the [DAPR Component Configuration Reference](dapr-component-reference.md) for detailed per-backend configuration guidance.

## Docker Compose Deployment Issues

These issues occur when deploying Hexalith.EventStore using Docker Compose. See the [Docker Compose Deployment Guide](deployment-docker-compose.md) for full setup instructions.

### Health Check Failures

**Symptom:** Containers restart repeatedly and show `unhealthy` status:

```bash
$ docker compose ps
# NAME         STATUS          PORTS
# commandapi   restarting      ...
```

**Probable Cause:** The health check endpoint is not ready within the configured interval, or dependency containers (Redis, PostgreSQL) have not started yet.

**Resolution:**

1. Check container logs for startup errors:

    ```bash
    $ docker compose logs commandapi
    $ docker compose logs -f  # Follow all container logs
    ```

2. Verify health and readiness endpoint paths are correct in `docker-compose.yml`:

    ```yaml
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 5
    ```

3. Increase the health check interval if the application needs more time to initialize:

    ```yaml
    healthcheck:
      interval: 60s
      start_period: 30s
    ```

4. Verify that dependency containers are healthy before the application starts. Use `depends_on` with health check conditions.

### Volume Mount Issues

**Symptom:** Data is not persisted between container restarts. State store data or configuration files disappear after `docker compose down && docker compose up`.

**Probable Cause:** Volume mounts are not configured correctly in `docker-compose.yml`, or file permissions prevent the container from writing to the mounted volume.

**Resolution:**

1. Verify volume definitions in `docker-compose.yml`:

    ```yaml
    volumes:
      - postgres-data:/var/lib/postgresql/data
    ```

2. Check that named volumes are defined at the top level of the compose file:

    ```yaml
    volumes:
      postgres-data:
    ```

3. If using bind mounts, verify the host directory exists and has correct permissions:

    ```bash
    $ ls -la /path/to/mount
    ```

4. Use `docker volume ls` and `docker volume inspect <name>` to verify volume state.

### Network Connectivity Issues

**Symptom:** Services cannot reach each other. You see connection refused or DNS resolution errors between containers:

```text
System.Net.Http.HttpRequestException: Connection refused (redis:6379)
```

**Probable Cause:** Docker network configuration is incorrect, or services are using `localhost` instead of container service names for inter-service communication.

**Resolution:**

1. Verify that all services are on the same Docker network:

    ```bash
    $ docker network ls
    $ docker network inspect <network-name>
    ```

2. Use service names (not `localhost`) for inter-container communication. Within Docker Compose, each service is reachable by its service name:

    ```yaml
    # Correct: use service name
    connectionString: "host=postgres;port=5432;..."

    # Wrong: localhost does not resolve to other containers
    connectionString: "host=localhost;port=5432;..."
    ```

3. Verify DNS resolution between containers:

    ```bash
    $ docker compose exec commandapi nslookup redis
    ```

### Keycloak Startup Issues

**Symptom:** Authentication fails in the local environment. Token validation errors appear in the Command API logs:

```text
IDX20803: Unable to obtain configuration from: 'http://keycloak:8180/realms/hexalith/.well-known/openid-configuration'
```

**Probable Cause:** The Keycloak identity provider container is not initialized, the `hexalith` realm has not been imported, or port 8180 is not correctly mapped.

**Resolution:**

1. Verify Keycloak container health:

    ```bash
    $ docker compose ps keycloak
    $ docker compose logs keycloak
    ```

2. Check that the realm import file exists. The Aspire AppHost auto-imports realms from `src/Hexalith.EventStore.AppHost/KeycloakRealms/`:

    ```bash
    $ ls src/Hexalith.EventStore.AppHost/KeycloakRealms/
    # Expected: hexalith-realm.json
    ```

3. Verify the Keycloak port mapping. The default port is 8180 to avoid conflicts with the Command API on port 8080:

    ```bash
    $ curl http://localhost:8180/realms/hexalith/.well-known/openid-configuration
    ```

4. If Keycloak is not needed, disable it by setting the environment variable:

    ```bash
    $ EnableKeycloak=false dotnet run --project src/Hexalith.EventStore.AppHost
    ```

## Kubernetes Deployment Issues

These issues occur when deploying to a Kubernetes cluster with the DAPR operator. See the [Kubernetes Deployment Guide](deployment-kubernetes.md) for full setup instructions.

### Pod Crash Loops

**Symptom:** Pods show `CrashLoopBackOff` status or `1/2` ready (application container running but DAPR sidecar failing):

```bash
$ kubectl get pods
# NAME                         READY   STATUS             RESTARTS
# commandapi-xxxxx             1/2     CrashLoopBackOff   5
```

**Probable Cause:** DAPR sidecar injection issues, missing ConfigMaps or Secrets, or container image pull failures.

**Resolution:**

1. Describe the pod to see events and error details:

    ```bash
    $ kubectl describe pod <pod-name>
    ```

2. Check the main application container logs:

    ```bash
    $ kubectl logs <pod-name> -c commandapi
    ```

3. Check the DAPR sidecar container logs:

    ```bash
    $ kubectl logs <pod-name> -c daprd
    ```

4. Verify DAPR annotations are correct on the deployment:

    ```yaml
    metadata:
      annotations:
        dapr.io/enabled: "true"
        dapr.io/app-id: "commandapi"
        dapr.io/app-port: "8080"
    ```

5. Check for missing ConfigMaps or Secrets referenced by the pod:

    ```bash
    $ kubectl get events --field-selector involvedObject.name=<pod-name>
    ```

### Image Pull Errors

**Symptom:** Pods show `ImagePullBackOff` or `ErrImagePull` status:

```bash
$ kubectl get pods
# NAME                         READY   STATUS             RESTARTS
# commandapi-xxxxx             0/2     ImagePullBackOff   0
```

**Probable Cause:** The container image tag does not exist in the registry, or the cluster does not have credentials to pull from a private registry.

**Resolution:**

1. Verify the image tag exists in the registry:

    ```bash
    $ kubectl describe pod <pod-name> | grep "Image:"
    ```

2. Check `imagePullSecrets` are configured on the deployment:

    ```yaml
    spec:
      imagePullSecrets:
        - name: registry-credentials
    ```

3. Verify the secret exists and contains valid credentials:

    ```bash
    $ kubectl get secret registry-credentials -o jsonpath='{.data.\.dockerconfigjson}' | base64 -d
    ```

4. Test registry access from outside the cluster to rule out image availability issues.

### ConfigMap and Secret Configuration

**Symptom:** The application starts but fails at runtime with missing configuration errors:

```text
System.InvalidOperationException: Configuration key 'Authentication:JwtBearer:Authority' is missing.
```

**Probable Cause:** Required ConfigMaps or Secrets are not created, or environment variable references in the deployment YAML do not match the actual Secret keys.

**Resolution:**

1. List available ConfigMaps and Secrets:

    ```bash
    $ kubectl get configmap
    $ kubectl get secret
    ```

2. Verify ConfigMap data contains the expected keys:

    ```bash
    $ kubectl get configmap <name> -o yaml
    ```

3. Verify Secret data (base64-encoded):

    ```bash
    $ kubectl get secret <name> -o jsonpath='{.data}'
    ```

4. Check that the deployment YAML correctly references the ConfigMap or Secret keys:

    ```yaml
    env:
      - name: Authentication__JwtBearer__Authority
        valueFrom:
          secretKeyRef:
            name: auth-config
            key: authority
    ```

### DAPR Operator Installation Issues

**Symptom:** DAPR annotations on pods are ignored. Sidecars are not injected, and DAPR components are not available.

**Probable Cause:** The DAPR operator is not installed on the Kubernetes cluster, or it is installed in a different namespace than expected.

**Resolution:**

1. Verify DAPR is installed on the cluster:

    ```bash
    $ dapr status -k
    # Expected: dapr-operator, dapr-sidecar-injector, dapr-placement, dapr-sentry all running
    ```

2. If DAPR is not installed, install it:

    ```bash
    $ dapr init -k
    ```

3. If DAPR is installed but sidecars are not injected, verify the `dapr-sidecar-injector` is running:

    ```bash
    $ kubectl get pods -n dapr-system
    ```

4. Check namespace annotations. Some configurations require enabling DAPR injection at the namespace level:

    ```bash
    $ kubectl get namespace <namespace> -o jsonpath='{.metadata.annotations}'
    ```

### Service Mesh Conflicts

**Symptom:** Intermittent connection failures between services, especially when a service mesh (Istio, Linkerd) is also deployed alongside DAPR.

**Probable Cause:** The service mesh mTLS (mutual TLS — encrypted communication between services) conflicts with DAPR's built-in mTLS. Both systems attempt to handle encryption and routing, causing interference.

**Resolution:**

1. Configure sidecar ordering to ensure DAPR initializes before the mesh proxy:

    ```yaml
    annotations:
      dapr.io/sidecar-order: "1"
    ```

2. Exclude DAPR ports from the service mesh:

    ```yaml
    # For Istio
    traffic.sidecar.istio.io/excludeOutboundPorts: "3500,50001"
    traffic.sidecar.istio.io/excludeInboundPorts: "3500,50001"
    ```

3. Consider disabling DAPR mTLS if the service mesh already provides encryption:

    ```bash
    $ dapr mtls -k disable
    ```

4. Consult the [DAPR documentation on service mesh integration](https://docs.dapr.io/operations/hosting/kubernetes/kubernetes-service-mesh/) for your specific mesh.

## Azure Container Apps Deployment Issues

These issues occur when deploying to Azure Container Apps (ACA). See the [Azure Container Apps Deployment Guide](deployment-azure-container-apps.md) for full setup instructions.

### Managed Identity Issues

**Symptom:** Access denied errors when the application tries to reach Azure resources (Key Vault, Cosmos DB, Service Bus):

```text
Azure.Identity.CredentialUnavailableException: ManagedIdentityCredential authentication unavailable.
```

**Probable Cause:** The system-assigned managed identity is not enabled on the Container App, or the necessary role assignments are missing.

**Resolution:**

1. Verify managed identity is assigned:

    ```bash
    $ az containerapp identity show --name commandapi --resource-group <rg>
    ```

2. Check role assignments for the managed identity:

    ```bash
    $ az role assignment list --assignee <identity-principal-id> --output table
    ```

3. Assign missing roles. Common roles needed:

    ```bash
    # Key Vault Secrets User
    $ az role assignment create \
        --assignee <principal-id> \
        --role "Key Vault Secrets User" \
        --scope /subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.KeyVault/vaults/<vault>

    # Cosmos DB account access
    $ az role assignment create \
        --assignee <principal-id> \
        --role "Cosmos DB Built-in Data Contributor" \
        --scope /subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.DocumentDB/databaseAccounts/<account>
    ```

### Key Vault Access Issues

**Symptom:** Secrets fail to load at startup, or you see 403 Forbidden errors when accessing Key Vault:

```text
Azure.RequestFailedException: Status: 403 (Forbidden) - Access denied
```

**Probable Cause:** The Container App's managed identity does not have the required Key Vault access policy or RBAC role.

**Resolution:**

1. Verify the access policy or RBAC assignment:

    ```bash
    # RBAC model
    $ az role assignment list --scope /subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.KeyVault/vaults/<vault> --output table

    # Access policy model
    $ az keyvault show --name <vault> --query "properties.accessPolicies"
    ```

2. If using RBAC, assign the `Key Vault Secrets User` role to the managed identity.

3. Check Key Vault firewall settings. If a firewall is enabled, add the Container App's outbound IP addresses:

    ```bash
    $ az containerapp show --name commandapi --resource-group <rg> --query "properties.outboundIpAddresses"
    ```

### ACR Authentication

**Symptom:** Container image pull failures in Azure Container Apps:

```text
Failed to pull image: unauthorized: authentication required
```

**Probable Cause:** The system-assigned managed identity does not have the `AcrPull` role on the Azure Container Registry.

**Resolution:**

1. Assign the `AcrPull` role to the managed identity:

    ```bash
    $ az role assignment create \
        --assignee <principal-id> \
        --role "AcrPull" \
        --scope /subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.ContainerRegistry/registries/<registry>
    ```

2. Verify the role assignment:

    ```bash
    $ az role assignment list --assignee <principal-id> --scope <acr-scope> --output table
    ```

3. Ensure the Container App is configured to use the managed identity for image pulls:

    ```bash
    $ az containerapp registry set \
        --name commandapi \
        --resource-group <rg> \
        --server <registry>.azurecr.io \
        --identity system
    ```

### Environment Variable Injection

**Symptom:** Configuration values are missing at runtime despite being set in the Azure portal:

```text
System.InvalidOperationException: Configuration key 'Authentication:JwtBearer:Authority' is missing.
```

**Probable Cause:** ACA secrets or environment variables are not configured correctly, or the references in the revision template do not match the secret names.

**Resolution:**

1. List existing secrets:

    ```bash
    $ az containerapp secret list --name commandapi --resource-group <rg> --output table
    ```

2. Verify environment variable references in the revision template:

    ```bash
    $ az containerapp show --name commandapi --resource-group <rg> \
        --query "properties.template.containers[0].env"
    ```

3. Set or update secrets and environment variables:

    ```bash
    # Set a secret
    $ az containerapp secret set \
        --name commandapi \
        --resource-group <rg> \
        --secrets "jwt-authority=https://login.microsoftonline.com/<tenant>/v2.0"

    # Reference secret in environment variable
    $ az containerapp update \
        --name commandapi \
        --resource-group <rg> \
        --set-env-vars "Authentication__JwtBearer__Authority=secretref:jwt-authority"
    ```

### DAPR Managed Services

**Symptom:** DAPR components are not available in Azure Container Apps. The application cannot access state stores or pub/sub:

```text
ERR_DIRECT_INVOKE: app id commandapi not found
```

**Probable Cause:** DAPR is not enabled on the ACA environment, or component scoping does not include the application name. Azure Container Apps provides DAPR as a managed service — components are configured at the environment level.

**Resolution:**

1. Verify DAPR is enabled on the Container Apps environment:

    ```bash
    $ az containerapp env dapr-component list \
        --name <environment> \
        --resource-group <rg> \
        --output table
    ```

2. Enable DAPR on the Container App if not already enabled:

    ```bash
    $ az containerapp dapr enable \
        --name commandapi \
        --resource-group <rg> \
        --dapr-app-id commandapi \
        --dapr-app-port 8080
    ```

3. Verify component scoping matches the app name:

    ```bash
    $ az containerapp env dapr-component show \
        --name <environment> \
        --resource-group <rg> \
        --dapr-component-name statestore \
        --query "properties.scopes"
    ```

4. If components are missing, create them at the environment level following the [DAPR Component Configuration Reference](dapr-component-reference.md).

## Diagnostic Commands Reference

Use these commands to gather diagnostic information when troubleshooting issues across any environment.

### Quick Reference

| Area | Command | Purpose |
|------|---------|---------|
| Docker | `docker info` | Verify Docker daemon status |
| Docker | `docker compose ps` | Check container status |
| Docker | `docker compose logs <service>` | View container logs |
| Docker | `docker network inspect <name>` | Inspect network configuration |
| DAPR | `dapr --version` | Check DAPR CLI version |
| DAPR | `dapr status` | Verify DAPR runtime (local) |
| DAPR | `dapr status -k` | Verify DAPR runtime (Kubernetes) |
| DAPR | `dapr components -k` | List loaded components (Kubernetes) |
| DAPR | `dapr dashboard` | Open visual component dashboard |
| .NET | `dotnet --list-sdks` | List installed .NET SDKs |
| .NET | `dotnet --version` | Show active .NET SDK version |
| .NET | `dotnet nuget locals all --list` | Show NuGet cache locations |
| Kubernetes | `kubectl get pods` | List pod status |
| Kubernetes | `kubectl describe pod <name>` | Detailed pod information |
| Kubernetes | `kubectl logs <pod> -c daprd` | DAPR sidecar logs |
| Kubernetes | `kubectl get events --sort-by='.lastTimestamp'` | Recent cluster events |
| Azure | `az containerapp logs show --name <app> -g <rg>` | ACA application logs |
| Azure | `az containerapp revision list --name <app> -g <rg>` | List app revisions |

### Log Collection Commands

Collect logs for each environment to assist with troubleshooting:

```bash
# Local development (Aspire)
$ dotnet run --project src/Hexalith.EventStore.AppHost 2>&1 | tee aspire-logs.txt

# Docker Compose
$ docker compose logs --timestamps > docker-logs.txt 2>&1

# Kubernetes (all pods in namespace)
$ kubectl logs -l app=commandapi --all-containers --timestamps > k8s-logs.txt 2>&1

# Azure Container Apps
$ az containerapp logs show \
    --name commandapi \
    --resource-group <rg> \
    --type system \
    --follow > aca-logs.txt 2>&1
```

### Correlation ID Tracing

Hexalith.EventStore assigns a correlation ID to every command submission. Use it to trace a command through the entire processing pipeline:

1. **Find the correlation ID** from the HTTP response header when submitting a command.

2. **Search structured logs** for the correlation ID across all services:

    ```bash
    # Docker Compose
    $ docker compose logs | grep "<correlation-id>"

    # Kubernetes
    $ kubectl logs -l app=commandapi --all-containers | grep "<correlation-id>"
    ```

3. **Trace through OpenTelemetry spans** if you have a tracing backend (Zipkin, Jaeger) configured. The correlation ID appears as a span attribute across the complete processing chain.

4. **Check command status** using the status tracking endpoint:

    ```bash
    $ curl http://localhost:8080/api/v1/commands/status/<correlation-id>
    ```

5. **Inspect dead-letter topics** if the command failed. Dead-letter messages include the full command payload, error details, and the correlation ID:

    ```bash
    # Topic pattern: deadletter.{tenant}.{domain}.events
    $ dapr logs --app-id commandapi | grep "deadletter"
    ```

### Dead-Letter Topic Inspection

Failed commands route to dead-letter topics with the naming pattern `deadletter.{tenant}.{domain}.events`. Each dead-letter message contains:

- Full command payload
- Error message and stack trace
- Correlation ID
- Timestamp
- Source actor/processor identity

To inspect dead-letter messages by environment:

```bash
# Local development with Redis pub/sub
$ redis-cli PSUBSCRIBE "deadletter.*"

# Kubernetes with RabbitMQ
$ kubectl exec -it <rabbitmq-pod> -- rabbitmqctl list_queues | grep deadletter

# Azure Container Apps with Service Bus
$ az servicebus topic subscription list \
    --resource-group <rg> \
    --namespace-name <namespace> \
    --topic-name "deadletter.acme.Counter.events"
```

## Next Steps

- **Next:** [Security Model](security-model.md) — security configuration and access control troubleshooting
- **Related:** [Disaster Recovery Procedure](disaster-recovery.md) — backup strategies and recovery procedures per backend
- **Related:** [DAPR Component Configuration Reference](dapr-component-reference.md) — detailed component configuration per backend
- **Related:** [Deployment Progression Guide](deployment-progression.md) — how environments differ and when to use each
- **Related:** [Docker Compose Deployment Guide](deployment-docker-compose.md) — full Docker Compose setup instructions
- **Related:** [Kubernetes Deployment Guide](deployment-kubernetes.md) — Kubernetes deployment with DAPR operator
- **Related:** [Azure Container Apps Deployment Guide](deployment-azure-container-apps.md) — Azure PaaS deployment
- **Related:** [Prerequisites](../getting-started/prerequisites.md) — verify installation requirements
