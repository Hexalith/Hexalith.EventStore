# Story 19.4: DAPR Resiliency Policy Viewer

Status: ready-for-dev

Size: Medium — Creates new resiliency models (`DaprResiliencySpec`, `DaprRetryPolicy`, `DaprTimeoutPolicy`, `DaprCircuitBreakerPolicy`, `DaprResiliencyTarget`, `DaprResiliencyTargetBinding`) in Admin.Abstractions, extends `IDaprInfrastructureQueryService` with `GetResiliencySpecAsync`, adds `GET /api/v1/admin/dapr/resiliency` endpoint to `AdminDaprController`, creates `AdminResiliencyApiClient` UI HTTP client, creates `DaprResiliency.razor` dashboard page with policy cards (retry, timeout, circuit breaker) + target assignment grid + YAML source viewer, adds "Resiliency Policies" button to `DaprComponents.razor`. Creates ~5–7 test classes across 3–4 test projects (~25–35 tests). Extends story 19-1/19-2/19-3's DAPR infrastructure foundation.

**Dependency:** Story 19-1 must be complete (`done`). Stories 19-2 and 19-3 should be complete or merged (this story extends the same controller and service — if those are only `ready-for-dev`, the interface methods they add won't exist yet). Epics 14 and 15 must be complete (both are `done`).

## Definition of Done

- All acceptance criteria verified
- All unit tests green
- Project builds with zero warnings (`dotnet build Hexalith.EventStore.slnx --configuration Release`)
- No new analyzer suppressions
- New `/dapr/resiliency` page renders with policy cards, target assignment grid, and YAML source viewer
- New REST endpoint returns structured JSON with parsed resiliency policies and targets
- DAPR components page (`/dapr`) includes "Resiliency Policies" button
- All existing Tier 1 and Tier 2 tests continue to pass with zero behavioral change
- Resiliency YAML is parsed correctly for both development (AppHost) and production (deploy) configurations
- Missing or invalid YAML is handled gracefully with EmptyState or IssueBanner

## Story

As a **platform operator or DBA using the Hexalith EventStore Admin UI**,
I want **a DAPR resiliency policy viewer showing all configured retry, timeout, and circuit breaker policies with their parameters, and the target assignments mapping policies to specific apps and components**,
so that **I can verify that resiliency configuration is correct, understand the failure handling strategy for each component, diagnose retry/timeout issues during incidents, and confirm that YAML configuration matches intended behavior without manually reading raw YAML files**.

## Acceptance Criteria

1. **AC1: Retry policy cards** — The page displays a "Retry Policies" section with a `FluentCard` per configured retry policy showing: policy name, strategy (constant/exponential), max retries, duration/maxInterval, and any inline YAML comments as tooltip context. Each card uses `FluentBadge` for the strategy type (blue for "exponential", neutral for "constant"). If no retry policies are configured, show informational note "No retry policies defined."

2. **AC2: Timeout policy cards** — A "Timeout Policies" section displays each configured timeout policy with: policy name, general timeout value (formatted as human-readable duration, e.g., "5 seconds"), and any sub-properties. Simple key-value display per timeout policy using a definition list (`<dl>`) pattern inside a `FluentCard`.

3. **AC3: Circuit breaker policy cards** — A "Circuit Breakers" section displays each configured circuit breaker with: policy name, maxRequests (half-open probe count), interval, timeout, and trip condition (e.g., `consecutiveFailures > 5`). Trip condition rendered as a prominent `FluentBadge` with Warning appearance. Each card clearly labels what each parameter controls: "Half-Open Probes" for maxRequests, "Sampling Window" for interval, "Open Duration" for timeout, "Trip Threshold" for trip.

4. **AC4: Target assignment grid** — A `FluentDataGrid<DaprResiliencyTargetBinding>` shows all target assignments with columns: Target Name, Target Type (App/Component), Direction (Inbound/Outbound/General — for components with directional policies), Retry Policy, Timeout Policy, Circuit Breaker Policy. Policy names rendered as `FluentBadge` linking to the corresponding policy card via anchor scroll (e.g., `#policy-{policyName}`). Targets with no policy assigned for a column show "—" (em dash). Sort by target type then target name.

5. **AC5: YAML source viewer** — A collapsible `FluentCard` section (collapsed by default) labeled "Raw YAML Configuration" showing the original resiliency YAML content in a `<pre><code>` block with monospace font. This allows operators to verify the raw configuration alongside the parsed view. Include a "Copy" `FluentButton` that copies the YAML text to clipboard via `navigator.clipboard.writeText()`.

6. **AC6: REST endpoint** — `GET /api/v1/admin/dapr/resiliency` returns `DaprResiliencySpec` containing all parsed policies, target bindings, raw YAML content string, and a `IsConfigurationAvailable` boolean flag. Requires `ReadOnly` authorization policy.

7. **AC7: Page routing and navigation** — Route is `/dapr/resiliency`. The DAPR components page (`/dapr`, from story 19-1) includes a `FluentButton` with `Appearance.Outline` labeled "Resiliency Policies" near the sidecar status card section (adjacent to the "Actor Inspector" and "Pub/Sub Metrics" buttons from stories 19-2 and 19-3). The NavMenu does NOT add a separate link. The `DaprResiliency.razor` page includes a `FluentAnchor` back-link to `/dapr`.

8. **AC8: Loading and error states** — `SkeletonCard` shows during initial load. No auto-refresh timer (resiliency config is static — changes require sidecar restart). Manual "Reload" `FluentButton` triggers a fresh file read and parse. If the YAML file path is not configured: `EmptyState` with title "Resiliency configuration not available" and description "Configure `AdminServer:ResiliencyConfigPath` in appsettings to point to the DAPR resiliency YAML file." If the file exists but parsing fails: `IssueBanner` with the parse error message and the raw YAML still displayed in the source viewer for manual inspection.

9. **AC9: Summary stat cards** — A summary row at the top with four `StatCard` components: Retry Policies (count), Timeout Policies (count), Circuit Breakers (count), Target Bindings (count). All use neutral severity. When configuration is unavailable, all show "N/A".

## Tasks / Subtasks

- [ ] Task 1: Create new models in Admin.Abstractions (AC: #1, #2, #3, #4, #6, #9)
  - [ ] 1.1 Create `DaprRetryPolicy` record in `Models/Dapr/DaprRetryPolicy.cs`
  - [ ] 1.2 Create `DaprTimeoutPolicy` record in `Models/Dapr/DaprTimeoutPolicy.cs`
  - [ ] 1.3 Create `DaprCircuitBreakerPolicy` record in `Models/Dapr/DaprCircuitBreakerPolicy.cs`
  - [ ] 1.4 Create `DaprResiliencyTargetBinding` record in `Models/Dapr/DaprResiliencyTargetBinding.cs`
  - [ ] 1.5 Create `DaprResiliencySpec` record in `Models/Dapr/DaprResiliencySpec.cs`
- [ ] Task 2: Extend service interface and implementation (AC: #6)
  - [ ] 2.0 **MANDATORY pre-step:** Add `YamlDotNet` package reference to `Hexalith.EventStore.Admin.Server.csproj` (already in `Directory.Packages.props` at version 16.3.0 — only needs `<PackageReference Include="YamlDotNet" />` in the csproj).
  - [ ] 2.1 Add `ResiliencyConfigPath` property to `AdminServerOptions` (nullable string, default: null)
  - [ ] 2.2 Add `GetResiliencySpecAsync(CancellationToken ct)` to `IDaprInfrastructureQueryService` — **NOTE: returns non-nullable `DaprResiliencySpec`** (see "Return Type Design" section in Dev Notes)
  - [ ] 2.3 Implement `GetResiliencySpecAsync` in `DaprInfrastructureQueryService`
- [ ] Task 3: Add REST endpoint to existing controller (AC: #6)
  - [ ] 3.1 Add `GetResiliencySpecAsync` endpoint to `AdminDaprController`
- [ ] Task 4: Create UI API client (AC: #6)
  - [ ] 4.1 Create `AdminResiliencyApiClient` in Admin.UI `Services/AdminResiliencyApiClient.cs`
  - [ ] 4.2 Register `AdminResiliencyApiClient` as scoped in `Program.cs` (after existing API client registrations)
- [ ] Task 5: Create resiliency policies page (AC: #1, #2, #3, #4, #5, #7, #8, #9)
  - [ ] 5.1 Create `DaprResiliency.razor` page in Admin.UI `Pages/`
  - [ ] 5.2 Add "Resiliency Policies" button to `DaprComponents.razor` (from story 19-1)
- [ ] Task 6: Write tests (all ACs)
  - [ ] 6.1 Model tests in Admin.Abstractions.Tests (`Models/Dapr/`)
  - [ ] 6.2 Service tests in Admin.Server.Tests (`Services/`)
  - [ ] 6.3 Controller tests in Admin.Server.Tests (`Controllers/`)
  - [ ] 6.4 UI page tests in Admin.UI.Tests (`Pages/`)

## Dev Notes

### Architecture Compliance

This story follows the **exact same architecture** as stories 19-1, 19-2, and 19-3 and all Epic 14/15/19 patterns:

- **Models:** Immutable C# `record` types with constructor validation (`ArgumentException` / `ArgumentNullException`). Located in `Admin.Abstractions/Models/Dapr/` (same subfolder as all other DAPR models).
- **Service extension:** Extends the existing `IDaprInfrastructureQueryService` and `DaprInfrastructureQueryService` — do NOT create a separate service interface. Add the new method to the existing interface and implementation. **IMPORTANT:** Adding a method to `IDaprInfrastructureQueryService` requires awareness that NSubstitute mocks in test files from stories 19-1, 19-2, and 19-3 will inherit the new method. NSubstitute stubs return default values for unconfigured methods, so existing tests should still compile and pass — but verify.
- **Controller extension:** Extends the existing `AdminDaprController` — do NOT create a separate controller. Add new action method to the existing controller.
- **UI API client:** New `AdminResiliencyApiClient` in `Admin.UI/Services/`. Uses `IHttpClientFactory` with `"AdminApi"` named client. Virtual async methods for testability. `HandleErrorStatus(response)` pattern from `AdminDaprApiClient`.
- **Page:** New `DaprResiliency.razor` at `/dapr/resiliency`. Implements `IDisposable` (NOT `IAsyncDisposable` — no PeriodicTimer needed since resiliency config is static). Injects `AdminResiliencyApiClient` and `NavigationManager`.
- **No auto-refresh:** Unlike stories 19-1/19-2/19-3, resiliency configuration is static YAML loaded at sidecar startup. Changes require a sidecar restart. Therefore: NO `PeriodicTimer`, NO `IAsyncDisposable`, NO `CancellationTokenSource`. Use a simple `OnInitializedAsync` load with a manual "Reload" button.

### Key Data Source: Resiliency YAML File

**CRITICAL: DAPR does NOT expose resiliency policies via any runtime API.** The DAPR metadata endpoint (`/v1.0/metadata`) returns components, actors, and subscriptions — but NOT resiliency configuration. There is no DAPR REST API, gRPC API, or SDK method to query resiliency policies at runtime.

**The resiliency configuration must be read directly from the YAML file on disk.**

**Design Rationale — Why file-read over alternatives:**
- **Configuration section (`appsettings.json`):** Would duplicate the DAPR config and require manual synchronization. The YAML file IS the canonical source of truth for DAPR resiliency; reading it directly prevents configuration drift.
- **Kubernetes API (query CRD):** Only works in K8s environments, requires K8s client dependency, doesn't help in Aspire development. Out of scope for v1 (see Kubernetes Future Note below).
- **File-read (chosen):** Single source of truth, works with Aspire + volume-mounted production, zero configuration drift risk. The trade-off is requiring file system access, which is acceptable for server-side admin tooling.

#### File Location Strategy

Add a new property to `AdminServerOptions`:

```csharp
/// <summary>
/// File path to the DAPR resiliency YAML configuration.
/// In Aspire development: set automatically via configuration injection (e.g., "DaprComponents/resiliency.yaml").
/// In production: set to the mounted resiliency YAML path (e.g., "/dapr/components/resiliency.yaml").
/// When null, the resiliency viewer shows "configuration not available" with setup guidance.
/// </summary>
public string? ResiliencyConfigPath { get; init; }
```

**Aspire Configuration Injection:** The `Hexalith.EventStore.AppHost` project (or the admin server's `appsettings.Development.json`) should set this path. For now, the dev agent should add a default value in `appsettings.Development.json`:

```json
{
  "AdminServer": {
    "ResiliencyConfigPath": "DaprComponents/resiliency.yaml"
  }
}
```

**IMPORTANT:** Check if `appsettings.Development.json` exists in the Admin.Server.Host project. If not, add the configuration to `appsettings.json` with a null default. The Aspire AppHost extension may inject the path via environment variable `AdminServer__ResiliencyConfigPath`.

**File path resolution:** The `ResiliencyConfigPath` may be relative (e.g., `"DaprComponents/resiliency.yaml"`). Resolve it to an absolute path using `Path.GetFullPath(configPath, AppContext.BaseDirectory)` before calling `File.ReadAllTextAsync`. This ensures the path is resolved relative to the application's base directory, not the OS working directory — which may differ when Aspire launches the server process. The dev agent MUST use `AppContext.BaseDirectory` (not `Directory.GetCurrentDirectory()`) for consistent behavior across launch modes.

**Kubernetes Future Note:** In Kubernetes deployments, the resiliency spec is typically a DAPR CRD (Custom Resource Definition), not a file on disk. The `ResiliencyConfigPath` approach works for Aspire development and volume-mounted production deployments. A future enhancement could add a Kubernetes API fallback to query the CRD directly — but that is out of scope for this story. The `EmptyState` guidance when no path is configured handles this gap gracefully for now.

#### YAML Parsing with YamlDotNet

**YamlDotNet is already in `Directory.Packages.props` at version 16.3.0.** Add a `<PackageReference Include="YamlDotNet" />` to the `Admin.Server.csproj` file (no version needed — centrally managed).

**MANDATORY pre-check:** Before adding the package reference, verify YamlDotNet 16.3.0 is compatible with .NET 10 and does not pull transitive dependencies that conflict with existing packages. Run `dotnet add src/Hexalith.EventStore.Admin.Server/Hexalith.EventStore.Admin.Server.csproj package YamlDotNet` and verify the restore succeeds with zero warnings. This is a new dependency for Admin.Server (currently has zero YAML dependencies).

**Parsing approach:**

```csharp
// In DaprInfrastructureQueryService.GetResiliencySpecAsync():
public async Task<DaprResiliencySpec> GetResiliencySpecAsync(CancellationToken ct)
{
    string? configPath = _options.ResiliencyConfigPath;
    if (string.IsNullOrWhiteSpace(configPath))
    {
        return DaprResiliencySpec.Unavailable;
    }

    // Resolve relative path against application base directory
    string resolvedPath = Path.IsPathRooted(configPath)
        ? configPath
        : Path.GetFullPath(configPath, AppContext.BaseDirectory);

    // Guard against accidentally large files (e.g., misconfigured path pointing to a binary)
    const long MaxFileSizeBytes = 1_048_576; // 1 MB
    try
    {
        long fileSize = new FileInfo(resolvedPath).Length;
        if (fileSize > MaxFileSizeBytes)
        {
            _logger.LogWarning("Resiliency config file too large ({Size} bytes): {Path}", fileSize, resolvedPath);
            return DaprResiliencySpec.ReadError(resolvedPath, $"File exceeds maximum size of {MaxFileSizeBytes / 1024}KB ({fileSize} bytes)");
        }
    }
    catch (FileNotFoundException)
    {
        _logger.LogWarning("Resiliency config file not found: {Path}", resolvedPath);
        return DaprResiliencySpec.NotFound(resolvedPath);
    }

    // Read the YAML file
    string yamlContent;
    try
    {
        yamlContent = await File.ReadAllTextAsync(resolvedPath, ct).ConfigureAwait(false);
    }
    catch (FileNotFoundException)
    {
        _logger.LogWarning("Resiliency config file not found: {Path}", configPath);
        return DaprResiliencySpec.NotFound(configPath);
    }
    catch (UnauthorizedAccessException ex)
    {
        // Catches: path points to a directory, or permission denied
        _logger.LogError(ex, "Cannot read resiliency config (access denied or path is a directory): {Path}", configPath);
        return DaprResiliencySpec.ReadError(configPath, ex.Message);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to read resiliency config: {Path}", configPath);
        return DaprResiliencySpec.ReadError(configPath, ex.Message);
    }

    // Parse YAML using YamlDotNet
    try
    {
        // Use YamlDotNet Deserializer with internal DTOs, then map to public models
        return ParseResiliencyYaml(yamlContent);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to parse resiliency YAML: {Path}", configPath);
        return DaprResiliencySpec.ParseError(configPath, yamlContent, ex.Message);
    }
}
```

**YAML Structure to Parse (from `deploy/dapr/resiliency.yaml`):**

```yaml
apiVersion: dapr.io/v1alpha1
kind: Resiliency
metadata:
  name: resiliency
spec:
  policies:
    retries:
      defaultRetry:
        policy: exponential
        maxInterval: 15s
        maxRetries: 10
      pubsubRetryOutbound:
        policy: exponential
        maxInterval: 10s
        maxRetries: 5
      pubsubRetryInbound:
        policy: exponential
        maxInterval: 60s
        maxRetries: 20
    timeouts:
      daprSidecar:
        general: 5s
      pubsubTimeout: 10s
      subscriberTimeout: 30s
    circuitBreakers:
      defaultBreaker:
        maxRequests: 1
        interval: 60s
        timeout: 60s
        trip: consecutiveFailures > 5
      pubsubBreaker:
        maxRequests: 1
        interval: 60s
        timeout: 60s
        trip: consecutiveFailures > 10
  targets:
    apps:
      commandapi:
        retry: defaultRetry
        timeout: daprSidecar
        circuitBreaker: defaultBreaker
    components:
      pubsub:
        outbound:
          retry: pubsubRetryOutbound
          timeout: pubsubTimeout
          circuitBreaker: pubsubBreaker
        inbound:
          retry: pubsubRetryInbound
          timeout: subscriberTimeout
      statestore:
        retry: defaultRetry
        timeout: daprSidecar
        circuitBreaker: defaultBreaker
```

**YAML parsing notes:**
- **Timeout values** can be either a simple string (e.g., `pubsubTimeout: 10s`) or a nested object with properties (e.g., `daprSidecar: { general: 5s }`). The parser must handle both forms.
- **YamlDotNet deserialization approach:** Use `IDeserializer` with `DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build()` and deserialize to a `Dictionary<string, object>` or internal DTO hierarchy. Do NOT expose YamlDotNet types in public models.
- **Internal DTOs for YAML deserialization** should be `private sealed class` types inside `DaprInfrastructureQueryService`, similar to the `DaprMetadataResponse` DTOs in the same service for JSON parsing (story 19-3 pattern). Map from DTOs to public `record` types after parsing.
- **Recommended parsing approach:** Use `YamlDotNet.RepresentationModel.YamlStream` for flexible parsing of mixed structures (simple values and nested objects). This avoids strict DTO mapping issues with DAPR's polymorphic YAML structure — particularly the timeout section where values alternate between simple strings and nested objects. `YamlStream` handles this naturally by traversing `YamlMappingNode`/`YamlScalarNode` types dynamically. The strict `IDeserializer` + DTO approach is an alternative but will require special handling (e.g., `object`-typed fields or custom type converters) to accommodate the polymorphic timeout structure.

**YamlStream parsing skeleton:**

```csharp
private static DaprResiliencySpec ParseResiliencyYaml(string yamlContent)
{
    var yaml = new YamlStream();
    yaml.Load(new StringReader(yamlContent));

    var root = (YamlMappingNode)yaml.Documents[0].RootNode;
    var spec = (YamlMappingNode)root["spec"];
    var policies = (YamlMappingNode)spec["policies"];

    // Parse retries
    var retries = new List<DaprRetryPolicy>();
    if (policies.Children.TryGetValue(new YamlScalarNode("retries"), out var retriesNode)
        && retriesNode is YamlMappingNode retriesMap)
    {
        foreach (var (key, value) in retriesMap.Children)
        {
            var name = ((YamlScalarNode)key).Value!;
            var props = (YamlMappingNode)value;
            retries.Add(new DaprRetryPolicy(
                Name: name,
                Strategy: GetScalar(props, "policy") ?? "unknown",
                MaxRetries: int.TryParse(GetScalar(props, "maxRetries"), out var mr) ? mr : 0,
                Duration: GetScalar(props, "duration"),
                MaxInterval: GetScalar(props, "maxInterval")));
        }
    }

    // Parse timeouts — handles BOTH simple string and nested object forms
    var timeouts = new List<DaprTimeoutPolicy>();
    if (policies.Children.TryGetValue(new YamlScalarNode("timeouts"), out var timeoutsNode)
        && timeoutsNode is YamlMappingNode timeoutsMap)
    {
        foreach (var (key, value) in timeoutsMap.Children)
        {
            var name = ((YamlScalarNode)key).Value!;
            if (value is YamlScalarNode scalar)
            {
                // Form 1: simple value (e.g., pubsubTimeout: 10s)
                timeouts.Add(new DaprTimeoutPolicy(name, scalar.Value!));
            }
            else if (value is YamlMappingNode nested)
            {
                // Form 2: nested object (e.g., daprSidecar: { general: 5s })
                var general = GetScalar(nested, "general");
                if (general is not null)
                    timeouts.Add(new DaprTimeoutPolicy(name, general));
            }
        }
    }

    // Parse circuitBreakers (similar pattern to retries — all fields are scalars)
    // ...

    // Parse targets — flatten directional and non-directional bindings
    // ...

    return new DaprResiliencySpec(retries, timeouts, circuitBreakers, targetBindings,
        IsConfigurationAvailable: true, RawYamlContent: yamlContent, ErrorMessage: null);
}

private static string? GetScalar(YamlMappingNode node, string key)
{
    return node.Children.TryGetValue(new YamlScalarNode(key), out var value)
        && value is YamlScalarNode scalar ? scalar.Value : null;
}
```

**Forward-compatibility note:** DAPR may add new policy types in future versions (e.g., `rateLimits`, `bulkheads`). The parser should only process known sections (`retries`, `timeouts`, `circuitBreakers`) and **log a warning** for any unrecognized policy sections under `spec.policies` — do NOT throw an exception. Unknown sections should be silently skipped so the viewer still works with newer DAPR resiliency YAML that has additional policy types. The raw YAML viewer (AC5) will show the full content including unknown sections.

### Model Definitions

#### DaprRetryPolicy

```csharp
/// <summary>Parsed DAPR resiliency retry policy.</summary>
public record DaprRetryPolicy(
    string Name,
    string Strategy,        // "constant" or "exponential"
    int MaxRetries,
    string? Duration,       // For constant policy (e.g., "1s")
    string? MaxInterval);   // For exponential policy (e.g., "15s")
```

- `Name`: policy identifier from YAML key (e.g., "defaultRetry", "pubsubRetryOutbound")
- `Strategy`: validated non-null/non-empty, from YAML `policy` field
- `MaxRetries`: from YAML `maxRetries` field
- `Duration`: only set for constant strategy (from YAML `duration` field)
- `MaxInterval`: only set for exponential strategy (from YAML `maxInterval` field)

#### DaprTimeoutPolicy

```csharp
/// <summary>Parsed DAPR resiliency timeout policy.</summary>
public record DaprTimeoutPolicy(
    string Name,
    string Value);          // Timeout duration string (e.g., "5s", "10s", "30s")
```

- `Name`: policy identifier from YAML key (e.g., "daprSidecar", "pubsubTimeout")
- `Value`: the timeout duration string. For nested objects like `daprSidecar: { general: 5s }`, flatten to the `general` value.

**IMPORTANT:** DAPR timeout YAML has two structural forms:
```yaml
# Form 1: Simple value
pubsubTimeout: 10s

# Form 2: Nested object with 'general' key
daprSidecar:
  general: 5s
```

The parser must detect and handle both. For Form 2, extract the `general` value. If other sub-keys exist besides `general`, create separate timeout entries (e.g., `daprSidecar.general`, `daprSidecar.per-route`). For now, only `general` is used in the project's YAML.

#### DaprCircuitBreakerPolicy

```csharp
/// <summary>Parsed DAPR resiliency circuit breaker policy.</summary>
public record DaprCircuitBreakerPolicy(
    string Name,
    int MaxRequests,        // Half-open probe count
    string Interval,        // Sampling window (e.g., "60s")
    string Timeout,         // Open state duration (e.g., "60s")
    string Trip);           // Trip condition expression (e.g., "consecutiveFailures > 5")
```

- All fields validated non-null/non-empty except `MaxRequests` (int, validated >= 0)
- `Trip`: the raw trip expression string from YAML — display as-is

#### DaprResiliencyTargetBinding

```csharp
/// <summary>Flattened target-to-policy assignment for grid display.</summary>
public record DaprResiliencyTargetBinding(
    string TargetName,      // e.g., "commandapi", "pubsub", "statestore"
    string TargetType,      // "App" or "Component"
    string? Direction,      // "Inbound", "Outbound", or null (for apps and non-directional components)
    string? RetryPolicy,    // Name of assigned retry policy, or null
    string? TimeoutPolicy,  // Name of assigned timeout policy, or null
    string? CircuitBreakerPolicy); // Name of assigned circuit breaker policy, or null
```

**Flattening logic for targets:**
- **App targets** (`targets.apps.*`): one binding per app, `Direction = null`
- **Component targets without direction** (`targets.components.statestore`): one binding, `Direction = null`
- **Component targets with direction** (`targets.components.pubsub.outbound/inbound`): TWO bindings — one per direction

Example from the project's YAML → 4 bindings:
| TargetName | TargetType | Direction | RetryPolicy | TimeoutPolicy | CircuitBreakerPolicy |
|---|---|---|---|---|---|
| commandapi | App | — | defaultRetry | daprSidecar | defaultBreaker |
| pubsub | Component | Outbound | pubsubRetryOutbound | pubsubTimeout | pubsubBreaker |
| pubsub | Component | Inbound | pubsubRetryInbound | subscriberTimeout | — |
| statestore | Component | — | defaultRetry | daprSidecar | defaultBreaker |

#### DaprResiliencySpec

```csharp
/// <summary>Complete parsed DAPR resiliency specification.</summary>
public record DaprResiliencySpec(
    IReadOnlyList<DaprRetryPolicy> RetryPolicies,
    IReadOnlyList<DaprTimeoutPolicy> TimeoutPolicies,
    IReadOnlyList<DaprCircuitBreakerPolicy> CircuitBreakerPolicies,
    IReadOnlyList<DaprResiliencyTargetBinding> TargetBindings,
    bool IsConfigurationAvailable,
    string? RawYamlContent,
    string? ErrorMessage);
```

- `IsConfigurationAvailable`: true if YAML was successfully read and parsed
- `RawYamlContent`: original YAML file content (for the source viewer), null when file not found
- `ErrorMessage`: parse error message, null on success or when config path not set

**Factory methods for error states:**

```csharp
public static DaprResiliencySpec Unavailable => new(
    [], [], [], [], IsConfigurationAvailable: false, RawYamlContent: null, ErrorMessage: null);

public static DaprResiliencySpec NotFound(string path) => new(
    [], [], [], [], IsConfigurationAvailable: false, RawYamlContent: null,
    ErrorMessage: $"Resiliency configuration file not found: {path}");

public static DaprResiliencySpec ReadError(string path, string error) => new(
    [], [], [], [], IsConfigurationAvailable: false, RawYamlContent: null,
    ErrorMessage: $"Failed to read resiliency configuration from {path}: {error}");

public static DaprResiliencySpec ParseError(string path, string rawYaml, string error) => new(
    [], [], [], [], IsConfigurationAvailable: false, RawYamlContent: rawYaml,
    ErrorMessage: $"Failed to parse resiliency YAML from {path}: {error}");
```

**JSON serialization note:** This record uses static factory methods that return instances with null `RawYamlContent` and `ErrorMessage` — a pattern not used in previous story models (e.g., `DaprPubSubOverview` has no factory methods or error states). `System.Text.Json` will serialize null properties by default (e.g., `"errorMessage": null`). This is the intended behavior — the UI checks `IsConfigurationAvailable` first, then inspects `ErrorMessage` and `RawYamlContent` for error display. Do NOT add `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]` — null values must be present in the JSON response so the UI can distinguish between "no error" (null) and "error with message" (non-null).

### Return Type Design: Non-nullable Result Object

**Intentional divergence from existing patterns.** All other methods in `IDaprInfrastructureQueryService` return nullable types (`DaprSidecarInfo?`, `DaprActorInstanceState?`) or non-nullable lists. `GetResiliencySpecAsync` returns a non-nullable `DaprResiliencySpec` — the caller never gets null.

**Why:** This method has 4 distinct failure modes (not configured, file not found, read error, parse error) that cannot be represented by a simple null. The "result object" pattern with `IsConfigurationAvailable` + `ErrorMessage` + `RawYamlContent` provides the UI with enough context to render the correct error state. Returning null would force the controller/UI to guess why the data is missing.

**Impact on NSubstitute mocks:** Unconfigured NSubstitute mocks return `default` for reference types — which is `null` for records. Since the return type is non-nullable, this is technically a contract violation but won't crash at compile time. Existing tests won't call this method, so they won't be affected. New tests must explicitly configure the mock return value.

### Controller Endpoint

Add to the existing `AdminDaprController` (from story 19-1). The controller already has `[Route("api/v1/admin/dapr")]` and `[Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]`.

```csharp
[HttpGet("resiliency")]
[ProducesResponseType(typeof(DaprResiliencySpec), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public async Task<IActionResult> GetResiliencySpecAsync(CancellationToken ct)
```

**Note:** Return type is `Task<IActionResult>` (not `Task<ActionResult<T>>`) — matching all existing endpoints in `AdminDaprController`.

- Return `Ok(spec)` on success — even when `IsConfigurationAvailable = false` (unavailable config is a valid response, not an error)
- Return `500 InternalServerError` with `ProblemDetails` only on unexpected exceptions
- Do NOT return 503 for missing YAML — the service itself is healthy, it just doesn't have resiliency config to show
- Follow the exact error handling pattern from existing endpoints in the same controller

### UI API Client: AdminResiliencyApiClient

**CRITICAL:** All existing Admin UI API clients use **primary constructors** with both `IHttpClientFactory` and `ILogger<T>` parameters. They create the `HttpClient` per-call (not stored in a field). Follow the exact pattern from `AdminDaprApiClient`:

```csharp
// Admin.UI/Services/AdminResiliencyApiClient.cs
public class AdminResiliencyApiClient(
    IHttpClientFactory httpClientFactory,
    ILogger<AdminResiliencyApiClient> logger)
{
    public virtual async Task<DaprResiliencySpec?> GetResiliencySpecAsync(CancellationToken ct = default)
    {
        using HttpResponseMessage response = await httpClientFactory.CreateClient("AdminApi")
            .GetAsync("api/v1/admin/dapr/resiliency", ct).ConfigureAwait(false);
        HandleErrorStatus(response);
        return await response.Content.ReadFromJsonAsync<DaprResiliencySpec>(ct).ConfigureAwait(false);
    }

    private static void HandleErrorStatus(HttpResponseMessage response)
    {
        // Follow exact pattern from AdminDaprApiClient:
        // - 401 -> throw UnauthorizedAccessException
        // - 403 -> throw ForbiddenAccessException
        // - 503 -> throw ServiceUnavailableException
        // - Other non-success -> throw HttpRequestException
    }
}
```

Register in `Program.cs` after existing API client registrations (after `AdminActorApiClient` and `AdminPubSubApiClient` registrations):
```csharp
builder.Services.AddScoped<AdminResiliencyApiClient>();
```

**STJ deserialization note:** `DaprResiliencySpec` uses `IReadOnlyList<T>` for its collection properties. `System.Text.Json` can deserialize `IReadOnlyList<T>` by default (it deserializes to `List<T>` which implements `IReadOnlyList<T>`). However, verify this works end-to-end by testing the full HTTP roundtrip in at least one integration test or manual test — STJ configuration may vary between server and client. If deserialization fails, change the record's collection types to `List<T>` (concrete type) instead of `IReadOnlyList<T>` (interface). Check how existing models like `DaprActorRuntimeInfo` (which also uses `IReadOnlyList<DaprActorTypeInfo>`) are serialized — follow the same pattern.

### UI Page: DaprResiliency.razor

Route: `@page "/dapr/resiliency"`

**Injected dependencies:**
- `AdminResiliencyApiClient` — for resiliency spec data
- `NavigationManager` — for navigation
- `IJSRuntime` — for clipboard interop (AC5 "Copy" button). Use `await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", yamlContent)`. Check if any existing Admin.UI page uses clipboard — if a shared helper exists, reuse it. If not, a one-liner inline JS call via `IJSRuntime.InvokeVoidAsync` is sufficient. Do NOT create a shared clipboard utility for a single use.
- `ILogger<DaprResiliency>` — for exception logging

**Layout (top to bottom):**

1. **Header:** Page title "DAPR Resiliency Policies" + `FluentAnchor` back-link to `/dapr`

2. **Summary stat cards row:** Four `StatCard` components:
   - Retry Policies: `spec.RetryPolicies.Count` (or "N/A")
   - Timeout Policies: `spec.TimeoutPolicies.Count` (or "N/A")
   - Circuit Breakers: `spec.CircuitBreakerPolicies.Count` (or "N/A")
   - Target Bindings: `spec.TargetBindings.Count` (or "N/A")

3. **Retry policies section:** `FluentCard` per retry policy:
   - Policy name as card header with `id="policy-{Name}"` for anchor linking
   - Strategy as `FluentBadge` (Appearance.Accent for "exponential", Appearance.Neutral for "constant")
   - Key-value pairs: "Max Retries" → value, "Max Interval" → value (exponential) or "Duration" → value (constant)
   - Use `<dl>` definition list for clean layout

4. **Timeout policies section:** `FluentCard` per timeout policy:
   - Policy name as card header with anchor id
   - Timeout value prominently displayed
   - Simple, clean single-value card

5. **Circuit breaker policies section:** `FluentCard` per circuit breaker:
   - Policy name as card header with anchor id
   - Trip condition as prominent `FluentBadge` with `Appearance.Warning`
   - Definition list: "Half-Open Probes" → maxRequests, "Sampling Window" → interval, "Open Duration" → timeout
   - Human-readable labels (NOT raw YAML field names)

6. **Target assignment grid:** `FluentDataGrid<DaprResiliencyTargetBinding>` with columns:
   - Target Name
   - Target Type (`FluentBadge`: "App" in accent, "Component" in neutral)
   - Direction (or "—" if null)
   - Retry Policy (as `FluentAnchor` linking to `#policy-{name}`, or "—")
   - Timeout Policy (as `FluentAnchor` linking to `#policy-{name}`, or "—")
   - Circuit Breaker (as `FluentAnchor` linking to `#policy-{name}`, or "—")

7. **YAML source viewer:** Collapsible `FluentCard` (collapsed by default):
   - "Raw YAML Configuration" header with expand/collapse toggle
   - `<pre><code>` block with the raw YAML content
   - "Copy to Clipboard" `FluentButton` using `navigator.clipboard.writeText()` via JS interop
   - When YAML is not available, section is hidden

**No PeriodicTimer — manual reload only:**
```csharp
private DaprResiliencySpec? _spec;
private bool _isLoading = true;

protected override async Task OnInitializedAsync()
{
    await LoadDataAsync();
}

private async Task LoadDataAsync()
{
    _isLoading = true;
    try
    {
        _spec = await ResiliencyClient.GetResiliencySpecAsync();
    }
    catch (Exception ex)
    {
        Logger.LogWarning(ex, "Failed to fetch resiliency spec");
        _spec = null;
    }
    finally
    {
        _isLoading = false;
    }
}

private async Task ReloadAsync()
{
    await LoadDataAsync();
    StateHasChanged();
}
```

**Reload button UX:** The "Reload" `FluentButton` must be **disabled while `_isLoading` is true** to prevent rapid-fire clicks that would spawn concurrent file reads. Use `Disabled="@_isLoading"` on the button. Show a spinner icon or "Loading..." text while disabled to indicate activity.

### Integration with Story 19-1

Add a "Resiliency Policies" button to `DaprComponents.razor` (created by story 19-1), near the sidecar status card section, adjacent to the "Actor Inspector" button from story 19-2 and "Pub/Sub Metrics" button from story 19-3:
```razor
<FluentButton Appearance="Appearance.Outline"
              OnClick="@(() => NavigationManager.NavigateTo("/dapr/resiliency"))">
    Resiliency Policies
</FluentButton>
```

### Reuse Existing Shared Components

Do NOT create new shared components. Reuse:
- `StatCard` — for summary metrics (policy counts)
- `IssueBanner` — for parse errors and configuration warnings
- `EmptyState` — for when no configuration is available
- `SkeletonCard` — for loading state
- `FluentBadge` — for policy strategy types, target types, and trip conditions

**Note:** `StatusBadge` is NOT needed — resiliency policies don't have health status. `PeriodicTimer` is NOT needed — config is static.

### Interface Coexistence

`IDaprInfrastructureQueryService` now has methods for: components (19-1), sidecar info (19-1), actor runtime (19-2), actor state (19-2), pub/sub overview (19-3), and resiliency spec (this story). This is by design — all DAPR infrastructure queries live in one service. Do NOT split into separate interfaces.

### Project Structure Notes

All new files go in existing projects — no new `.csproj` files:

| File | Project | Path |
|------|---------|------|
| `DaprRetryPolicy.cs` | Admin.Abstractions | `Models/Dapr/DaprRetryPolicy.cs` |
| `DaprTimeoutPolicy.cs` | Admin.Abstractions | `Models/Dapr/DaprTimeoutPolicy.cs` |
| `DaprCircuitBreakerPolicy.cs` | Admin.Abstractions | `Models/Dapr/DaprCircuitBreakerPolicy.cs` |
| `DaprResiliencyTargetBinding.cs` | Admin.Abstractions | `Models/Dapr/DaprResiliencyTargetBinding.cs` |
| `DaprResiliencySpec.cs` | Admin.Abstractions | `Models/Dapr/DaprResiliencySpec.cs` |
| `AdminResiliencyApiClient.cs` | Admin.UI | `Services/AdminResiliencyApiClient.cs` |
| `DaprResiliency.razor` | Admin.UI | `Pages/DaprResiliency.razor` |

**Modified files:**

| File | Change |
|------|--------|
| `IDaprInfrastructureQueryService.cs` | Add `GetResiliencySpecAsync` method |
| `DaprInfrastructureQueryService.cs` | Implement `GetResiliencySpecAsync` (YAML read + parse) |
| `AdminDaprController.cs` | Add `GetResiliencySpecAsync` endpoint |
| `AdminServerOptions.cs` | Add `ResiliencyConfigPath` property |
| `DaprComponents.razor` | Add "Resiliency Policies" button (next to existing buttons) |
| `Program.cs` (Admin.UI) | Register `AdminResiliencyApiClient` |
| `Admin.Server.csproj` | Add `<PackageReference Include="YamlDotNet" />` |

**Test files:**

| File | Project | Path |
|------|---------|------|
| `DaprRetryPolicyTests.cs` | Admin.Abstractions.Tests | `Models/Dapr/` |
| `DaprCircuitBreakerPolicyTests.cs` | Admin.Abstractions.Tests | `Models/Dapr/` |
| `DaprResiliencySpecTests.cs` | Admin.Abstractions.Tests | `Models/Dapr/` |
| `DaprResiliencyQueryServiceTests.cs` | Admin.Server.Tests | `Services/` |
| `AdminDaprControllerResiliencyTests.cs` | Admin.Server.Tests | `Controllers/` |
| `DaprResiliencyPageTests.cs` | Admin.UI.Tests | `Pages/` |

### Testing Standards

- **Framework:** xUnit 2.9.3, **Assertions:** Shouldly 4.3.0, **Mocking:** NSubstitute 5.3.0
- Follow existing test patterns from story 19-1 and 19-2 test files

- **Model tests:** Validate constructor argument validation (null/empty strings throw `ArgumentException`). Test all five model types. Test `DaprResiliencySpec` factory methods (`Unavailable`, `NotFound`, `ParseError`). Verify `IsConfigurationAvailable` is false for error states and true for valid specs.

- **Service tests:** The YAML parsing is the core logic — this is the highest-risk area in the story. Test these scenarios:
  - **CRITICAL — Production YAML fixture test:** Copy the exact content of `deploy/dapr/resiliency.yaml` into a test constant or embedded resource. Parse it and assert: exactly 3 retry policies, 3 timeout policies, 2 circuit breakers, and 4 target bindings. Verify specific policy names and values match the production YAML. This is the regression anchor — if the parser breaks on real-world YAML, this test catches it. Do NOT use synthetic YAML for this test.
  - **Happy path:** Full resiliency YAML (matching `deploy/dapr/resiliency.yaml` structure) → correctly parsed policies and targets
  - **Config path is null:** → `DaprResiliencySpec.Unavailable`
  - **File not found:** → `DaprResiliencySpec.NotFound` with path in error message
  - **Config path points to a directory (not a file):** `File.ReadAllTextAsync` on a directory throws `UnauthorizedAccessException` on Windows. The service must catch this and return `DaprResiliencySpec.ReadError`. Add a test using a temp directory path.
  - **File exceeds 1MB size limit:** Create a temp file larger than 1MB → `DaprResiliencySpec.ReadError` with size in error message. Verify the file content is NOT read into memory.
  - **Invalid YAML:** → `DaprResiliencySpec.ParseError` with raw YAML content preserved
  - **Empty YAML file:** → graceful handling (empty policy lists, `IsConfigurationAvailable = true`)
  - **YAML with only retries (no timeouts/breakers):** → partial spec with empty lists for missing sections
  - **Mixed timeout forms:** Both simple string (`pubsubTimeout: 10s`) and nested object (`daprSidecar: { general: 5s }`) parsed correctly
  - **CRITICAL — Target flattening tests:** The directional vs non-directional target binding logic is non-trivial and deserves dedicated, focused test methods (not buried in general service tests):
    - **Component targets with direction:** pubsub outbound/inbound → exactly TWO `DaprResiliencyTargetBinding` entries, each with correct Direction, policy names, and TargetType="Component"
    - **Component targets without direction:** statestore → exactly ONE `DaprResiliencyTargetBinding` entry with Direction=null
    - **App targets:** commandapi → exactly ONE entry with TargetType="App" and Direction=null
    - **Verify the exact 4-row output** from the production YAML matches the expected table in the Model Definitions section
  - **Cancellation token propagation:** CancellationToken respected during file read
  - **For service tests:** Use actual YAML strings (not files) by extracting the parsing logic into a testable private/internal method, or use a temp file in the test. The dev agent should choose the approach that keeps tests fast and isolated. For the production YAML fixture test, embed the real YAML content as a string constant.

- **Controller tests:** Mock `IDaprInfrastructureQueryService` and `ILogger<T>`. Test: 200 OK with full spec, 200 OK with unavailable config (not an error), 500 on unexpected exception.

- **UI page tests:** Follow patterns from story 19-1's `DaprComponentsPageTests.cs`. Test: empty state when config unavailable, error banner when parse error, stat card rendering, YAML source viewer content, manual reload button behavior.

### Previous Story Intelligence (Stories 19-1, 19-2, 19-3)

Key learnings from preceding stories:

- **Story 19-1 (`DaprComponents.razor`):** Established the `DaprInfrastructureQueryService` metadata retrieval, `AdminDaprController` REST pattern, `FluentDataGrid` component grid, `SkeletonCard` → loaded content transition, and `EmptyState` patterns. **Follow page structure and shared component usage exactly.**
- **Story 19-2 (`DaprActors.razor`):** Established sub-page navigation from `DaprComponents.razor` with button links. **Follow the same button placement pattern.**
- **Story 19-3 (`DaprPubSub.razor`):** Established the third DAPR sub-page pattern, subscription grid, and collapsible reference cards. **The collapsible FluentCard pattern for the YAML source viewer should follow the same expand/collapse approach as story 19-3's topology card.**
- **Story 19-1 code review fix (P-9):** All async methods must handle exceptions gracefully — log and return error state, don't crash.
- **Story 19-1 code review fix (P-1):** Pass cancellation token through to all async calls.
- **Interface extension impact:** Adding methods to `IDaprInfrastructureQueryService` does not break existing NSubstitute mocks (unconfigured methods return default values), but verify all existing tests pass after the extension.
- **Button placement on DaprComponents.razor:** Stories 19-2 and 19-3 added buttons. Add "Resiliency Policies" adjacent to them (same row/section). Check current layout to ensure buttons don't overflow — if 3+ buttons in a row looks crowded, consider wrapping in a `FluentStack` with `Orientation.Horizontal` and `Wrap="true"`.

### Git Intelligence

Recent commits established:
- `5c2b209`: Story 19-2 merged — DAPR actor inspector
- `9953bbb`: `feat: add DAPR actor inspector with type registry, runtime config, and state viewer (story 19-2)`
- `308690e`: Story 19-1 merged — DAPR component status dashboard foundation
- `Admin.Abstractions/Models/Dapr/` subfolder for DAPR-specific models
- `IDaprInfrastructureQueryService` with `GetComponentsAsync`, `GetSidecarInfoAsync`, `GetActorRuntimeInfoAsync`, `GetActorInstanceStateAsync`
- `AdminDaprController` REST controller at `api/v1/admin/dapr`
- `AdminServerOptions` with `EventStoreDaprHttpEndpoint` for cross-sidecar metadata
- NSubstitute mock patterns for service and controller tests

### Technical Context: DAPR Resiliency YAML Structure

**Production config (`deploy/dapr/resiliency.yaml`):**
- `defaultRetry`: exponential, maxInterval=15s, maxRetries=10
- `pubsubRetryOutbound`: exponential, maxInterval=10s, maxRetries=5
- `pubsubRetryInbound`: exponential, maxInterval=60s, maxRetries=20
- `daprSidecar` timeout: 5s (general)
- `pubsubTimeout`: 10s
- `subscriberTimeout`: 30s
- `defaultBreaker`: maxRequests=1, interval=60s, timeout=60s, trip=consecutiveFailures>5
- `pubsubBreaker`: maxRequests=1, interval=60s, timeout=60s, trip=consecutiveFailures>10
- App targets: commandapi (default policies)
- Component targets: pubsub (directional: outbound + inbound), statestore (non-directional)

**Development config (`src/Hexalith.EventStore.AppHost/DaprComponents/resiliency.yaml`):**
- Same structure with more conservative thresholds (lower retries, shorter intervals)
- `defaultRetry`: constant (not exponential), duration=1s, maxRetries=3
- Circuit breaker trips at 3 consecutive failures (vs 5 in production)

**IMPORTANT:** The YAML comments contain valuable context (e.g., effective retry counts per backend, design rationale). These are NOT parsed by YamlDotNet's default deserializer but are visible in the raw YAML source viewer (AC5). Do NOT attempt to parse or display comments — the raw viewer covers this need.

### References

- [Source: deploy/dapr/resiliency.yaml] — Production resiliency YAML to parse
- [Source: src/Hexalith.EventStore.AppHost/DaprComponents/resiliency.yaml] — Development resiliency YAML
- [Source: src/Hexalith.EventStore.Admin.Abstractions/Services/IDaprInfrastructureQueryService.cs] — Service interface to extend
- [Source: src/Hexalith.EventStore.Admin.Server/Services/DaprInfrastructureQueryService.cs] — Service implementation to extend
- [Source: src/Hexalith.EventStore.Admin.Server/Controllers/AdminDaprController.cs] — Controller to extend
- [Source: src/Hexalith.EventStore.Admin.Server/Configuration/AdminServerOptions.cs] — Options class to extend with ResiliencyConfigPath
- [Source: src/Hexalith.EventStore.Admin.UI/Pages/DaprComponents.razor] — Sister page pattern and button integration point (story 19-1)
- [Source: src/Hexalith.EventStore.Admin.UI/Pages/DaprActors.razor] — Sister page pattern (story 19-2)
- [Source: src/Hexalith.EventStore.Admin.UI/Services/AdminDaprApiClient.cs] — API client pattern to follow
- [Source: src/Hexalith.EventStore.Admin.UI/Services/AdminActorApiClient.cs] — API client pattern to follow
- [Source: src/Hexalith.EventStore.Admin.UI/Components/Shared/StatCard.razor] — Metrics card component
- [Source: src/Hexalith.EventStore.Admin.UI/Components/Shared/IssueBanner.razor] — Error/warning banner
- [Source: src/Hexalith.EventStore.Admin.UI/Components/Shared/EmptyState.razor] — Empty state component
- [Source: src/Hexalith.EventStore.Admin.UI/Components/Shared/SkeletonCard.razor] — Loading state component
- [Source: Directory.Packages.props] — YamlDotNet 16.3.0 already available
- [Source: _bmad-output/implementation-artifacts/19-1-dapr-component-status-dashboard.md] — Previous story reference
- [Source: _bmad-output/implementation-artifacts/19-2-dapr-actor-inspector.md] — Previous story reference
- [Source: _bmad-output/implementation-artifacts/19-3-dapr-pubsub-delivery-metrics.md] — Previous story reference
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 19] — Epic definition (FR75 DAPR portion)
- [Source: _bmad-output/planning-artifacts/architecture.md] — Resiliency as DAPR building block dependency, rule 4 (no custom retry logic), rule 14 (5s sidecar timeout)

## Dev Agent Record

### Agent Model Used

(to be filled by dev agent)

### Debug Log References

### Completion Notes List

### File List
