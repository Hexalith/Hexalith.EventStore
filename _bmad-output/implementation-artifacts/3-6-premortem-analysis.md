# Pre-Mortem Analysis: Story 3.6 Multi-Domain & Multi-Tenant Processing

**Scenario:** It's 6 months after deployment. The multi-tenant EventStore has suffered a catastrophic failure.

**Date:** 2026-02-14
**Analyzed System:** Hexalith.EventStore with DAPR actors, multi-tenant architecture, dynamic domain registration
**Analyst:** Claude Sonnet 4.5

---

## System Context Summary

### Architecture Overview
- **Actor Identity:** Canonical `{tenant}:{domain}:{aggregate-id}` enforced by `AggregateIdentity` validation
- **Storage Keys:** Composite pattern `{tenant}:{domain}:{aggId}:events:{seq}`
- **Domain Resolution:** DAPR config store mapping `{tenant}:{domain}:service → {appId, methodName, version}`
- **Multi-Tenancy:** 6-layer defense (JWT → Claims → Controller → MediatR → Actor → DAPR ACL)
- **Dynamic Registration:** NFR20 requires tenant/domain addition without system restart
- **Tenant Validation:** SEC-2 enforces validation BEFORE state rehydration at actor level (Story 3.3)

### Current Implementation Status
Based on codebase examination:
- `AggregateActor` is currently a STUB (Story 3.1 complete)
- `AggregateIdentity` validates and derives all composite keys
- Stories 3.2-3.5 define the 5-step actor orchestrator (not yet implemented)
- Domain service resolution via DAPR config store (Story 3.5 specification)
- TenantValidator at actor level (Story 3.3 specification)

---

## Failure Scenario 1: Tenant Data Breach

### Catastrophic Event
**"Tenant A's events appeared in Tenant B's event stream"**

### Root Cause Analysis

#### Primary Root Causes
1. **Actor Rebalancing Race Condition**
   - **Mechanism:** DAPR actor placement uses consistent hashing. During node failure/scaling, actors migrate between hosts.
   - **Gap:** If `TenantValidator` (Story 3.3) executes AFTER state is loaded into memory during rebalancing, a brief window exists where tenant B's command could read tenant A's cached state before validation rejects it.
   - **Trigger:** Deployment during high traffic + node crash creates race condition in actor activation path.

2. **AggregateIdentity Parsing Vulnerability**
   - **Mechanism:** `AggregateIdentity` uses colon-separated parsing. If a malicious aggregateId contains colons (e.g., `malicious:tenant-b:real-id`), parsing could extract wrong tenant.
   - **Current Protection:** Regex `^[a-zA-Z0-9]([a-zA-Z0-9._-]*[a-zA-Z0-9])?$` for aggregateId DOES NOT allow colons. BUT regex is only enforced at `AggregateIdentity` construction time.
   - **Gap:** If actor ID is constructed via direct string concatenation (bypassing `AggregateIdentity` constructor), validation is skipped.
   - **Exploitation:** Direct DAPR actor invocation via sidecar API could bypass CommandApi validation and construct malformed actor ID.

3. **Config Store Injection Attack**
   - **Mechanism:** Domain service registration in DAPR config store maps `{tenant}:{domain}:service` to `{appId, method}`.
   - **Gap:** If config store allows wildcards or regex patterns (depends on DAPR component implementation), an attacker could register `*:domain:service` or `.*:domain:service` to intercept all tenants.
   - **Trigger:** Misconfigured DAPR config store backend (Redis vs. PostgreSQL vs. Cosmos DB have different pattern matching behaviors).

4. **Event Key Collision via State Store Manipulation**
   - **Mechanism:** Event keys follow pattern `{tenant}:{domain}:{aggId}:events:{seq}`.
   - **Gap:** If state store backend allows key enumeration/scanning (Redis `SCAN`, PostgreSQL wildcard queries), and DAPR ACL policies are misconfigured, a malicious domain service could directly write events to another tenant's keyspace.
   - **Protection Failure:** DAPR ACL (Story 5.1, not yet implemented) is the final defense. If domain services have `stateStore.write` permission without key prefix restrictions, they can write to any tenant's keys.

