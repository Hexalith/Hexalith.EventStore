---
stepsCompleted: [1, 2, 3, 4]
inputDocuments: []
session_topic: 'Simplify Hexalith.EventStore client API with fluent builder pattern and convention-based configuration'
session_goals: 'Minimal quickstart friction, convention-based settings from domain name, progressive disclosure for experts, two-line onboarding'
selected_approach: 'ai-recommended'
techniques_used: ['Assumption Reversal', 'SCAMPER Method', 'Role Playing']
ideas_generated: 19
context_file: ''
session_active: false
workflow_completed: true
---

# Brainstorming Session Results

**Facilitator:** Jerome
**Date:** 2026-02-28

## Session Overview

**Topic:** Simplify Hexalith.EventStore client API with fluent builder pattern and convention-based configuration

**Goals:**
- Minimal quickstart friction — DAPR wiring invisible by default
- Convention-based settings — derive config keys/names from domain name automatically
- Progressive disclosure — simple for beginners, fully customizable for experts
- Two-line onboarding — `AddEventStore` at registration, `UseEventStore` at runtime

### Session Setup

The session focuses on redesigning the consumer-facing API surface of Hexalith.EventStore. The target developer experience is:

```csharp
// Registration
builder.AddEventStore(...)

// Usage
app.UseEventStore("MyDomain", events => MyDomain.Apply(events))
```

DAPR complexity should be abstracted away for quickstart scenarios. Settings should follow naming conventions derived from the domain name. Expert users must retain full control over custom configuration when needed.

## Technique Selection

**Approach:** AI-Recommended Techniques
**Analysis Context:** EventStore client API simplification with focus on developer experience and progressive disclosure

**Recommended Techniques:**

- **Assumption Reversal:** Surface and challenge inherited assumptions from DAPR's configuration model to identify what can be replaced by conventions
- **SCAMPER Method:** Systematically walk the API surface — Substitute, Combine, Adapt, Modify, Put to other uses, Eliminate, Reverse — to generate concrete API shape candidates
- **Role Playing:** Stress-test API designs from beginner, intermediate, and expert developer personas to validate progressive disclosure

**AI Rationale:** The challenge requires both deconstructing inherited complexity (Assumption Reversal) and constructing new elegant abstractions (SCAMPER), then validating from multiple user perspectives (Role Playing).

## Technique Execution Results

### Assumption Reversal

**Interactive Focus:** Surfacing and flipping 7 core assumptions about EventStore client configuration

**Key Breakthroughs:**