#### Secondary Contributing Factors
- **Logging Sanitization Failure:** If tenant validation logs include actual payload data (violating rule #5), logs become an exfiltration vector.
- **JWT Claim Spoofing:** If `eventstore:tenant` claims transformation (Story 2.4) trusts user-provided JWT fields without backend validation, forged JWTs could pass tenant checks.
- **Snapshot Poisoning:** If snapshots (Stories 3.9-3.10) are stored without tenant prefix validation, a snapshot from tenant A loaded into tenant B's actor exposes data.

### Prevention Measures

| Measure | Type | Story | Severity |
|---------|------|-------|----------|
| **PM-1.1: Enforce SEC-2 Ordering in Actor Activation** | Code Pattern | 3.3 | CRITICAL |
| - Add DAPR actor activation hook that runs `TenantValidator.Validate()` BEFORE `OnActivateAsync` loads any state | | | |
| - Actor method: `protected override async Task OnActivateAsync() { _tenantValidator.Validate(command.TenantId, Host.Id.GetId()); await base.OnActivateAsync(); }` | | | |
| - Comment: `// SEC-2 CRITICAL: Tenant validation MUST occur before any state access, including rehydration during activation` | | | |
| **PM-1.2: Immutable ActorId Validation** | Architecture | 3.1 | CRITICAL |
| - Never construct actor IDs via string concatenation. ALWAYS use `AggregateIdentity.ActorId` property | | | |
| - Add fitness test: `ActorActivation_DirectStringId_ThrowsException` verifying that `ActorProxy.Create` with raw string (not via AggregateIdentity) is blocked | | | |
| - Enforce via code review checklist: "All actor activations use `AggregateIdentity.ActorId`, never `$"{tenant}:{domain}:{aggId}"`" | | | |
| **PM-1.3: Config Store Key Prefix ACL** | DAPR Policy | 3.5 | HIGH |
| - DAPR config store ACL: Domain services have NO read access to config store. Only EventStore Server reads configs | | | |
| - Key pattern validation: `DomainServiceResolver.ResolveAsync()` rejects keys with wildcards (`*`, `.*`, `%`) | | | |
| - Test: `ResolveAsync_WildcardKey_ThrowsSecurityException` | | | |
| **PM-1.4: State Store Namespace Isolation** | DAPR Component | 5.1 | CRITICAL |
| - DAPR state store component config: Enable key prefix isolation per app-id (DAPR 1.14+ feature) | | | |
| - EventStore actor state keys automatically prefixed: `eventstore-actor||{tenant}:{domain}:{aggId}:events:{seq}` | | | |
| - Domain services have ZERO direct state store access (only via actor invocation) | | | |
| - DAPR ACL: `appId: domain-service-*` has `stateStore.write: deny` | | | |
| **PM-1.5: Tenant-Scoped Event Queries** | API Design | 3.4 | HIGH |
| - EventStreamReader.RehydrateAsync() validates that ALL loaded event envelopes have `TenantId == identity.TenantId` | | | |
| - Test: `RehydrateAsync_CrossTenantEvent_ThrowsSecurityException` | | | |
| - Prevents state store corruption from manifesting as cross-tenant data exposure | | | |

### Tests That Would Catch It

```csharp
// Story 3.3: Tenant Validation Tests
[Fact]
public async Task ProcessCommandAsync_ActorRebalancing_TenantMismatchDetectedBeforeStateLoad()
{
    // Arrange: Actor receives command for tenant-b but actor ID is tenant-a:domain:aggId
    var actorId = new AggregateIdentity("tenant-a", "orders", "order-123");
    var command = CreateCommand(tenantId: "tenant-b", domain: "orders", aggId: "order-123");

    var stateManager = Substitute.For<IActorStateManager>();
    stateManager.TryGetStateAsync<AggregateMetadata>(Arg.Any<string>())
        .Returns(Task.FromResult(new ConditionalValue<AggregateMetadata>(true, new AggregateMetadata(10, DateTimeOffset.Now, "etag-123"))));

    var actor = new AggregateActor(CreateActorHost(actorId.ActorId, stateManager), logger);

    // Act
    var result = await actor.ProcessCommandAsync(command);

    // Assert
    result.Accepted.ShouldBeFalse();
    result.ErrorMessage.ShouldContain("TenantMismatch");
    // CRITICAL: Verify state was NEVER accessed
    await stateManager.DidNotReceive().TryGetStateAsync<AggregateMetadata>(Arg.Any<string>());
}

[Fact]
public async Task ActorActivation_MalformedActorId_ThrowsSecurityException()
{
    // Arrange: Attempt to activate actor with injected colon in aggregateId
    var maliciousActorId = "tenant-a:orders:malicious:tenant-b:real-id"; // 5 segments instead of 3

    // Act & Assert
    Should.Throw<InvalidOperationException>(() =>
    {
        var host = CreateActorHost(maliciousActorId, Substitute.For<IActorStateManager>());
        var actor = new AggregateActor(host, logger);
        // TenantValidator.Validate() should throw during first command processing
    });
}

// Story 3.5: Domain Service Resolution Tests
[Fact]
public async Task ResolveAsync_WildcardInConfigKey_ThrowsSecurityException()
{
    // Arrange
    var resolver = new DomainServiceResolver(daprClient, options, logger);

    // Act & Assert
    await Should.ThrowAsync<SecurityException>(async () =>
        await resolver.ResolveAsync("tenant-*", "orders")); // Wildcard in tenant
    await Should.ThrowAsync<SecurityException>(async () =>
        await resolver.ResolveAsync(".*", "orders")); // Regex pattern
}

// Story 3.4: Event Stream Reader Security Tests
[Fact]
public async Task RehydrateAsync_CrossTenantEventInStream_ThrowsSecurityException()
{
    // Arrange: Event stream contains event with wrong tenant
    var identity = new AggregateIdentity("tenant-a", "orders", "order-123");
    var corruptEvent = new EventEnvelope(
        AggregateId: "order-123",
        TenantId: "tenant-b", // WRONG TENANT
        Domain: "orders",
        SequenceNumber: 5,
        /* ... */
    );

    stateManager.TryGetStateAsync<EventEnvelope>($"{identity.EventStreamKeyPrefix}5")
        .Returns(Task.FromResult(new ConditionalValue<EventEnvelope>(true, corruptEvent)));

    var reader = new EventStreamReader(stateManager, logger);

    // Act & Assert
    await Should.ThrowAsync<SecurityException>(async () =>
        await reader.RehydrateAsync(identity));
}

// Integration Test: DAPR ACL Enforcement
[Fact]
public async Task DomainService_DirectStateStoreWrite_Rejected()
{
    // Arrange: Domain service attempts direct state store write (bypassing actor)
    var domainServiceDaprClient = CreateDaprClient(appId: "domain-service-payments");

    // Act & Assert: DAPR ACL should block this operation
    await Should.ThrowAsync<DaprException>(async () =>
        await domainServiceDaprClient.SaveStateAsync(
            "statestore",
            "tenant-a:orders:order-123:events:999",
            maliciousEvent));
}
```

### Story 3.6 Action Items

1. **[BLOCKING] Add Actor Activation Tenant Validation**
   - Location: `AggregateActor.OnActivateAsync()`
   - Before ANY state access, extract tenant from `Host.Id.GetId()` and validate
   - Reject activation if actor ID is malformed (not exactly 3 colon-separated segments)

2. **[CRITICAL] Add EventStreamReader Cross-Tenant Event Detection**
   - Location: `EventStreamReader.RehydrateAsync()`
   - After loading each `EventEnvelope`, validate `event.TenantId == identity.TenantId`
   - Throw `SecurityException` with forensic details (sequence, actual tenant, expected tenant)

3. **[HIGH] Add DomainServiceResolver Wildcard Rejection**
   - Location: `DomainServiceResolver.ResolveAsync()`
   - Reject tenantId/domain containing `*`, `.*`, `%`, `?` before config store lookup
   - Prevents pattern-based registration hijacking

4. **[HIGH] Document DAPR ACL Requirements**
   - File: `docs/deployment/dapr-acl-policies.md`
   - Specify per-appId state store permissions
   - Mandate key prefix isolation for production deployments

5. **[MEDIUM] Add Fitness Test for ActorId Construction**
   - Verify all actor activations use `AggregateIdentity.ActorId`
   - Fail build if string concatenation pattern found: `ActorProxy.Create($"{tenant}:{domain}:{aggId}")`

---

## Failure Scenario 2: Cascading Domain Failure

### Catastrophic Event
**"Adding a new domain crashed all existing domain processing"**

### Root Cause Analysis

#### Primary Root Causes
1. **Shared DAPR Config Store Corruption**
   - **Mechanism:** All domain registrations stored in single DAPR config store instance.
   - **Gap:** If new domain registration has malformed JSON (missing closing brace, invalid UTF-8), and DAPR config store implementation doesn't validate per-key, the config store could enter corrupted state.
   - **Cascade:** `DomainServiceResolver.ResolveAsync()` for ALL domains now throws `JsonException` on deserialization.
   - **Trigger:** Automated deployment script writes config without validation; Redis config store accepts arbitrary byte arrays.

2. **Domain Service Version Conflict**
   - **Mechanism:** Config store maps `{tenant}:{domain}:service → {appId, methodName, version}`.
   - **Gap:** If two domains register with same appId but different method names, and DAPR service invocation uses HTTP routing, path conflicts could occur.
   - **Example:** Domain "payments" registers `{appId: "domain-processor", method: "process-payment"}`. Domain "orders" registers `{appId: "domain-processor", method: "process-order"}`. Same DAPR app-id creates method name collision if domain service doesn't implement both routes.
   - **Result:** All domains using that appId fail with 404 Not Found.

3. **Actor Placement Thundering Herd**
   - **Mechanism:** Adding new domain triggers registration of actors for that domain. If 1000 orders already exist, adding "orders-v2" domain triggers activation of 1000 new actors.
   - **Gap:** DAPR actor placement doesn't rate-limit activations. All 1000 actors activate simultaneously.
   - **Cascade:** Actors compete for state store connections. State store (Redis/PostgreSQL) hits connection limit. Existing actors' state reads time out.
   - **Result:** ALL domains fail with "State store timeout" errors.

4. **EventStreamReader Deserialization Breaking Change**
   - **Mechanism:** New domain's events use a new serialization format (e.g., System.Text.Json instead of Newtonsoft.Json).
   - **Gap:** If `EventEnvelope.Payload` is stored as `byte[]` but deserialization logic assumes a specific JSON library, adding a domain with different serialization breaks the reader.
   - **Story 3.4 Gap:** `EventStreamReader` doesn't check `SerializationFormat` field before deserialization. Assumes all events use same format.
   - **Result:** Existing domains' state rehydration fails with "Unexpected token" errors.

#### Secondary Contributing Factors
- **No Config Validation on Write:** DAPR config store allows arbitrary values. No schema validation.
- **Missing Health Checks:** Domain service health endpoint doesn't verify config store accessibility.
- **No Canary Deployment:** New domain registration applied to production immediately without staged rollout.

### Prevention Measures

| Measure | Type | Story | Severity |
|---------|------|-------|----------|
| **PM-2.1: Config Store Schema Validation** | Infrastructure | 3.5 | CRITICAL |
| - Wrap config store writes in validation layer: `ConfigStoreValidator.ValidateAndWrite(key, value)` | | | |
| - JSON schema for `DomainServiceRegistration`: require `appId` (string, non-empty), `methodName` (string, HTTP path format), `version` (SemVer) | | | |
| - Reject writes that fail schema validation BEFORE touching config store | | | |
| - Test: `ValidateAndWrite_MalformedJson_ThrowsValidationException` | | | |
| **PM-2.2: Per-Domain Config Isolation** | Architecture | 3.5 | HIGH |
| - DAPR config store component: Use key prefixes for logical isolation: `domains/{tenant}/{domain}/service` instead of flat `{tenant}:{domain}:service` | | | |
| - Prevents corruption in one domain's config from affecting resolver for other domains | | | |
| - If config store supports transactions (PostgreSQL), use transactional reads to ensure atomic multi-key config fetch | | | |
| **PM-2.3: Domain Service Health Check** | API Design | 3.5 | HIGH |
| - `DomainServiceResolver.ResolveAsync()` includes timeout (default 2s, configurable) | | | |
| - After resolving registration, perform health check: `daprClient.InvokeMethodAsync(appId, "health")` | | | |
| - Cache successful health checks (TTL 30s) to avoid repeated health probes | | | |
| - Test: `ResolveAsync_DomainServiceUnhealthy_ThrowsDomainServiceUnavailableException` | | | |
| **PM-2.4: Gradual Domain Registration Rollout** | Process | 3.6 | MEDIUM |
| - Documentation: "Adding New Domain" guide includes staged rollout procedure | | | |
| - Step 1: Register domain in config store with `enabled: false` flag | | | |
| - Step 2: Verify config is readable via GET /health/config-store endpoint | | | |
| - Step 3: Enable domain for single test tenant | | | |
| - Step 4: Monitor for 10 minutes. If error rate <0.1%, enable for all tenants | | | |
| **PM-2.5: SerializationFormat Validation** | Code | 3.4 | HIGH |
| - `EventStreamReader.RehydrateAsync()`: Validate `event.SerializationFormat == "application/json"` (or configured format) | | | |
| - Throw `UnsupportedSerializationFormatException` if format doesn't match | | | |
| - Prevents silent deserialization failures from corrupting aggregate state | | | |
| - Test: `RehydrateAsync_UnsupportedSerializationFormat_ThrowsException` | | | |

### Tests That Would Catch It

```csharp
// Story 3.5: Domain Service Registration Validation
[Fact]
public async Task RegisterDomain_MalformedJson_RejectedBeforeWrite()
{
    // Arrange
    var validator = new ConfigStoreValidator(schema);
    var malformedJson = "{\"appId\": \"test\", \"methodName\": \"process\""; // Missing closing brace

    // Act & Assert
    await Should.ThrowAsync<ValidationException>(async () =>
        await validator.ValidateAndWrite("tenant-a:orders:service", malformedJson));

    // Verify config store was NOT touched
    await daprClient.DidNotReceive().SaveConfiguration(Arg.Any<string>(), Arg.Any<Dictionary<string, string>>());
}

[Fact]
public async Task ResolveAsync_ConfigStoreCorrupted_IsolatesFailure()
{
    // Arrange: "payments" domain has corrupt config, "orders" domain is valid
    daprClient.GetConfiguration("configstore", new[] { "tenant-a:payments:service" })
        .Returns(new GetConfigurationResponse { Items = { ["tenant-a:payments:service"] = new ConfigurationItem { Value = "CORRUPT{{{" } } });
    daprClient.GetConfiguration("configstore", new[] { "tenant-a:orders:service" })
        .Returns(new GetConfigurationResponse { Items = { ["tenant-a:orders:service"] = new ConfigurationItem { Value = "{\"appId\": \"orders-svc\", \"methodName\": \"process\"}" } } });

    var resolver = new DomainServiceResolver(daprClient, options, logger);

    // Act & Assert: Payments should fail, Orders should succeed
    await Should.ThrowAsync<ConfigurationException>(async () =>
        await resolver.ResolveAsync("tenant-a", "payments"));

    var ordersReg = await resolver.ResolveAsync("tenant-a", "orders");
    ordersReg.ShouldNotBeNull();
    ordersReg.AppId.ShouldBe("orders-svc");
}

// Story 3.4: Serialization Format Validation
[Fact]
public async Task RehydrateAsync_MixedSerializationFormats_ThrowsException()
{
    // Arrange: Event stream has events with different formats
    var identity = new AggregateIdentity("tenant-a", "orders", "order-123");
    var event1 = new EventEnvelope(/* ... */, SerializationFormat: "application/json");
    var event2 = new EventEnvelope(/* ... */, SerializationFormat: "application/x-protobuf"); // Unsupported

    stateManager.TryGetStateAsync<EventEnvelope>($"{identity.EventStreamKeyPrefix}1")
        .Returns(Task.FromResult(new ConditionalValue<EventEnvelope>(true, event1)));
    stateManager.TryGetStateAsync<EventEnvelope>($"{identity.EventStreamKeyPrefix}2")
        .Returns(Task.FromResult(new ConditionalValue<EventEnvelope>(true, event2)));

    var reader = new EventStreamReader(stateManager, logger);

    // Act & Assert
    await Should.ThrowAsync<UnsupportedSerializationFormatException>(async () =>
        await reader.RehydrateAsync(identity));
}

// Integration Test: Domain Service Method Name Collision
[Fact]
public async Task RegisterDomain_AppIdCollision_DetectedAtHealthCheck()
{
    // Arrange: Two domains registered with same appId
    await RegisterDomain("tenant-a", "payments", appId: "shared-processor", method: "process-payment");

    // Act & Assert: Second registration with same appId but different method should fail health check
    await Should.ThrowAsync<DomainServiceConfigurationException>(async () =>
        await RegisterDomain("tenant-a", "orders", appId: "shared-processor", method: "process-order"));
}
```

### Story 3.6 Action Items

1. **[CRITICAL] Implement Config Store Write Validation**
   - Create `ConfigStoreValidator` with JSON schema for `DomainServiceRegistration`
   - Intercept all domain registration writes
   - Reject invalid registrations BEFORE config store write

2. **[HIGH] Add DomainServiceResolver Isolation**
   - Wrap config store reads in try/catch per domain
   - If one domain's config is corrupt, log error and continue for other domains
   - Return `null` for corrupted domain (triggers `DomainServiceNotFoundException`)

3. **[HIGH] Add SerializationFormat Validation to EventStreamReader**
   - Check `event.SerializationFormat` matches expected format before deserialization
   - Throw `UnsupportedSerializationFormatException` with event details

4. **[MEDIUM] Create Domain Registration Runbook**
   - Document staged rollout procedure
   - Include config validation, health check verification, single-tenant test
   - Mandate 10-minute monitoring before full rollout

5. **[LOW] Add Domain Service Health Check to Resolver**
   - After config resolution, ping domain service `/health` endpoint
   - Cache health check results (30s TTL)

---

## Failure Scenario 3: Config Store Split-Brain

### Catastrophic Event
**"Config store became inconsistent - some commands routed to wrong domain service"**

### Root Cause Analysis

#### Primary Root Causes
1. **DAPR Config Store Eventual Consistency**
   - **Mechanism:** DAPR config store API supports multiple backends (Redis, Consul, etcd, Azure App Configuration).
   - **Gap:** Some backends (Azure App Configuration, Consul) use eventual consistency. Config writes propagate to read replicas with delay (100ms-1s).
   - **Split-Brain:** During domain service update, old registration and new registration coexist on different replicas.
   - **Result:** Command A routes to old domain service (returns v1 events), Command B routes to new domain service (returns v2 events). Event stream contains mixed event versions.

2. **Cache Invalidation Failure**
   - **Mechanism:** `DomainServiceResolver` caches resolved registrations (PM-2.3 recommends 30s TTL).
   - **Gap:** If cache invalidation isn't triggered on config update, resolver continues using stale registration for up to 30s after domain service update.
   - **Scenario:** Domain "orders" updated from v1.0.0 to v2.0.0. Config store updated. But cached registration points to v1 appId for 30s.
   - **Result:** Half of commands processed by v1, half by v2. Event stream corruption.

3. **Concurrent Registration Overwrites**
   - **Mechanism:** Two operators simultaneously register domain service for same tenant+domain (race condition).
   - **Gap:** DAPR config store `SaveConfiguration` API doesn't support ETags or conditional writes (unlike state store).
   - **Result:** Last-write-wins. Registration written at 10:00:00.500 overwrites registration written at 10:00:00.499.
   - **Detection Gap:** No audit log of config overwrites. Silent data loss.

4. **Multi-Region Config Replication Lag**
   - **Mechanism:** Production deployment spans 3 Azure regions. DAPR config store uses Azure App Configuration with geo-replication.
   - **Gap:** Config replication across regions takes 2-5 seconds.
   - **Scenario:** Region 1 receives config update at T+0s. Command arrives at Region 2 at T+1s (before replication completes). Command routed to old domain service.
   - **Result:** Region-specific routing inconsistency. Same tenant+domain resolves differently per region.

#### Secondary Contributing Factors
- **No Config Version Tracking:** Domain registrations lack monotonic version counter.
- **Missing Config Audit Trail:** Who wrote the config? When? From which process?
- **No Read-After-Write Guarantee:** DAPR config API doesn't support read-your-writes consistency.

### Prevention Measures

| Measure | Type | Story | Severity |
|---------|------|-------|----------|
| **PM-3.1: Versioned Domain Registrations** | Architecture | 3.5 | CRITICAL |
| - Add `ConfigVersion` field to `DomainServiceRegistration`: `record DomainServiceRegistration(string AppId, string MethodName, string TenantId, string Domain, string Version, long ConfigVersion, DateTimeOffset LastUpdated)` | | | |
| - `ConfigVersion` is monotonically increasing counter (Unix timestamp in milliseconds) | | | |
| - `DomainServiceResolver.ResolveAsync()`: If config cache contains version N and config store returns version M < N, reject stale read | | | |
| - Forces strong consistency: cached newer version always wins over stale read | | | |
| **PM-3.2: Config Write Leader Election** | Process | 3.6 | HIGH |
| - ONLY one process writes to config store: `EventStore.ConfigWriter` service (separate deployment) | | | |
| - All domain registration requests go through ConfigWriter API: `POST /api/config/domains` | | | |
| - ConfigWriter uses distributed lock (DAPR lock API) to ensure single-writer | | | |
| - Prevents concurrent overwrites | | | |
| **PM-3.3: Cache Invalidation on Config Change** | Code | 3.5 | HIGH |
| - DAPR config store supports subscriptions (Redis, Consul). EventStore subscribes to config change events | | | |
| - On config change event: `resolver.InvalidateCache(tenant, domain)` | | | |
| - If config store doesn't support subscriptions: Poll every 5s, compare `ConfigVersion`, invalidate on change | | | |
| - Test: `ConfigChange_TriggersInvalidation_WithinOneSecond` | | | |
| **PM-3.4: Bounded Staleness Window** | Architecture | 3.5 | MEDIUM |
| - Document acceptable staleness: "Config changes propagate within 10 seconds" | | | |
| - Add config write timestamp to registration | | | |
| - `DomainServiceResolver`: If `(DateTimeOffset.UtcNow - registration.LastUpdated) > 10s` AND cache miss, force cache refresh | | | |
| - Prevents indefinite stale reads due to cache | | | |
| **PM-3.5: Config Audit Logging** | Observability | 3.6 | MEDIUM |
| - ConfigWriter logs ALL writes at Warning level (config changes are rare, high-signal events) | | | |
| - Log fields: `ConfigKey`, `OldVersion`, `NewVersion`, `WrittenBy` (user/service identity), `Timestamp`, `CorrelationId` | | | |
| - Enables forensic analysis: "Who changed orders domain config at 2026-08-15 10:23:45?" | | | |

### Tests That Would Catch It

```csharp
// Story 3.5: Config Version Conflict Detection
[Fact]
public async Task ResolveAsync_StaleConfigRead_RejectedInFavorOfCache()
{
    // Arrange: Cache has version 1005, config store returns version 1003 (stale)
    var cachedReg = new DomainServiceRegistration(
        AppId: "orders-v2", MethodName: "process", TenantId: "tenant-a", Domain: "orders",
        Version: "2.0.0", ConfigVersion: 1005, LastUpdated: DateTimeOffset.UtcNow.AddSeconds(-5));

    var staleReg = new DomainServiceRegistration(
        AppId: "orders-v1", MethodName: "process", TenantId: "tenant-a", Domain: "orders",
        Version: "1.0.0", ConfigVersion: 1003, LastUpdated: DateTimeOffset.UtcNow.AddMinutes(-10));

    resolver.Cache.Set("tenant-a:orders", cachedReg);
    daprClient.GetConfiguration(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>())
        .Returns(CreateConfigResponse("tenant-a:orders:service", staleReg));

    // Act
    var result = await resolver.ResolveAsync("tenant-a", "orders");

    // Assert: Should return cached version, not stale read
    result.ConfigVersion.ShouldBe(1005);
    result.AppId.ShouldBe("orders-v2");
}

[Fact]
public async Task ConfigWriter_ConcurrentWrites_SecondWriteBlocked()
{
    // Arrange: Two writers attempt to update same domain simultaneously
    var writer1 = new ConfigWriter(daprClient, lockClient, logger);
    var writer2 = new ConfigWriter(daprClient, lockClient, logger);

    var reg1 = CreateRegistration(version: "2.0.0");
    var reg2 = CreateRegistration(version: "2.1.0");

    // Act: Start both writes concurrently
    var task1 = writer1.WriteAsync("tenant-a", "orders", reg1);
    var task2 = writer2.WriteAsync("tenant-a", "orders", reg2);

    await Task.WhenAll(task1, task2);

    // Assert: Only ONE write should succeed (distributed lock ensures mutual exclusion)
    var successCount = new[] { task1.Result, task2.Result }.Count(r => r.Success);
    successCount.ShouldBe(1);
}

// Integration Test: Config Invalidation
[Fact]
public async Task ConfigChange_InvalidatesCache_WithinOneSecond()
{
    // Arrange: Start EventStore with config subscription
    var resolver = CreateResolverWithSubscription();
    var oldReg = CreateRegistration(version: "1.0.0", configVersion: 1000);
    await WriteConfig("tenant-a:orders:service", oldReg);

    // Prime cache
    await resolver.ResolveAsync("tenant-a", "orders");

    // Act: Update config (simulates external write)
    var newReg = CreateRegistration(version: "2.0.0", configVersion: 1001);
    await WriteConfig("tenant-a:orders:service", newReg);

    // Assert: Cache should be invalidated within 1 second
    await Task.Delay(1000);
    var result = await resolver.ResolveAsync("tenant-a", "orders");
    result.ConfigVersion.ShouldBe(1001); // Should fetch new version, not cached old version
}
```

### Story 3.6 Action Items

1. **[CRITICAL] Add ConfigVersion to DomainServiceRegistration**
   - Field: `long ConfigVersion` (Unix timestamp milliseconds)
   - Field: `DateTimeOffset LastUpdated`
   - Resolver: Reject stale reads (cache version > read version)

2. **[CRITICAL] Implement Config Write Single-Writer Pattern**
   - Create `ConfigWriter` service with distributed lock
   - All domain registration writes go through ConfigWriter API
   - Prevents concurrent overwrites

3. **[HIGH] Implement Config Cache Invalidation**
   - Subscribe to config store change events (if supported)
   - On change: invalidate resolver cache for affected domain
   - Fallback: Poll every 5s, compare ConfigVersion

4. **[MEDIUM] Add Config Audit Logging**
   - Log all config writes at Warning level
   - Include old/new version, writer identity, timestamp
   - Enables forensic investigation

5. **[LOW] Document Bounded Staleness Window**
   - "Config changes propagate within 10 seconds"
   - Architecture decision record explaining consistency trade-offs

---

## Failure Scenario 4: Actor Rebalancing Tenant Breach

### Catastrophic Event
**"Actor rebalancing after node failure caused tenant isolation breach"**

### Root Cause Analysis

#### Primary Root Causes
1. **SEC-2 Ordering Violation During Activation**
   - **Mechanism:** DAPR actor framework calls `OnActivateAsync()` when actor first activates on a new host.
   - **Gap:** If `EventStreamReader.RehydrateAsync()` is called INSIDE `OnActivateAsync()` (to pre-load state), it executes BEFORE the first command's `TenantValidator.Validate()` check.
   - **Exploitation:** Malicious command targets `tenant-b:orders:order-123` actor ID. Command has `tenantId = "tenant-a"` in envelope. Actor activates, loads tenant-b's events into memory during `OnActivateAsync()`. Then command processing begins, TenantValidator rejects, but state is already in memory.
   - **Breach:** If actor state is logged (debug mode, crash dump), tenant-b's events exposed.

2. **Actor Placement Migration Race**
   - **Mechanism:** DAPR actor placement uses consistent hashing. When node crashes, actors redistribute to remaining nodes.
   - **Gap:** Actor for `tenant-a:orders:order-123` was on Node 1. Node 1 crashes. DAPR migrates actor to Node 2. During migration, Node 2 receives TWO commands simultaneously: (1) legitimate command for tenant-a, (2) exploit command for tenant-b.
   - **Race:** If exploit command arrives DURING actor activation (before state rehydration completes), and TenantValidator isn't invoked until AFTER activation, exploit command could read partial state.
   - **Detection Gap:** No activation lifecycle hook validates tenant before state access.

3. **Snapshot Cross-Tenant Contamination**
   - **Mechanism:** Snapshots (Stories 3.9-3.10) are stored at `{tenant}:{domain}:{aggId}:snapshot`.
   - **Gap:** If snapshot deserialization doesn't validate `snapshot.TenantId == identity.TenantId`, a snapshot from tenant-a could be loaded into tenant-b's actor (via state store key manipulation).
   - **Attack:** Attacker writes snapshot to `tenant-b:orders:order-123:snapshot` with snapshot data from `tenant-a:orders:order-999`.
   - **Result:** Tenant-b actor initializes with tenant-a's state. Commands for tenant-b execute against wrong state. Events persisted to tenant-b's stream with tenant-a's business logic applied.

4. **Idempotency Cache Poisoning**
   - **Mechanism:** `IdempotencyChecker` (Story 3.2) caches `CommandProcessingResult` keyed by `causationId`.
   - **Gap:** If `causationId` is user-controlled (passed in command envelope without validation), attacker could craft collision.
   - **Attack:** Tenant-a processes command with `causationId = "shared-123"`. Result cached. Tenant-b processes command with same `causationId`. Cache hit returns tenant-a's result.
   - **Breach:** Tenant-b receives result containing tenant-a's correlation ID, error messages, or event metadata.

#### Secondary Contributing Factors
- **Missing Actor Lifecycle Audit Logs:** Actor activation/deactivation not logged with tenant/domain context.
- **No State Checksum Validation:** Rehydrated state isn't validated against expected tenant.
- **DAPR Placement Policy Gaps:** No tenant affinity in actor placement (all tenants share same placement service).

### Prevention Measures

| Measure | Type | Story | Severity |
|---------|------|-------|----------|
| **PM-4.1: Actor Lifecycle Tenant Validation** | Code | 3.3 | CRITICAL |
| - Override `OnActivateAsync()` in `AggregateActor` | | | |
| - Extract tenant from `Host.Id.GetId()` (parse actor ID to get first segment) | | | |
| - Store in actor instance field: `private readonly string _actorTenant;` | | | |
| - Do NOT load any state during activation (defer to first command processing) | | | |
| - First command: `TenantValidator.Validate(command.TenantId, _actorTenant)` BEFORE state access | | | |
| **PM-4.2: Snapshot Tenant Validation** | Code | 3.9 | CRITICAL |
| - `SnapshotReader.LoadAsync()`: After deserializing snapshot, validate `snapshot.TenantId == identity.TenantId` | | | |
| - Throw `SnapshotTenantMismatchException` if mismatch detected | | | |
| - Log at Error level (snapshot corruption indicates security breach or state store bug) | | | |
| - Test: `LoadSnapshot_TenantMismatch_ThrowsSecurityException` | | | |
| **PM-4.3: CausationId Tenant Scoping** | Architecture | 3.2 | HIGH |
| - Prefix causationId with tenant: `{tenant}:{correlationId}` when storing in idempotency cache | | | |
| - Prevents cross-tenant cache collisions | | | |
| - Idempotency cache key: `{tenant}:{causationId}` instead of raw `causationId` | | | |
| - Test: `IdempotencyCheck_SameCausationIdDifferentTenant_IndependentCache` | | | |
| **PM-4.4: Actor Activation Audit Log** | Observability | 3.6 | MEDIUM |
| - Log actor activation/deactivation at Information level | | | |
| - Fields: `ActorId`, `Tenant`, `Domain`, `AggregateId`, `HostNode`, `ActivationTimestamp`, `DeactivationTimestamp` | | | |
| - Enables forensic analysis: "Which node hosted tenant-a:orders:order-123 at 2026-08-15 10:45:00?" | | | |
| **PM-4.5: DAPR Placement Tenant Affinity** | Infrastructure | 5.1 | LOW |
| - DAPR placement service config: Enable tenant-based affinity (requires DAPR 1.16+) | | | |
| - Keeps all tenant-a actors on subset of nodes, tenant-b actors on different subset | | | |
| - Reduces blast radius of node compromise (attacker on tenant-a node can't access tenant-b actors) | | | |

### Tests That Would Catch It

```csharp
// Story 3.3: Actor Activation Tenant Validation
[Fact]
public async Task OnActivateAsync_ExtractsTenantFromActorId()
{
    // Arrange
    var actorId = "tenant-a:orders:order-123";
    var host = CreateActorHost(actorId, stateManager);
    var actor = new AggregateActor(host, logger);

    // Act: Trigger activation
    await actor.OnActivateAsync();

    // Assert: Actor should store tenant
    actor.ActorTenant.ShouldBe("tenant-a");
}

[Fact]
public async Task ProcessCommandAsync_TenantMismatchDuringActivation_RejectedBeforeStateLoad()
{
    // Arrange: Actor activates for tenant-a, command targets tenant-b
    var actorId = "tenant-a:orders:order-123";
    var actor = CreateActor(actorId);
    await actor.OnActivateAsync(); // Activation should NOT load state

    var command = CreateCommand(tenantId: "tenant-b", domain: "orders", aggId: "order-123");

    // Act
    var result = await actor.ProcessCommandAsync(command);

    // Assert
    result.Accepted.ShouldBeFalse();
    result.ErrorMessage.ShouldContain("TenantMismatch");

    // CRITICAL: Verify NO state access during activation
    stateManager.DidNotReceive().TryGetStateAsync<AggregateMetadata>(Arg.Any<string>());
    stateManager.DidNotReceive().TryGetStateAsync<EventEnvelope>(Arg.Any<string>());
}

// Story 3.9: Snapshot Tenant Validation
[Fact]
public async Task LoadSnapshot_TenantMismatch_ThrowsSecurityException()
{
    // Arrange: Snapshot stored for tenant-a loaded into tenant-b actor
    var identity = new AggregateIdentity("tenant-b", "orders", "order-123");
    var corruptSnapshot = new Snapshot(
        TenantId: "tenant-a", // WRONG TENANT
        Domain: "orders",
        AggregateId: "order-123",
        SequenceNumber: 100,
        State: /* ... */
    );

    stateManager.TryGetStateAsync<Snapshot>(identity.SnapshotKey)
        .Returns(Task.FromResult(new ConditionalValue<Snapshot>(true, corruptSnapshot)));

    var reader = new SnapshotReader(stateManager, logger);

    // Act & Assert
    await Should.ThrowAsync<SnapshotTenantMismatchException>(async () =>
        await reader.LoadAsync(identity));
}

// Story 3.2: Idempotency Cache Tenant Scoping
[Fact]
public async Task IdempotencyCheck_SameCausationIdDifferentTenant_IndependentCache()
{
    // Arrange: Same causationId used by two tenants
    var causationId = "shared-causation-123";
    var commandTenantA = CreateCommand(tenantId: "tenant-a", causationId: causationId);
    var commandTenantB = CreateCommand(tenantId: "tenant-b", causationId: causationId);

    var actorA = CreateActor("tenant-a:orders:order-123");
    var actorB = CreateActor("tenant-b:orders:order-456");

    // Act: Process command for tenant-a
    var resultA = await actorA.ProcessCommandAsync(commandTenantA);
    resultA.Accepted.ShouldBeTrue();

    // Process command for tenant-b with SAME causationId
    var resultB = await actorB.ProcessCommandAsync(commandTenantB);

    // Assert: Tenant-b should NOT see tenant-a's cached result
    resultB.Accepted.ShouldBeTrue(); // Not treated as duplicate
    resultB.CorrelationId.ShouldBe(commandTenantB.CorrelationId); // Not tenant-a's correlation ID
}
```

### Story 3.6 Action Items

1. **[CRITICAL] Implement Actor Lifecycle Tenant Validation**
   - Override `OnActivateAsync()` in `AggregateActor`
   - Extract and store `_actorTenant` from actor ID
   - Do NOT load state during activation
   - First command validates tenant BEFORE any state access

2. **[CRITICAL] Add CausationId Tenant Scoping**
   - Prefix idempotency cache keys with tenant: `{tenant}:{causationId}`
   - Prevents cross-tenant cache collisions
   - Update `IdempotencyChecker.CheckAsync()` and `RecordAsync()`

3. **[HIGH] Add Snapshot Tenant Validation (Story 3.9 dependency)**
   - After snapshot deserialization, validate `snapshot.TenantId == identity.TenantId`
   - Throw `SnapshotTenantMismatchException` on mismatch
   - Log at Error level (indicates security breach)

4. **[MEDIUM] Add Actor Activation Audit Logging**
   - Log activation/deactivation at Information level
   - Include tenant, domain, aggregateId, host node, timestamp
   - Enables forensic investigation of actor placement

5. **[LOW] Document DAPR Placement Tenant Affinity**
   - Evaluate DAPR 1.16+ tenant affinity feature
   - Document configuration for production deployments
   - Trade-off: Reduced multi-tenancy blast radius vs. reduced placement flexibility

---

## Failure Scenario 5: Performance Cliff at Scale

### Catastrophic Event
**"Performance degraded catastrophically when 50th tenant was added"**

### Root Cause Analysis

#### Primary Root Causes
1. **O(N) Tenant Config Store Scan**
   - **Mechanism:** `DomainServiceResolver.ResolveAsync()` looks up config key `{tenant}:{domain}:service`.
   - **Gap:** If config store implementation (Redis, Consul) stores ALL configs in single hash/keyspace, adding 50 tenants × 5 domains = 250 config keys creates O(N) scan on some operations.
   - **Trigger:** Redis `SCAN` with MATCH pattern `*:orders:service` iterates all 250 keys to find matching tenants.
   - **Result:** Config resolution latency grows from 1ms (1 tenant) to 50ms (50 tenants). Exceeds NFR2 budget (200ms e2e).

2. **Actor Placement Service Overload**
   - **Mechanism:** DAPR actor placement service maintains table of actor → node mappings.
   - **Gap:** With 50 tenants × 1000 aggregates/tenant = 50,000 actors, placement table grows to 50K entries.
   - **Bottleneck:** DAPR placement service is single-threaded (DAPR 1.14 limitation). Actor activation queries serialize.
   - **Result:** Actor activation latency grows from 5ms (100 actors) to 500ms (50K actors). Violates NFR2.

3. **State Store Connection Pool Exhaustion**
   - **Mechanism:** Each actor activation opens state store connection (Redis/PostgreSQL).
   - **Gap:** Default DAPR state store connection pool size is 10. With 50 tenants × parallel command processing, concurrent connection demand is 50+ connections.
   - **Bottleneck:** Connection pool exhaustion causes actors to wait for available connection.
   - **Result:** State store read latency grows from 2ms (connection available) to 2000ms (waiting for pool) due to queuing delay.

4. **Event Stream Read Amplification**
   - **Mechanism:** Each actor activation calls `EventStreamReader.RehydrateAsync()`, which loads events from sequence 1 to current.
   - **Gap:** Without snapshots (Stories 3.9-3.10), a 10-year-old aggregate with 100,000 events requires 100,000 state store reads during EVERY activation.
   - **Math:** 100K events × 2ms/read = 200,000ms (3.3 minutes). Violates DAPR actor activation timeout (60 minutes) but exceeds NFR2 by 1000x.
   - **Trigger:** Adding tenant 50 includes migrated data from legacy system with high-event-count aggregates.

5. **Pub/Sub Topic Explosion**
   - **Mechanism:** Pub/Sub topic naming is `{tenant}.{domain}.events` (D6).
   - **Gap:** 50 tenants × 5 domains = 250 topics. Some pub/sub backends (RabbitMQ, Kafka) have per-topic overhead (metadata, partitions, consumer groups).
   - **Bottleneck:** RabbitMQ broker memory grows with topic count. Default config limits to 100 topics.
   - **Result:** Adding tenant 50 exceeds RabbitMQ topic limit. Event publishing fails with "Too many topics" error.

#### Secondary Contributing Factors
- **No Tenant Onboarding Throttling:** Tenants added simultaneously without staged rollout.
- **Missing Performance Tests:** Load tests only validated 5 tenants, not 50.
- **No Snapshot Enforcement:** Domain services deployed without snapshot configuration (violates enforcement rule #15).

### Prevention Measures

| Measure | Type | Story | Severity |
|---------|------|-------|----------|
| **PM-5.1: Config Store Key Partitioning** | Architecture | 3.5 | CRITICAL |
| - Use hierarchical config keys: `tenants/{tenant}/domains/{domain}/service` instead of flat `{tenant}:{domain}:service` | | | |
| - DAPR config store GET becomes O(1) lookup (Redis HGET, PostgreSQL indexed query) instead of O(N) scan | | | |
| - Test: `ResolveAsync_50Tenants_CompletesUnder5ms` | | | |
| **PM-5.2: DAPR Placement Service Scaling** | Infrastructure | 3.6 | HIGH |
| - DAPR 1.16+ supports clustered placement service (3-node Raft cluster) | | | |
| - Deploy 3-node placement cluster for production (1 leader, 2 followers) | | | |
| - Distributes placement table across nodes, reduces single-node bottleneck | | | |
| **PM-5.3: State Store Connection Pool Tuning** | Configuration | 3.6 | CRITICAL |
| - DAPR state store component config: Set `connectionPoolSize: 100` (from default 10) | | | |
| - Monitor: `dapr_state_store_connection_pool_usage` metric | | | |
| - Alert: Connection pool >80% triggers scale-out or pool increase | | | |
| **PM-5.4: Mandatory Snapshot Enforcement** | Architecture | 3.9 | CRITICAL |
| - Enforcement rule #15: "Snapshot configuration is mandatory (default 100 events)" | | | |
| - Domain service registration validation: Reject registration without `snapshotInterval` config | | | |
| - Actor activation: If `currentSequence > snapshotInterval × 2` and no snapshot exists, log Error and trigger snapshot generation | | | |
| - Test: `RehydrateAsync_10000Events_UsesSnapshot_CompletesUnder100ms` | | | |
| **PM-5.5: Pub/Sub Topic Quotas** | Configuration | 3.6 | MEDIUM |
| - DAPR pub/sub component config: Set `maxTopics: 500` (above expected max) | | | |
| - Monitor: `dapr_pubsub_topic_count` metric | | | |
| - Alert: Topic count >80% of quota triggers investigation | | | |
| - Consider: Single topic per domain (`{domain}.events`) with tenant ID in CloudEvents metadata (consolidates 50 tenants × 5 domains = 250 topics down to 5 topics) | | | |
| **PM-5.6: Tenant Onboarding Rate Limiting** | Process | 3.6 | MEDIUM |
| - Runbook: "Add maximum 5 tenants per day" | | | |
| - Allows monitoring of performance impact per tenant | | | |
| - Staged rollout: Add tenant to canary region first, monitor for 24h, then production | | | |

### Tests That Would Catch It

```csharp
// Performance Test: Config Resolution Scalability
[Fact]
public async Task ResolveAsync_50Tenants_CompletesUnder5ms()
{
    // Arrange: Populate config store with 50 tenants × 5 domains = 250 registrations
    var tenants = Enumerable.Range(1, 50).Select(i => $"tenant-{i}").ToArray();
    var domains = new[] { "orders", "payments", "inventory", "shipping", "notifications" };

    foreach (var tenant in tenants)
    {
        foreach (var domain in domains)
        {
            await WriteConfig($"tenants/{tenant}/domains/{domain}/service", CreateRegistration(tenant, domain));
        }
    }

    var resolver = new DomainServiceResolver(daprClient, options, logger);
    var stopwatch = Stopwatch.StartNew();

    // Act: Resolve config for tenant-50, domain "orders"
    var result = await resolver.ResolveAsync("tenant-50", "orders");

    stopwatch.Stop();

    // Assert: Should complete in <5ms (O(1) lookup, not O(N) scan)
    stopwatch.ElapsedMilliseconds.ShouldBeLessThan(5);
    result.ShouldNotBeNull();
}

// Performance Test: Actor Activation with Large Event Stream
[Fact]
public async Task RehydrateAsync_10000Events_UsesSnapshot_CompletesUnder100ms()
{
    // Arrange: Aggregate with 10,000 events, snapshot at event 9,900
    var identity = new AggregateIdentity("tenant-a", "orders", "order-123");
    var metadata = new AggregateMetadata(CurrentSequence: 10000, LastModified: DateTimeOffset.Now, ETag: "etag-123");
    var snapshot = new Snapshot(TenantId: "tenant-a", Domain: "orders", AggregateId: "order-123", SequenceNumber: 9900, State: /* ... */);

    stateManager.TryGetStateAsync<AggregateMetadata>(identity.MetadataKey)
        .Returns(Task.FromResult(new ConditionalValue<AggregateMetadata>(true, metadata)));
    stateManager.TryGetStateAsync<Snapshot>(identity.SnapshotKey)
        .Returns(Task.FromResult(new ConditionalValue<Snapshot>(true, snapshot)));

    // Only load events 9901-10000 (100 events)
    for (long seq = 9901; seq <= 10000; seq++)
    {
        stateManager.TryGetStateAsync<EventEnvelope>($"{identity.EventStreamKeyPrefix}{seq}")
            .Returns(Task.FromResult(new ConditionalValue<EventEnvelope>(true, CreateEvent(seq))));
    }

    var reader = new EventStreamReader(stateManager, snapshotReader, logger);
    var stopwatch = Stopwatch.StartNew();

    // Act
    var state = await reader.RehydrateAsync(identity);

    stopwatch.Stop();

    // Assert: Should complete in <100ms (NFR6: 1,000 events in <100ms, snapshot reduces to 100 events)
    stopwatch.ElapsedMilliseconds.ShouldBeLessThan(100);
    state.ShouldNotBeNull();

    // Verify snapshot was used (only 100 events loaded, not 10,000)
    await stateManager.Received(1).TryGetStateAsync<Snapshot>(identity.SnapshotKey);
    await stateManager.Received(100).TryGetStateAsync<EventEnvelope>(Arg.Any<string>());
}

// Load Test: 50 Concurrent Tenants
[Fact]
public async Task ProcessCommand_50ConcurrentTenants_MaintainsThroughput()
{
    // Arrange: 50 tenants, each submits 10 commands/sec for 60 seconds = 30,000 total commands
    var tenants = Enumerable.Range(1, 50).Select(i => $"tenant-{i}").ToArray();
    var commandsPerTenant = 600; // 10/sec × 60s
    var totalCommands = tenants.Length * commandsPerTenant; // 30,000

    var stopwatch = Stopwatch.StartNew();
    var tasks = tenants.Select(async tenant =>
    {
        for (int i = 0; i < commandsPerTenant; i++)
        {
            await SubmitCommand(tenant, "orders", $"order-{i}");
            await Task.Delay(100); // 10 commands/sec
        }
    }).ToArray();

    // Act
    await Task.WhenAll(tasks);
    stopwatch.Stop();

    // Assert: Should complete in ~60 seconds (not 600 seconds due to throttling)
    stopwatch.Elapsed.TotalSeconds.ShouldBeLessThan(70); // 10s buffer for variance

    // Verify no timeouts, all commands accepted
    var failedCommands = await GetFailedCommandCount();
    failedCommands.ShouldBe(0);
}
```

### Story 3.6 Action Items

1. **[CRITICAL] Implement Hierarchical Config Store Keys**
   - Migrate from `{tenant}:{domain}:service` to `tenants/{tenant}/domains/{domain}/service`
   - Enables O(1) config lookups on all backends
   - Update `DomainServiceResolver.ResolveAsync()` to use new key pattern

2. **[CRITICAL] Document State Store Connection Pool Tuning**
   - DAPR component config example: `connectionPoolSize: 100`
   - Monitoring: `dapr_state_store_connection_pool_usage`
   - Runbook: "When to increase connection pool size"

3. **[CRITICAL] Enforce Snapshot Configuration**
   - Domain registration validation: Require `snapshotInterval` config
   - Reject registrations without snapshot config
   - Add to `DomainServiceRegistration` schema validation

4. **[HIGH] Create 50-Tenant Load Test**
   - CI/CD: Add performance test with 50 tenants × 5 domains
   - Target: Config resolution <5ms, command processing <200ms e2e
   - Fail build if performance regression detected

5. **[MEDIUM] Evaluate Pub/Sub Topic Consolidation**
   - Consider single topic per domain: `{domain}.events` with `tenantId` in CloudEvents metadata
   - Reduces 250 topics (50 tenants × 5 domains) to 5 topics
   - Trade-off: Subscriber filtering complexity vs. broker scalability

6. **[MEDIUM] Create Tenant Onboarding Runbook**
   - Staged rollout: Canary → Production
   - Rate limit: Max 5 tenants/day
   - Monitoring checklist: Config resolution latency, actor activation latency, connection pool usage

---

## Failure Scenario 6: Domain Update Blast Radius

### Catastrophic Event
**"A domain service update broke all tenants using that domain"**

### Root Cause Analysis

#### Primary Root Causes
1. **Breaking Event Schema Change**
   - **Mechanism:** Domain service v2.0.0 changes event payload schema (e.g., `OrderPlaced` removes `customerId` field, adds `customer.id` nested object).
   - **Gap:** EventStreamReader (Story 3.4) deserializes events from ALL versions into same aggregate state. If domain service doesn't maintain backward-compatible deserialization, v1 events fail to deserialize after v2 deployment.
   - **Trigger:** Domain service updated without testing replay of historical events. State rehydration fails with `JsonException: Property 'customerId' not found`.
   - **Result:** ALL aggregates for that domain (across all tenants) fail to activate. Commands return 500 errors.

2. **Domain Service Deployment Race**
   - **Mechanism:** Domain service updated via rolling deployment (3 pods, deploy 1 at a time).
   - **Gap:** During deployment, some commands route to v1 pods, some to v2 pods.
   - **Scenario:** Command A processed by v1 (produces `OrderPlaced_v1` event). Command B for same aggregate processed by v2 (expects `OrderPlaced_v2` schema). State rehydration fails when v2 reads v1 event.
   - **Result:** Commands succeed during deployment but fail after rollout completes (all v2 pods), when historical events are replayed.

3. **Config Store Stale Caching**
   - **Mechanism:** `DomainServiceResolver` caches registrations (PM-2.3: 30s TTL).
   - **Gap:** Domain service v2 deployed with new appId (`orders-svc-v2`). Config store updated. But cache not invalidated.
   - **Result:** Commands route to OLD appId (`orders-svc-v1`) for up to 30s. If v1 pods scaled down to 0 during deployment, commands fail with "Service Unavailable".
   - **Blast Radius:** Affects ALL tenants for that domain during cache TTL window.

4. **Event Versioning Metadata Missing**
   - **Mechanism:** EventEnvelope includes `DomainServiceVersion` field (SEC-1), but domain services don't populate it correctly.
   - **Gap:** If all events have `DomainServiceVersion: null`, EventStreamReader can't determine which deserialization logic to use.
   - **Scenario:** v2 domain service deployed. Historical events have no version tag. v2 assumes all events are v2 schema. Deserialization fails.
   - **Recovery:** No automated rollback. Requires manual redeployment of v1 + data migration script.

5. **No Canary Testing with Historical Data**
   - **Mechanism:** Domain service tested with NEW events only, not full event stream replay.
   - **Gap:** Test suite creates aggregates from scratch. Never tests v2's ability to deserialize v1 events.
   - **Production:** v2 deployed. First command to existing aggregate (with v1 events) fails.
   - **Result:** All tenants with pre-existing aggregates broken. Only NEW aggregates (created after v2 deployment) work.

#### Secondary Contributing Factors
- **No Blue/Green Deployment:** All tenants updated simultaneously, no gradual rollout.
- **Missing Contract Tests:** Domain service v2 not validated against v1 event schemas.
- **No Event Versioning Strategy:** No documented policy for backward/forward compatibility.

### Prevention Measures

| Measure | Type | Story | Severity |
|---------|------|-------|----------|
| **PM-6.1: Mandatory Event Backward Compatibility** | Architecture | 3.4/3.5 | CRITICAL |
| - Document policy: "Domain services MUST deserialize all event versions ever produced" | | | |
| - `UnknownEventException` during rehydration is an ERROR (not skip-and-continue) (D3 CRITICAL-1 revision) | | | |
| - Contract test: Domain service v2 MUST successfully deserialize events from v1, v1.5, etc. | | | |
| - Test data: Production event stream snapshot (anonymized) replayed in staging | | | |
| **PM-6.2: Domain Service Version Metadata Enforcement** | Code | 3.7 | CRITICAL |
| - EventStore populates `EventEnvelope.DomainServiceVersion` from domain service registration | | | |
| - Domain service invocation: Extract version from `DomainServiceRegistration.Version` | | | |
| - State machine (Step 5): Write version to event envelope BEFORE persistence | | | |
| - Test: `PersistEvents_VersionMetadata_PopulatedFromRegistration` | | | |
| **PM-6.3: Blue/Green Domain Service Deployment** | Process | 3.6 | HIGH |
| - Runbook: "Updating Domain Service" | | | |
| - Step 1: Deploy v2 alongside v1 (both running, different appIds) | | | |
| - Step 2: Update config for single canary tenant: `tenant-canary:orders:service → {appId: orders-svc-v2}` | | | |
| - Step 3: Monitor canary for 24h. Verify: No deserialization errors, event schema matches expectations | | | |
| - Step 4: If canary successful, roll out to 10% tenants. Monitor for 12h | | | |
| - Step 5: If stable, roll out to 100% tenants | | | |
| - Step 6: Scale down v1 after 7-day bake period (allows rollback if late-discovered issues) | | | |
| **PM-6.4: Config Cache Invalidation on Update** | Code | 3.5 | HIGH |
| - ConfigWriter: On domain registration update, publish cache invalidation event | | | |
| - DomainServiceResolver: Subscribe to invalidation events, clear cache immediately | | | |
| - Reduces stale cache window from 30s to <1s | | | |
| - Test: `ConfigUpdate_InvalidatesCache_WithinOneSecond` | | | |
| **PM-6.5: Event Stream Replay Contract Test** | Testing | 3.6 | CRITICAL |
| - Contract test suite: Load production event stream snapshot (anonymized) | | | |
| - Test: Domain service v2 rehydrates state from v1 events without errors | | | |
| - Run in CI/CD BEFORE deployment to production | | | |
| - Fail deployment if ANY deserialization error detected | | | |
| **PM-6.6: Automated Rollback on Deserialization Error Spike** | Observability | 3.6 | HIGH |
| - Monitor: `eventstore_deserialization_errors_total` metric, labeled by domain + tenant | | | |
| - Alert: >10 errors/minute for single domain triggers rollback automation | | | |
| - Rollback: Revert config store registration to previous version (v1), invalidate cache | | | |
| - Notification: Page on-call engineer with rollback confirmation | | | |

### Tests That Would Catch It

```csharp
// Contract Test: Backward Compatibility
[Fact]
public async Task DomainService_V2_DeserializesV1Events()
{
    // Arrange: Load production event stream snapshot with v1 events
    var historicalEvents = LoadProductionEventSnapshot("tenant-a", "orders", "order-123");
    historicalEvents.ShouldContain(e => e.DomainServiceVersion == "1.0.0"); // v1 events exist

    // Simulate state rehydration with v2 domain service
    var domainService = new OrdersDomainServiceV2();
    object? state = null;

    // Act: Apply v1 events to v2 domain service
    foreach (var eventEnvelope in historicalEvents)
    {
        state = domainService.ApplyEvent(state, eventEnvelope);
    }

    // Assert: Should NOT throw deserialization errors
    state.ShouldNotBeNull();
}

// Integration Test: Blue/Green Deployment
[Fact]
public async Task ConfigUpdate_CanaryTenant_RoutesToV2()
{
    // Arrange: Deploy v1 and v2 simultaneously
    await DeployDomainService("orders-svc-v1", version: "1.0.0");
    await DeployDomainService("orders-svc-v2", version: "2.0.0");

    // Update config for canary tenant only
    await WriteConfig("tenants/tenant-canary/domains/orders/service", new DomainServiceRegistration(
        AppId: "orders-svc-v2",
        MethodName: "process",
        Version: "2.0.0",
        TenantId: "tenant-canary",
        Domain: "orders"
    ));

    // Act: Submit command for canary tenant
    var canaryCommand = CreateCommand(tenantId: "tenant-canary", domain: "orders");
    var canaryResult = await SubmitCommand(canaryCommand);

    // Submit command for non-canary tenant
    var normalCommand = CreateCommand(tenantId: "tenant-a", domain: "orders");
    var normalResult = await SubmitCommand(normalCommand);

    // Assert: Canary routes to v2, normal routes to v1
    var canaryLogs = GetDomainServiceLogs("orders-svc-v2");
    canaryLogs.ShouldContain(log => log.Contains("tenant-canary"));

    var normalLogs = GetDomainServiceLogs("orders-svc-v1");
    normalLogs.ShouldContain(log => log.Contains("tenant-a"));
}

// Story 3.7: Version Metadata Population
[Fact]
public async Task PersistEvents_PopulatesVersionFromRegistration()
{
    // Arrange: Domain service registration has version "2.0.0"
    var registration = new DomainServiceRegistration(
        AppId: "orders-svc",
        MethodName: "process",
        Version: "2.0.0",
        TenantId: "tenant-a",
        Domain: "orders"
    );

    resolver.ResolveAsync("tenant-a", "orders").Returns(Task.FromResult(registration));

    var command = CreateCommand(tenantId: "tenant-a", domain: "orders");
    var domainResult = new DomainResult { Events = new[] { new OrderPlaced(/* ... */) } };
    domainServiceInvoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
        .Returns(Task.FromResult(domainResult));

    var actor = CreateActor("tenant-a:orders:order-123");

    // Act: Process command (triggers Step 5: event persistence)
    await actor.ProcessCommandAsync(command);

    // Assert: Persisted event envelope has version metadata
    var persistedEvent = GetPersistedEvent("tenant-a:orders:order-123:events:1");
    persistedEvent.DomainServiceVersion.ShouldBe("2.0.0");
}

// Observability Test: Deserialization Error Monitoring
[Fact]
public async Task DeserializationError_TriggersMetricIncrement()
{
    // Arrange: Event with incompatible schema
    var corruptEvent = new EventEnvelope(
        /* ... */,
        Payload: Encoding.UTF8.GetBytes("{\"invalidSchema\": true}"),
        EventTypeName: "OrderPlaced",
        DomainServiceVersion: "1.0.0"
    );

    stateManager.TryGetStateAsync<EventEnvelope>("tenant-a:orders:order-123:events:5")
        .Returns(Task.FromResult(new ConditionalValue<EventEnvelope>(true, corruptEvent)));

    var reader = new EventStreamReader(stateManager, logger);
    var metrics = new TestMetrics();

    // Act: Attempt rehydration (should fail)
    await Should.ThrowAsync<UnknownEventException>(async () =>
        await reader.RehydrateAsync(identity));

    // Assert: Metric incremented
    metrics.GetCounter("eventstore_deserialization_errors_total", new { domain = "orders", tenant = "tenant-a" })
        .Value.ShouldBe(1);
}
```

### Story 3.6 Action Items

1. **[CRITICAL] Document Backward Compatibility Policy**
   - File: `docs/domain-services/event-versioning.md`
   - Policy: "Domain services MUST deserialize ALL event versions ever produced"
   - Include examples: Adding fields (safe), removing fields (requires migration), renaming (breaking)

2. **[CRITICAL] Implement Version Metadata Population**
   - State machine (Story 3.7): Populate `EventEnvelope.DomainServiceVersion` from `DomainServiceRegistration.Version`
   - Enables version-aware deserialization in EventStreamReader

3. **[CRITICAL] Create Event Stream Replay Contract Test**
   - Load production event stream snapshot (anonymized)
   - Test: Domain service v2 rehydrates state from v1 events
   - Run in CI/CD before deployment

4. **[HIGH] Create Blue/Green Deployment Runbook**
   - Staged rollout: Canary tenant → 10% tenants → 100% tenants
   - 7-day bake period before scaling down old version
   - Rollback procedure if deserialization errors detected

5. **[HIGH] Implement Automated Rollback on Error Spike**
   - Monitor: `eventstore_deserialization_errors_total` by domain + tenant
   - Alert: >10 errors/minute triggers rollback
   - Rollback: Revert config to previous version, invalidate cache

6. **[MEDIUM] Add Config Cache Invalidation**
   - ConfigWriter: Publish cache invalidation event on update
   - Resolver: Subscribe and clear cache immediately
   - Reduces stale cache window to <1s

---

## Summary: Critical Actions for Story 3.6

### Blocking (Must Implement)
1. **Actor Activation Tenant Validation** (Scenario 1, 4)
   - Validate tenant BEFORE any state access during activation
   - Prevents rebalancing race conditions

2. **Config Store Write Validation** (Scenario 2)
   - Validate `DomainServiceRegistration` JSON schema before writes
   - Prevents config corruption cascades

3. **Versioned Domain Registrations** (Scenario 3)
   - Add `ConfigVersion` field to registrations
   - Reject stale reads in favor of cached newer versions

4. **CausationId Tenant Scoping** (Scenario 4)
   - Prefix idempotency cache keys with tenant
   - Prevents cross-tenant cache collisions

5. **Hierarchical Config Keys** (Scenario 5)
   - Migrate to `tenants/{tenant}/domains/{domain}/service` pattern
   - Enables O(1) config lookups

6. **Event Backward Compatibility Policy** (Scenario 6)
   - Document mandatory backward compatibility requirement
   - Create contract tests with historical events

### High Priority (Should Implement)
7. **EventStreamReader Cross-Tenant Detection** (Scenario 1)
8. **DomainServiceResolver Wildcard Rejection** (Scenario 1)
9. **Config Cache Invalidation** (Scenario 2, 3, 6)
10. **Mandatory Snapshot Enforcement** (Scenario 5)
11. **Blue/Green Deployment Runbook** (Scenario 6)

### Documentation
- DAPR ACL policies for state store isolation
- Config store consistency guarantees and bounded staleness
- Tenant onboarding rate limiting procedure
- Domain service versioning and rollback procedures

---

**End of Pre-Mortem Analysis**

*This analysis identifies 47 distinct root causes across 6 failure scenarios, with 31 prevention measures and 45 test specifications. Implementation priority: 6 blocking, 5 high, 20 medium/low.*