**[Assumption Reversal #1]**: DAPR Visibility
_Concept_: The client SDK encapsulates all three DAPR interactions (command dispatch, event stream reading, event publishing) so the domain service developer never references DAPR directly. DAPR becomes a pure infrastructure implementation detail behind the SDK facade.
_Novelty_: Instead of the consumer configuring DAPR and then using EventStore on top, the EventStore SDK is the only surface — DAPR is its private dependency.

**[Assumption Reversal #2]**: Convention-as-Configuration
_Concept_: A single domain name string drives all DAPR resource naming — state store, pub/sub topic, command endpoint — through predictable conventions. Zero configuration files needed for the default path.
_Novelty_: Eliminates an entire configuration layer. The domain name isn't just a label, it's the configuration itself.

**[Assumption Reversal #3]**: All-or-Nothing Control
_Concept_: The SDK handles everything by default (command reception, aggregate lifecycle, event publishing, retries) but every single piece is overridable. The developer starts with zero boilerplate and progressively takes control only where they need it.
_Novelty_: Reverses the typical SDK pattern where you build up from nothing. Instead you start with everything and peel back layers selectively.

**[Assumption Reversal #4a]**: Naming Override
_Concept_: Convention derives `mydomain-eventstore`, `mydomain-events`, etc. but experts can override any individual name without abandoning the convention for everything else.
_Novelty_: Partial override — change the state store name but keep the auto-derived pub/sub topic.

**[Assumption Reversal #4b]**: Serialization Override
_Concept_: SDK defaults to a built-in serializer but allows plugging in custom serializers per domain or even per event type.
_Novelty_: Most SDKs make you choose one serializer globally — per-domain granularity is rare.

**[Assumption Reversal #4c]**: Multi-Tenancy Override
_Concept_: By default one domain = one set of DAPR resources. Multi-tenant scenarios need tenant-scoped resources. The convention extends: `mydomain-{tenantId}-eventstore`.
_Novelty_: Tenant isolation built into the naming convention itself rather than being a separate cross-cutting concern.

**[Assumption Reversal #4d]**: Custom DAPR Component Configuration
_Concept_: SDK ships with sensible DAPR component defaults, but experts can provide their own component specs — custom state store backends, different pub/sub providers, specific retry policies.
_Novelty_: The SDK generates DAPR component configuration by convention but accepts overrides, so experts aren't locked out of DAPR's full power.

**[Assumption Reversal #5]**: Flat Configuration
_Concept_: Configuration is layered — global defaults for cross-cutting concerns (serialization, multi-tenancy) and per-domain overrides for domain-specific concerns (naming, DAPR components). Per-domain inherits from global unless explicitly overridden.
_Novelty_: Instead of one flat options bag or one-per-domain, a cascading model where `AddEventStore()` sets global defaults and per-domain options override selectively.

**[Assumption Reversal #6]**: Explicit Registration Required
_Concept_: Auto-discovery scans for domain types (by interface, attribute, or convention) and registers them with default conventions. Experts can override any auto-discovered domain or add unregistered ones explicitly.
_Novelty_: The zero-config quickstart becomes truly zero — `builder.AddEventStore()` discovers domains, derives all names, wires all DAPR resources.

**[Assumption Reversal #7]**: External Event Handling
_Concept_: Domain types are self-describing — they define their own event application logic. The SDK discovers the domain, discovers its Apply method, and wires everything. `UseEventStore()` with no arguments is the complete quickstart.
_Novelty_: The consumer writes zero infrastructure code. They write their domain, and the SDK does the rest.

### SCAMPER Method

**Interactive Focus:** Systematic API surface design through seven creative lenses

**Key Breakthroughs:**

**[SCAMPER-S #1]**: Substitute String Names with Type + Attribute
_Concept_: Domain type name is the default identifier (`OrderAggregate` → `"order"`). An `[EventStoreDomain("custom-name")]` attribute overrides the convention when needed. No magic strings in the fluent API.
_Novelty_: Type safety by default, string flexibility by exception.

**[SCAMPER-C #1]**: Combine Add/Use — Evaluated and Rejected
_Concept_: Merging `AddEventStore` and `UseEventStore` was considered but the two calls serve genuinely distinct lifecycle phases — DI registration vs. runtime activation (DAPR subscriptions, middleware pipeline).
_Novelty_: Confirms the two-call pattern is correct, not accidental complexity.

**[SCAMPER-A #1]**: Adapt IOptions + Self-Configuring Domain
_Concept_: Global cross-cutting config uses the familiar external `AddEventStore(options => ...)` pattern. Domain-specific config lives inside the domain type itself via an `OnConfiguring` override method. Runtime resolution cascades through 5 layers.
_Novelty_: Configuration lives where it's most relevant. Global concerns stay in Program.cs, domain concerns stay in the domain type.

**[SCAMPER-M #1]**: Modify Activation Granularity
_Concept_: `UseEventStore()` activates everything by default, but supports selective activation — by domain type, by capability (subscriptions vs. commands vs. projections), or both.
_Novelty_: Most event store SDKs are all-or-nothing at runtime. Granular activation means the same SDK serves different service archetypes without waste.

**[SCAMPER-P #1]**: Projections via Same Convention Engine
_Concept_: Projection types are auto-discovered alongside aggregates. The SDK wires them to the same domain event stream using identical naming conventions. Projections are self-describing — they declare their Apply methods just like aggregates.
_Novelty_: Aggregates and projections share the same discovery, convention, and wiring model.

**[SCAMPER-E #1]**: Eliminate Mandatory Config Files
_Concept_: No `appsettings.json` section required for quickstart. But the SDK binds to `appsettings.json` when present, so ops teams can override settings at deployment time without recompilation.
_Novelty_: Config files are a deployment-time concern that the developer never sees during development.

**[SCAMPER-R #1]**: Reverse Registration — Domain as Manifest
_Concept_: Simple infrastructure needs declared via attributes on the domain type. Complex scenarios use the `OnConfiguring` override method. Domain types fully own their infrastructure declaration.
_Novelty_: Infrastructure config migrates from external files into the domain type itself — via two complementary mechanisms (attributes for declarative, method for imperative).

### Role Playing

**Interactive Focus:** Validating progressive disclosure across three developer personas

**Key Breakthroughs:**

**[Role Play - Beginner #1]**: One NuGet, Two Lines
_Concept_: The complete quickstart is: `dotnet add package Hexalith.EventStore`, two lines in Program.cs, write your domain type. No DAPR references, no config files, no appsettings.json, no component YAML.
_Novelty_: The NuGet package transitively brings everything needed. The beginner's dependency graph is a single node.

**[Role Play - Intermediate #1]**: Partial Customization
_Concept_: Three domains, one global override (serialization), one domain-specific override (state store name via attribute), selective activation (projections only). Customization is surgical — touch only what differs.
_Novelty_: The intermediate developer adds exactly 3 lines beyond the quickstart to handle a real multi-domain, custom-serialization, read-service scenario.

**[Role Play - Expert #1]**: Full DAPR Control with Multi-Tenancy
_Concept_: Multi-tenant SaaS with per-domain serializers, custom DAPR components, retry policies, tenant-scoped naming patterns, and deployment-time overrides. Every piece is customizable yet the structure remains the same two-line Program.cs skeleton.
_Novelty_: The skeleton never changes from beginner to expert. Complexity moves to options and domain types, never to the wiring pattern.

### Creative Facilitation Narrative

The session began by deconstructing the existing DAPR-coupled API through Assumption Reversal, which revealed that all infrastructure complexity could be hidden behind conventions derived from domain names. SCAMPER then systematically shaped the API surface — confirming the Add/Use split, introducing type-safe domain registration, cascading configuration, and self-configuring domain types. Role Playing validated the design across three developer personas, proving that the same two-line skeleton scales from zero-config quickstart to enterprise multi-tenant deployment.

### Session Highlights

**Creative Strengths:** Consistent "both" principle — every design decision offers a simple default AND expert override
**Breakthrough Moments:** The realization that the domain type itself is a complete infrastructure manifest (attributes + OnConfiguring)
**Design Principle:** The skeleton never changes — complexity is always in options and domain types, never in wiring

## Idea Organization and Prioritization

### Theme 1: Zero-Config Quickstart

- **DAPR Encapsulation** — SDK owns all 3 DAPR interactions. Consumer never references DAPR.
- **One NuGet, Two Lines** — `dotnet add package`, `AddEventStore()`, `UseEventStore()`. Nothing else.
- **Auto-Discovery** — Assemblies scanned for aggregates and projections. No explicit registration needed.
- **Self-Describing Domains** — Domain types contain their own Apply methods. No external handler wiring.

### Theme 2: Convention Engine

- **Name-Derived Resources** — `OrderAggregate` → `order-eventstore`, `order-events`, `order-commands`
- **Type-as-Identifier** — Generic `AddEventStoreDomain<T>()` instead of magic strings
- **Attribute Override** — `[EventStoreDomain("custom-name")]` when conventions don't fit
- **Projection Sharing** — Projections discovered and wired to the same domain event stream automatically

### Theme 3: Cascading Configuration

Five-layer override model from convention to deployment:

1. **Convention defaults** — domain name drives everything
2. **Global code options** — `AddEventStore(options => ...)`
3. **Domain self-config** — `OnConfiguring` override method
4. **appsettings.json / environment variables** — deployment-time
5. **Explicit external override** — `Configure<EventStoreDomainOptions>(...)`

### Theme 4: Expert Override Pillars

- **Naming** — Override any derived name per-domain via attribute or OnConfiguring
- **Serialization** — Global default serializer + per-domain override
- **Multi-Tenancy** — Tenant resolver, tenant-scoped naming patterns (`domain-{tenantId}-store`)
- **DAPR Components** — Custom pub/sub, state store, retry policies per domain or global

### Theme 5: Selective Runtime Activation

- **Per-Domain Activation** — `UseEventStore<OrderAggregate>()` for single-domain services
- **Per-Capability Activation** — Enable/disable subscriptions, command endpoints, projections independently
- **Service Archetypes** — Command service, read service, full service all from the same SDK

### Breakthrough Concepts

- **Domain-as-Manifest** — Attributes + OnConfiguring make the domain type a complete infrastructure declaration
- **Skeleton Never Changes** — From beginner to expert, the same `AddEventStore()` / `UseEventStore()` pattern scales without changing shape

### Prioritization Results

All 5 themes prioritized as essential, organized into implementation phases:

- **Phase 1 (Foundation):** Quickstart + Convention Engine + Cascading Config
- **Phase 2 (Power):** Expert Override Pillars
- **Phase 3 (Enterprise):** Selective Activation + Service Presets

## Action Plans

### Priority 1: Zero-Config Quickstart

1. Create `Hexalith.EventStore.Client` NuGet package with transitive DAPR dependencies
2. Implement `AddEventStore()` extension method on `IHostApplicationBuilder` with assembly scanning
3. Implement `UseEventStore()` extension method on `IHost`/`WebApplication` for DAPR subscriptions and middleware
4. Define `EventStoreAggregate` base class with virtual Apply method pattern
5. Define `EventStoreProjection<TReadModel>` base class with same Apply pattern
6. Build integration test: single aggregate, zero config, events flow end-to-end

**Success Indicator:** New developer goes from `dotnet new` to working event-sourced domain in under 5 minutes with zero DAPR knowledge.

### Priority 2: Convention Engine

1. Define naming convention rules: type name → kebab-case domain name (`OrderAggregate` → `order`)
2. Implement resource name derivation: `{domain}-eventstore`, `{domain}-events`, `{domain}-commands`
3. Create `[EventStoreDomain("name")]` attribute for convention override
4. Implement assembly scanner for types inheriting `EventStoreAggregate` and `EventStoreProjection<T>`
5. Wire discovered projections to the same event stream as their parent domain
6. Support both `AddEventStoreDomain<T>()` (explicit type-safe) and auto-discovery

**Success Indicator:** No string-based configuration in quickstart. All resource names predictable and inspectable.

### Priority 3: Cascading Configuration

1. Define `EventStoreOptions` class for global cross-cutting settings
2. Define `EventStoreDomainOptions` class for per-domain settings
3. Implement 5-layer cascade resolution
4. Integrate with `IOptions<T>` / `IOptionsSnapshot<T>` for standard .NET configuration binding
5. Bind `appsettings.json` section `EventStore` and `EventStore:Domains:{name}` by convention
6. Implement `OnConfiguring(EventStoreOptions options)` virtual method on `EventStoreAggregate`

**Success Indicator:** Any setting configurable at any layer. Lower layers override higher ones predictably.

### Priority 4: Expert Override Pillars

1. **Naming:** Per-resource name overrides in `EventStoreDomainOptions`
2. **Serialization:** `IEventStoreSerializer` interface with global and per-domain override
3. **Multi-Tenancy:** `TenantResolver<T>()` and `NamingPattern` with `{tenantId}` placeholder
4. **DAPR Components:** `Dapr.PubSubComponent`, `StateStoreComponent`, `RetryPolicy` at both levels
5. Infrastructure attribute set: `[UsePubSub("name")]`, `[StateStore("name")]`, `[UseSerializer<T>]`

**Success Indicator:** Multi-tenant SaaS with per-domain serializers and custom DAPR backends works without workarounds.

### Priority 5: Selective Runtime Activation

1. Implement activation options: `Subscriptions`, `CommandEndpoints`, `Projections` booleans
2. Support per-domain activation: `UseEventStore<OrderAggregate>()`
3. Define service archetype presets: `UseEventStoreCommandService()`, `UseEventStoreReadService()`
4. Ensure non-activated components have zero runtime overhead

**Success Indicator:** Read-only and command-only services use the same NuGet with different single-line activation.

### Implementation Sequence

```
Phase 1: Foundation          Phase 2: Power           Phase 3: Enterprise
┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐
│ 1. Quickstart    │    │ 4. Expert        │    │ 5. Selective     │
│ 2. Convention    │ →  │    Overrides     │ →  │    Activation    │
│ 3. Cascading     │    │                  │    │    Service       │
│    Config        │    │                  │    │    Presets       │
└──────────────────┘    └──────────────────┘    └──────────────────┘
```

## Session Summary and Insights

**Key Achievements:**

- 19 distinct ideas generated across 3 creative techniques
- Complete API design emerged from structured brainstorming
- Progressive disclosure validated across 3 developer personas (beginner, intermediate, expert)
- 5 implementation themes with concrete action plans and phased delivery sequence

**Core Design Principle Discovered:** "The skeleton never changes." From zero-config beginner to multi-tenant enterprise expert, the same `AddEventStore()` / `UseEventStore()` pattern holds. Complexity always moves to options and domain types, never to the wiring pattern itself.

**API Surface Summary:**

```csharp
// Beginner — zero config
builder.AddEventStore();
app.UseEventStore();

// Intermediate — global options + selective activation
builder.AddEventStore(options => options.UseSerializer<MessagePackSerializer>());
app.UseEventStore(activate => activate.CommandEndpoints = false);

// Expert — full control
builder.AddEventStore(options =>
{
    options.UseSerializer<MessagePackSerializer>();
    options.MultiTenancy.Enabled = true;
    options.MultiTenancy.TenantResolver<HttpHeaderTenantResolver>();
    options.Dapr.PubSubComponent = "rabbitmq-pubsub";
});
app.UseEventStore();
```

```csharp
// Self-configuring domain type
[EventStoreDomain("billing")]
public class BillingAggregate : EventStoreAggregate
{
    protected override void OnConfiguring(EventStoreOptions options)
    {
        options.UseSerializer<ProtobufSerializer>();
        options.Dapr.StateStoreComponent = "postgresql-billing";
    }

    public void Apply(InvoiceCreated e) => ...
    public void Apply(PaymentReceived e) => ...
}
```
