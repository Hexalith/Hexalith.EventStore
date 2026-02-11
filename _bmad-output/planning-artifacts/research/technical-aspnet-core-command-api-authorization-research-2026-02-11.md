---
stepsCompleted: [1, 2, 3, 4, 5, 6]
inputDocuments: []
workflowType: 'research'
lastStep: 6
research_type: 'technical'
research_topic: 'ASP.NET Core Command API Authorization (Tenant, Domain, Behavior-based)'
research_goals: 'Concrete implementation for Hexalith.EventStore covering both authorization model design and middleware/enforcement layer for command API endpoints with tenant, domain, and command-type access control'
user_name: 'Jerome'
date: '2026-02-11'
web_research_enabled: true
source_verification: true
---

# Multi-Dimensional Command Authorization for Event-Sourced Systems: Comprehensive ASP.NET Core Technical Research

## Executive Summary

This research provides a complete authorization architecture for Hexalith.EventStore's command API endpoints, addressing the three-dimensional access control problem: **who can execute which commands, in which domains, for which tenants**. The research draws from current ASP.NET Core 10 capabilities, CQRS/event sourcing patterns, and proven multi-tenant authorization approaches.

ASP.NET Core's built-in policy-based authorization framework provides all the primitives needed — `IAuthorizationRequirement`, `AuthorizationHandler<T>`, `IAuthorizationPolicyProvider`, and `IClaimsTransformation` — without requiring external authorization infrastructure. The recommended architecture uses a **two-layer enforcement model**: coarse-grained checks at the API endpoint (tenant membership, basic role), and fine-grained per-command authorization in a MediatR `IPipelineBehavior` (tenant + domain + command-type evaluation).

**Key Technical Findings:**

- A **policy-based ABAC approach** (Attribute-Based Access Control) is optimal for Hexalith's three-dimensional permission model — pure RBAC leads to role explosion in multi-tenant contexts
- **MediatR pipeline behaviors** provide the ideal enforcement point for command authorization in CQRS systems, intercepting commands before state changes regardless of entry point (API, message bus, background jobs)
- A **hybrid permission model** — JWT claims for coarse tenant/role checks, cached permission store for fine-grained domain + command-type evaluation — balances performance with flexibility
- **Defense in depth** across six layers (gateway, authentication, endpoint, pipeline, domain, data) ensures no single bypass vector compromises authorization
- `.NET 8+` introduced `IAuthorizationRequirementData`, significantly reducing boilerplate for custom authorization attributes

**Top 5 Recommendations:**

1. Build on ASP.NET Core's built-in authorization framework (zero additional dependencies)
2. Implement `AuthorizationBehavior<TRequest, TResponse>` as the primary command enforcement point
3. Use `IClaimsTransformation` to bridge JWT identity with Hexalith's permission store
4. Deploy per-tenant cache partitioning for authorization decisions from day one
5. Evaluate AuthPermissions.AspNetCore for accelerating admin/permission management features

---

## Table of Contents

1. [Technical Research Introduction and Methodology](#1-technical-research-introduction-and-methodology)
2. [Technology Stack and Authorization Framework Analysis](#2-technology-stack-and-authorization-framework-analysis)
3. [Integration Patterns: Authorization in the Command Pipeline](#3-integration-patterns-authorization-in-the-command-pipeline)
4. [Architectural Patterns and Design](#4-architectural-patterns-and-design)
5. [Implementation Approaches and Concrete Patterns](#5-implementation-approaches-and-concrete-patterns)
6. [Performance, Scalability, and Security](#6-performance-scalability-and-security)
7. [Strategic Recommendations and Roadmap](#7-strategic-recommendations-and-roadmap)
8. [Source Documentation and Methodology](#8-source-documentation-and-methodology)

---

## 1. Technical Research Introduction and Methodology

### Research Significance

Authorization in event-sourced CQRS systems presents a unique challenge: commands are the sole entry point for state changes, and each command carries multi-dimensional context (tenant, domain, behavior type) that must be evaluated before any events are produced. Unlike traditional CRUD applications where authorization maps to resources and operations, a CQRS command API requires authorization that understands the **intent** (command type), the **scope** (domain), and the **isolation boundary** (tenant) simultaneously.

For Hexalith.EventStore, this is foundational infrastructure — every command that enters the system must pass through authorization gates that evaluate these three dimensions before producing events. Getting this right means security, performance, and developer ergonomics all working together.

### Research Methodology

- **Technical Scope**: ASP.NET Core 10 authorization framework, CQRS pipeline authorization, multi-tenant patterns, ABAC/RBAC models, permission data architecture
- **Data Sources**: Microsoft Learn documentation, official .NET repositories, established .NET community authors (Jon P Smith, Milan Jovanovic, Andrew Lock), NuGet package documentation, Azure Architecture Center
- **Analysis Framework**: Structured evaluation across technology stack, integration patterns, architectural patterns, and implementation approaches
- **Time Period**: Current (.NET 10 LTS, February 2026) with evolution context from .NET 6-9
- **Verification**: All technical claims verified against current public sources with URL citations

### Research Goals Achieved

**Original Goal:** Concrete implementation for Hexalith.EventStore covering both authorization model design and middleware/enforcement layer for command API endpoints with tenant, domain, and command-type access control.

**Achieved:**
- Complete authorization model architecture with three-dimensional permission structure (Tenant x Domain x CommandType)
- Two-layer enforcement design (API endpoint + MediatR pipeline) with concrete code patterns
- Claims integration strategy bridging identity providers with Hexalith's permission model
- Build vs. buy analysis of relevant libraries (AuthPermissions.AspNetCore, Casbin.NET, MediatR.Behaviors.Authorization)
- 4-phase implementation roadmap with testing strategy and observability plan

---

## 2. Technology Stack and Authorization Framework Analysis

### Core Authorization Framework — ASP.NET Core Built-in

ASP.NET Core ships with a mature, extensible authorization system built around three key abstractions:

- **Authorization Requirement** (`IAuthorizationRequirement`): Defines a condition that must be satisfied. Requirements are simple marker classes — the logic lives in handlers.
- **Authorization Handler** (`AuthorizationHandler<TRequirement>` or `AuthorizationHandler<TRequirement, TResource>`): Contains the actual decision logic. Multiple handlers can evaluate the same requirement; only one needs to succeed.
- **Authorization Policy**: A named collection of requirements registered at startup via `builder.Services.AddAuthorization(options => options.AddPolicy(...))`.

For Hexalith's three-dimensional access model (tenant, domain, command type), the built-in framework provides everything needed to compose custom requirements and handlers without external dependencies.

_Key package: `Microsoft.AspNetCore.Authorization` v10.0.2 (ships with ASP.NET Core 10)_
_Source: [Policy-based authorization in ASP.NET Core — Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies?view=aspnetcore-10.0)_

**`IAuthorizationRequirementData` (introduced .NET 8)**: Enables custom authorization attributes that carry requirement data directly, eliminating the need for `IAuthorizationPolicyProvider` boilerplate. This allows syntax like `[CommandAuthorize(tenant: "acme", domain: "orders")]` that automatically maps to requirements and handlers.
_Source: [Custom authorization policies with IAuthorizationRequirementData — Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/iard?view=aspnetcore-9.0)_

**Resource-based authorization**: When authorization depends on the resource being accessed (e.g., the command object itself), ASP.NET Core supports `IAuthorizationService.AuthorizeAsync(user, resource, requirement)` — calling authorization imperatively within handlers rather than relying solely on endpoint attributes.
_Source: [Resource-based authorization in ASP.NET Core — Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resourcebased?view=aspnetcore-10.0)_

### Authorization Model Approaches (RBAC, ABAC, ReBAC)

For a multi-tenant CQRS system, pure RBAC is insufficient. Research confirms that modern SaaS and multi-tenant systems require contextual authorization:

- **RBAC** (Role-Based): Simple role checks. Breaks down when the same user has different permissions per tenant/domain. Leads to role explosion.
- **ABAC** (Attribute-Based): Evaluates attributes of the user, resource, and environment. Ideal for rules like "user X can execute PlaceOrder commands in the Orders domain of tenant Acme."
- **ReBAC** (Relationship-Based): Models permissions as relationships between entities (Google Zanzibar approach). Powerful for deeply nested hierarchies.
- **Policy-Based (combined)**: When RBAC, ABAC, and ReBAC are combined, the result is policy-based access control — which is exactly what ASP.NET Core's authorization pipeline supports natively.

_For Hexalith, a **policy-based ABAC approach** using ASP.NET Core's built-in framework is the most practical: it evaluates tenant, domain, and command-type attributes without requiring external infrastructure._
_Source: [RBAC vs ABAC & ReBAC: Choosing the Right Authorization Model — Permit.io](https://www.permit.io/blog/rbac-vs-abac-and-rebac-choosing-the-right-authorization-model)_

### External Authorization Libraries and Services

| Library/Service | Model | .NET Support | Relevance to Hexalith |
|---|---|---|---|
| **Casbin.NET** | ACL, RBAC, ABAC, RBAC-with-domains | Native .NET (NuGet v2.19.2) | **High** — built-in multi-tenant domain support, policy file-based, no external infra |
| **OpenFGA** (Auth0/Okta) | ReBAC (Zanzibar-inspired) | gRPC/.NET SDK | Medium — powerful but requires running an external service |
| **SpiceDB** (AuthZed) | ReBAC (Zanzibar-inspired) | gRPC client | Medium — excellent for complex hierarchies, but heavy for command-level auth |
| **Oso** | Declarative (Polar language) | REST API / embedded | Low — SaaS pricing model, external dependency |
| **Permit.io** | RBAC/ABAC/ReBAC via OPA | REST API | Low — cloud service, adds latency and dependency |
| **AuthPermissions.AspNetCore** | Permission + multi-tenant | Native .NET (NuGet v10.0) | **High** — admin services, hierarchical tenants, MIT license |

_Sources: [Casbin.NET — GitHub](https://github.com/casbin/Casbin.NET), [RBAC with Domains — Casbin](https://casbin.org/docs/rbac-with-domains/), [AuthPermissions.AspNetCore — GitHub](https://github.com/JonPSmith/AuthPermissions.AspNetCore), [OpenFGA](https://openfga.dev/), [SpiceDB — GitHub](https://github.com/authzed/spicedb)_

### MediatR Pipeline Behaviors for Command Authorization

In CQRS architectures, authorization can also be enforced at the **command pipeline level** using MediatR's `IPipelineBehavior<TRequest, TResponse>`. This is a cross-cutting concern pattern where:

- An `AuthorizationBehavior<TRequest, TResponse>` intercepts every command before it reaches the handler
- The behavior inspects the command metadata (tenant, domain, command type) and the current user's claims
- Authorization failures short-circuit the pipeline before any state changes occur

This pattern is widely adopted in .NET CQRS implementations and complements (or replaces) endpoint-level authorization — particularly valuable when commands arrive through multiple entry points (API, message bus, background jobs).

The **`MediatR.Behaviors.Authorization`** NuGet package (v12.5.0) provides a ready-made implementation with `AbstractRequestAuthorizer<TRequest>`, per-command authorizer classes, and automatic assembly scanning.

_Sources: [CQRS Validation with MediatR Pipeline — Milan Jovanovic](https://www.milanjovanovic.tech/blog/cqrs-validation-with-mediatr-pipeline-and-fluentvalidation), [MediatR.Behaviors.Authorization — GitHub](https://github.com/AustinDavies/MediatR.Behaviors.Authorization), [Implementing the microservice application layer — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-application-layer-implementation-web-api)_

### Authentication Infrastructure (.NET 10)

- **`Microsoft.AspNetCore.Authentication.JwtBearer`** v10.0.2 — JWT token validation for API endpoints
- **Microsoft.Identity.Web** — Integration with Microsoft Entra ID, encrypted token caching and Key Vault support
- **Passkey support** — Built-in passkey registration/authentication in .NET 10
- **Pushed Authorization Requests (PAR)** — .NET 9+, moves auth parameters to back-channel
- **Authentication/Authorization metrics** — .NET 10 adds telemetry for sign-in events

_Sources: [.NET 10 Authentication and Authorization — Auth0](https://auth0.com/blog/authentication-authorization-enhancements-dotnet-10/), [What's new in ASP.NET Core .NET 9 — Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-9.0?view=aspnetcore-10.0)_

---

## 3. Integration Patterns: Authorization in the Command Pipeline

### Enforcement Point 1: API Endpoint Authorization

The first enforcement layer sits at the HTTP boundary.

**Minimal API Endpoint Filters (.NET 7+)**: `AddEndpointFilter` intercepts requests before the endpoint handler executes, running in FIFO order. For Hexalith, a custom endpoint filter extracts tenant, domain, and command type from the request and performs coarse-grained authorization.
_Source: [Filters in Minimal API apps — Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/min-api-filters?view=aspnetcore-9.0)_

**Policy-Based `[Authorize]` Attributes**: For controller-based endpoints, `[Authorize(Policy = "...")]` triggers the authorization middleware. With `IAuthorizationRequirementData` (.NET 8+), custom attributes carry requirement data directly.
_Source: [Custom authorization policies with IAuthorizationRequirementData — Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/iard?view=aspnetcore-9.0)_

**Key design decision**: Endpoint-level authorization handles coarse-grained checks (tenant membership, basic role). Fine-grained command-type authorization belongs in the pipeline.

### Enforcement Point 2: Command Pipeline Authorization (MediatR)

The second enforcement layer sits inside the CQRS dispatch pipeline, providing command-specific authorization.

**`IPipelineBehavior<TRequest, TResponse>`** intercepts every command before the handler executes. An `AuthorizationBehavior` inspects the command object, extracts authorization metadata, and evaluates against the current user's claims. This runs after endpoint authorization but before any state changes.

**Custom implementation pattern**:
1. Command implements `IRequireAuthorization` with `TenantId`, `Domain`, `CommandType` properties
2. Pipeline behavior reads these, evaluates against `ClaimsPrincipal` claims
3. Short-circuits with `UnauthorizedAccessException` on failure

_Sources: [Creating a Basic Authorization Pipeline with MediatR — Austin Davies](https://medium.com/@austin.davies0101/creating-a-basic-authorization-pipeline-with-mediatr-and-asp-net-core-c257fe3cc76b), [Handling Authorization in Clean Architecture with MediatR — Level Up Coding](https://levelup.gitconnected.com/handling-authorization-in-clean-architecture-with-asp-net-core-and-mediatr-6b91eeaa4d15)_

### Identity and Claims Integration

**JWT Bearer Token Claims** encode authorization dimensions:
- `tenant_id` / `tid` — tenant membership (single or multiple)
- `domain_access` — permitted domains (e.g., `orders`, `inventory`)
- `command_permissions` — command types or wildcard patterns (`orders:*`, `orders:PlaceOrder`)
- Standard `role` claims for backward compatibility

**`IClaimsTransformation`** enriches the `ClaimsPrincipal` after authentication but before authorization — the integration point where Hexalith-specific permission data from a local store gets attached to the user identity.

**`JwtBearerEvents.OnTokenValidated`** is an alternative hook point for adding custom claims during token validation.

_Sources: [Claims-based authorization — Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/claims?view=aspnetcore-8.0), [Master Claims Transformation — Milan Jovanovic](https://www.milanjovanovic.tech/blog/master-claims-transformation-for-flexible-aspnetcore-authorization), [Adding custom claims during authentication — Joonas W](https://joonasw.net/view/adding-custom-claims-aspnet-core-2)_

### Multi-Tenant Resolution

**Tenant resolution strategies** (ordered by common usage for APIs):
1. **JWT claim** — `tid` or custom `tenant_id` claim (most common)
2. **Request header** — `X-Tenant-Id` (useful for service-to-service calls)
3. **Route/path segment** — `/api/{tenantId}/commands` (explicit, URL-based)
4. **Subdomain** — `acme.api.hexalith.com` (less common for APIs)

A `TenantResolutionMiddleware` runs early in the pipeline, resolves the tenant, and stores it in a scoped `ITenantContext` service.
_Sources: [Multi-tenant .NET Core — Tenant Resolution — Michael McKenna](https://michael-mckenna.com/multi-tenant-asp-dot-net-core-application-tenant-resolution), [Multi-tenant middleware pipelines — Ben Foster](https://benfoster.io/blog/aspnet-core-multi-tenant-middleware-pipelines/)_

### API Gateway Integration (YARP)

YARP provides tenant-aware routing and authorization for distributed deployments:
- Per-route authorization policies integrating with ASP.NET Core middleware
- Claims-based routing of tenants to different backend clusters
- Credential passthrough (tokens, cookies, API keys)

_Sources: [API Gateway Authentication with YARP — Milan Jovanovic](https://www.milanjovanovic.tech/blog/implementing-api-gateway-authentication-with-yarp), [Claims-based routing for SaaS — Microsoft Tech Community](https://techcommunity.microsoft.com/blog/fasttrackforazureblog/claims-based-routing-for-saas-solutions/3865707)_

### Event-Driven Integration: Authorization and Event Sourcing

- **Command acceptance**: Authorization enforced _before_ events are produced
- **Event replay**: Events are _not_ re-authorized during replay — they represent facts
- **Read-side projections**: Query authorization is a separate concern
- **Event subscriptions**: Sagas must preserve originating user's authorization context or use elevated service identity

_Sources: [Event Sourcing pattern — Azure Architecture Center](https://learn.microsoft.com/en-us/azure/architecture/patterns/event-sourcing), [CQRS Pattern — Azure Architecture Center](https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs)_

### Complete OAuth 2.0 + JWT Authorization Flow

```
1. Client authenticates with identity provider (Entra ID, Duende IdentityServer, etc.)
2. JWT token includes tenant_id, roles, custom domain_access / command_permissions claims
3. ASP.NET Core validates token via JwtBearerAuthentication middleware
4. IClaimsTransformation enriches claims with Hexalith-specific permission data
5. Endpoint filter or [Authorize] policy performs coarse-grained check
6. MediatR AuthorizationBehavior performs fine-grained tenant + domain + command-type check
7. Command handler executes and produces events
```

_Sources: [Configure JWT bearer authentication — Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0), [Authorization based on Scopes and Claims — Duende](https://docs.duendesoftware.com/identityserver/apis/aspnetcore/authorization/)_

---

## 4. Architectural Patterns and Design

### System Architecture: PDP/PEP Pattern

The ABAC standard's separation of concerns maps directly to ASP.NET Core:

| ABAC Component | Hexalith Mapping | ASP.NET Core Implementation |
|---|---|---|
| **PEP** (Enforcement) | API endpoint filter + MediatR pipeline behavior | `EndpointFilter`, `IPipelineBehavior` |
| **PDP** (Decision) | Authorization service evaluating policies | `IAuthorizationHandler`, `IAuthorizationService` |
| **PIP** (Information) | User permissions store, claims transformation | `IClaimsTransformation`, custom `IPermissionStore` |
| **PAP** (Administration) | Admin API for permission management | Custom admin endpoints |

_Sources: [ABAC — Wikipedia](https://en.wikipedia.org/wiki/Attribute-based_access_control), [ABAC in Message-Driven Architectures — Particular Docs](https://docs.particular.net/architecture/azure/azure-abac-auth)_

### Clean Architecture Layering

```
┌─────────────────────────────────────────────────────┐
│  Presentation Layer (API)                           │
│  - Endpoint filters: coarse-grained tenant check    │
│  - [Authorize] policies on endpoints                │
├─────────────────────────────────────────────────────┤
│  Application Layer (CQRS Pipeline)                  │
│  - AuthorizationBehavior<TRequest, TResponse>       │
│  - Per-command authorizers (tenant+domain+type)     │
│  - ICurrentUserService (claims accessor)            │
├─────────────────────────────────────────────────────┤
│  Domain Layer                                       │
│  - Command definitions with authorization metadata  │
│  - IRequireAuthorization marker interface            │
├─────────────────────────────────────────────────────┤
│  Infrastructure Layer                               │
│  - IPermissionStore implementation (DB queries)     │
│  - IClaimsTransformation implementation             │
│  - Token validation, JWT configuration              │
└─────────────────────────────────────────────────────┘
```

_Sources: [Clean Architecture & CQRS in .NET Core — Apriorit](https://www.apriorit.com/dev-blog/783-web-clean-architecture-and-cqrs-in-net-core-apps), [Authorization in Clean Architecture with MediatR — Level Up Coding](https://levelup.gitconnected.com/handling-authorization-in-clean-architecture-with-asp-net-core-and-mediatr-6b91eeaa4d15)_

### Permission Data Model: Tenant x Domain x CommandType

**Recommended: Hybrid Approach** — JWT claims for coarse checks, cached permission store for fine-grained evaluation.

**Approach A: Claims-Based (Stateless)**
```
permissions: [
  "tenant:acme/domain:orders/command:*",
  "tenant:acme/domain:inventory/command:AdjustStock",
  "tenant:globex/domain:*/command:*"
]
```
Pros: No DB lookup, fast evaluation. Cons: Token size growth, refresh required on changes.

**Approach B: Permission Store (Stateful)**
```
PermissionAssignment:
  - UserId / RoleId
  - TenantId (FK or wildcard "*")
  - DomainId (FK or wildcard "*")
  - CommandType (string pattern or wildcard "*")
  - Effect (Allow / Deny)
```
Pros: Fine-grained, real-time changes, deny rules. Cons: DB dependency (mitigated by caching).

_Sources: [A better way to handle ASP.NET Core authorization — The Reformed Programmer](https://www.thereformedprogrammer.net/a-better-way-to-handle-asp-net-core-authorization-six-months-on/), [Best Practices for Multi-Tenant Authorization — Permit.io](https://www.permit.io/blog/best-practices-for-multi-tenant-authorization)_

### Hierarchical Tenant Architecture

AuthPermissions.AspNetCore demonstrates hierarchical tenants using a `DataKey` pattern:
- Each tenant encodes its hierarchy position (e.g., `"org1."`, `"org1.dept1."`)
- `StartsWith` comparison grants parent access to sub-tenant data
- EF Core global query filters enforce isolation at the data layer

_Sources: [Hierarchical multi-tenant apps — The Reformed Programmer](https://www.thereformedprogrammer.net/building-asp-net-core-and-ef-core-hierarchical-multi-tenant-apps/), [Multi-tenant explained — AuthPermissions Wiki](https://github.com/JonPSmith/AuthPermissions.AspNetCore/wiki/Multi-tenant-explained)_

### Defense in Depth: Six-Layer Enforcement

```
Layer 1: Network/Gateway    → YARP: tenant route validation, rate limiting
Layer 2: Authentication     → JWT validation, token expiry, audience check
Layer 3: Endpoint Auth      → [Authorize] policy: coarse tenant + role check
Layer 4: Pipeline Auth      → MediatR behavior: tenant + domain + command-type check
Layer 5: Domain Validation  → Command handler: business rule validation
Layer 6: Data Layer         → EF Core global query filter: tenant isolation on writes
```

_Source: [Architectural Considerations for Identity in Multitenant Solutions — Azure Architecture Center](https://learn.microsoft.com/en-us/azure/architecture/guide/multitenant/considerations/identity)_

### Design Principles Applied

- **Single Responsibility**: Each handler evaluates one dimension (tenant OR domain OR command-type)
- **Open/Closed**: New command types get authorization by implementing `IRequireAuthorization`
- **Dependency Inversion**: Handlers depend on `IPermissionStore` abstraction
- **Schema Separation**: Permission assignments live in a dedicated schema, not in domain entities

---

## 5. Implementation Approaches and Concrete Patterns

### Phased Implementation Strategy

**Phase 1 — Foundation**
1. Define `IRequireAuthorization` with `TenantId`, `Domain`, `CommandType`
2. Implement `IClaimsTransformation` for Hexalith permission claims
3. Create `IPermissionStore` abstraction + in-memory implementation
4. Build `AuthorizationBehavior<TRequest, TResponse>` pipeline behavior

**Phase 2 — Policy Engine**
1. Implement `TenantAuthorizationRequirement` + handler
2. Implement `DomainAuthorizationRequirement` + handler
3. Implement `CommandTypeAuthorizationRequirement` + handler
4. Implement `IAuthorizationPolicyProvider` for dynamic per-command policies

**Phase 3 — Persistence and Caching**
1. Database-backed `IPermissionStore` (EF Core or Dapper)
2. Distributed caching with per-tenant keys
3. Cache invalidation on permission changes

**Phase 4 — Observability and Admin**
1. OpenTelemetry traces and metrics for authorization decisions
2. Admin API for permission management
3. Audit logging for failures and permission changes

_Sources: [Custom Authorization Policy Providers — Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/iauthorizationpolicyprovider?view=aspnetcore-9.0), [Dynamic Authorization Policies — Joao Grassi](https://blog.joaograssi.com/posts/2021/asp-net-core-protecting-api-endpoints-with-dynamic-policies/)_

### Pattern 1: Command Authorization Metadata

```csharp
public interface IRequireAuthorization
{
    string TenantId { get; }
    string Domain { get; }
    string CommandType => GetType().Name; // convention-based default
}

public record PlaceOrderCommand(
    string TenantId,
    string Domain,
    Guid OrderId
) : IRequest<Result>, IRequireAuthorization;
```

### Pattern 2: MediatR Authorization Pipeline Behavior

```csharp
public class AuthorizationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequireAuthorization
{
    private readonly IAuthorizationService _authService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        var user = _httpContextAccessor.HttpContext!.User;
        var requirement = new CommandAuthorizationRequirement(
            request.TenantId, request.Domain, request.CommandType);

        var result = await _authService.AuthorizeAsync(user, requirement);
        if (!result.Succeeded)
            throw new UnauthorizedAccessException(
                $"Access denied: {request.CommandType} in {request.Domain}@{request.TenantId}");

        return await next();
    }
}
```

### Pattern 3: Dynamic Policy Provider

```csharp
public class CommandPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith("Command:"))
        {
            var parts = policyName.Split(':');
            var policy = new AuthorizationPolicyBuilder()
                .AddRequirements(new CommandAuthorizationRequirement(
                    parts[1], parts[2], parts[3]))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }
        return _fallback.GetPolicyAsync(policyName);
    }
}
```

_Sources: [Complex Authorization Policy Setups — Manuel Roemer](https://manuelroemer.com/blog/5-asp-net-core-authorization-policy-provider/), [Authorization Pipeline with MediatR — Austin Davies](https://medium.com/@austin.davies0101/creating-a-basic-authorization-pipeline-with-mediatr-and-asp-net-core-c257fe3cc76b)_

### Testing Strategy

**Unit tests**: Each `IAuthorizationHandler` tested independently with mock `ClaimsPrincipal`, verifying `context.Succeed()` for valid claims and wildcard matching.

**Integration tests with WebApplicationFactory**:
```csharp
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[] {
            new Claim("tenant_id", "test-tenant"),
            new Claim("domain_access", "orders"),
            new Claim("command_permissions", "orders:PlaceOrder")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
```

_Sources: [Mocking Authentication in Integration Tests — Muhammad Azeez](https://mazeez.dev/posts/auth-in-integration-tests/), [Testing with fake JWT tokens — Renato Golia](https://renatogolia.com/2025/08/01/testing-aspnet-core-endpoints-with-fake-jwt-tokens-and-webapplicationfactory/)_

### Build vs. Buy Analysis

| Option | Pros | Cons | Recommendation |
|---|---|---|---|
| **Built-in ASP.NET Core** | Zero dependencies, full control | Build from scratch | **Primary choice** |
| **AuthPermissions.AspNetCore** (v10.0) | Multi-tenant + admin services, MIT | Opinionated | **Evaluate** for Phase 1-2 |
| **MediatR.Behaviors.Authorization** (v12.5) | Ready-made pipeline auth | May be too simple | **Reference** pattern |
| **Casbin.NET** (v2.19) | RBAC-with-domains, hybrid | Different paradigm | **Reserve** as fallback |

_Sources: [AuthPermissions.AspNetCore — GitHub](https://github.com/JonPSmith/AuthPermissions.AspNetCore), [MediatR.Behaviors.Authorization — NuGet](https://www.nuget.org/packages/MediatR.Behaviors.Authorization)_

---

## 6. Performance, Scalability, and Security

### Performance Patterns

**Permission Caching**: Cache per user with tenant-scoped keys in `IDistributedCache`. Short TTLs (30s-5min). Per-tenant partitioning to prevent cross-tenant leaks.

**Claims Preloading**: `IClaimsTransformation` loads all permissions once per request, avoiding multiple DB round-trips.

**Short-Circuit Evaluation**: Tenant (JWT claim, no DB) -> Domain (cache lookup) -> CommandType (most specific). Each gate avoids expensive lookups for users who fail earlier.

**Token Cache Eviction**: Sliding + absolute expiration to bound cache growth in multi-tenant environments.

_Sources: [Distributed caching — Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed?view=aspnetcore-7.0), [Token cache serialization — Microsoft Learn](https://learn.microsoft.com/en-us/entra/msal/dotnet/how-to/token-cache-serialization)_

### Observability

- .NET 10 natively emits authentication/authorization metrics via OpenTelemetry
- Custom `Activity` spans with `tenant_id`, `domain`, `command_type`, `result` tags
- `authorization.decisions` counter metric with dimensions for monitoring
- Authorization failures logged at `Warning` level with full context

_Sources: [.NET Observability with OpenTelemetry — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-with-otel), [ASP.NET Core metrics — Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/log-mon/metrics/metrics?view=aspnetcore-10.0)_

### Risk Assessment

| Risk | Impact | Mitigation |
|---|---|---|
| Permission model too rigid | High | Wildcard patterns + convention-based naming |
| Token size explosion | Medium | Hybrid: JWT for tenant/role, DB for fine-grained |
| Cache inconsistency | Medium | Short TTL + event-driven invalidation |
| Performance overhead | Low-Medium | Cache + preloading + short-circuit cascade |
| Cross-tenant data leak | **Critical** | Per-tenant cache keys + EF Core filters + integration tests |
| Single `IAuthorizationPolicyProvider` | Low | Merge with `DefaultAuthorizationPolicyProvider` fallback |

---

## 7. Strategic Recommendations and Roadmap

### Implementation Roadmap

1. **Start with ASP.NET Core built-in authorization** — `IAuthorizationRequirement`, `AuthorizationHandler<T>`, policy-based
2. **Implement MediatR `AuthorizationBehavior`** as the primary enforcement point
3. **Use `IClaimsTransformation`** to bridge JWT claims with Hexalith's permission store
4. **Implement `IAuthorizationPolicyProvider`** for dynamic per-command policies
5. **Add caching** with per-tenant partitioning early — not as an afterthought
6. **Evaluate AuthPermissions.AspNetCore** for accelerating admin/permission management

### Technology Stack Recommendations

- **Core**: ASP.NET Core 10 built-in authorization framework (zero additional dependencies)
- **Pipeline**: MediatR `IPipelineBehavior` for command-level enforcement
- **Identity**: JWT Bearer authentication with custom claims
- **Caching**: `IDistributedCache` (Redis or in-memory) with per-tenant keys
- **Observability**: OpenTelemetry with custom authorization spans and metrics
- **Testing**: `WebApplicationFactory` + custom `TestAuthHandler`
- **Optional**: AuthPermissions.AspNetCore for multi-tenant admin features

### Success Metrics

- Authorization decision latency < 5ms (cached), < 50ms (cold)
- Zero cross-tenant permission leaks in integration test suite
- 100% command coverage (enforced by pipeline behavior on `IRequireAuthorization`)
- Permission changes effective within cache TTL (configurable, default 60s)
- All failures logged with full context via OpenTelemetry

---

## 8. Source Documentation and Methodology

### Primary Sources

- [Policy-based authorization in ASP.NET Core — Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies?view=aspnetcore-10.0)
- [Resource-based authorization — Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resourcebased?view=aspnetcore-10.0)
- [Custom authorization policies with IAuthorizationRequirementData — Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/iard?view=aspnetcore-9.0)
- [Custom Authorization Policy Providers — Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/iauthorizationpolicyprovider?view=aspnetcore-9.0)
- [CQRS Pattern — Azure Architecture Center](https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs)
- [Event Sourcing Pattern — Azure Architecture Center](https://learn.microsoft.com/en-us/azure/architecture/patterns/event-sourcing)
- [Architectural Considerations for Identity in Multitenant Solutions — Azure Architecture Center](https://learn.microsoft.com/en-us/azure/architecture/guide/multitenant/considerations/identity)
- [Implementing the microservice application layer — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-application-layer-implementation-web-api)

### Community and Library Sources

- [A better way to handle ASP.NET Core authorization — Jon P Smith / The Reformed Programmer](https://www.thereformedprogrammer.net/a-better-way-to-handle-asp-net-core-authorization-six-months-on/)
- [AuthPermissions.AspNetCore — GitHub](https://github.com/JonPSmith/AuthPermissions.AspNetCore)
- [Master Claims Transformation — Milan Jovanovic](https://www.milanjovanovic.tech/blog/master-claims-transformation-for-flexible-aspnetcore-authorization)
- [API Gateway Authentication with YARP — Milan Jovanovic](https://www.milanjovanovic.tech/blog/implementing-api-gateway-authentication-with-yarp)
- [Custom authorisation policies and requirements — Andrew Lock](https://andrewlock.net/custom-authorisation-policies-and-requirements-in-asp-net-core/)
- [Handling Authorization in Clean Architecture with MediatR — Austin Davies](https://levelup.gitconnected.com/handling-authorization-in-clean-architecture-with-asp-net-core-and-mediatr-6b91eeaa4d15)
- [MediatR.Behaviors.Authorization — GitHub](https://github.com/AustinDavies/MediatR.Behaviors.Authorization)
- [Dynamic Authorization Policies — Joao Grassi](https://blog.joaograssi.com/posts/2021/asp-net-core-protecting-api-endpoints-with-dynamic-policies/)
- [Casbin.NET — GitHub](https://github.com/casbin/Casbin.NET)
- [RBAC with Domains — Casbin](https://casbin.org/docs/rbac-with-domains/)

### External Analysis Sources

- [RBAC vs ABAC & ReBAC — Permit.io](https://www.permit.io/blog/rbac-vs-abac-and-rebac-choosing-the-right-authorization-model)
- [Best Practices for Multi-Tenant Authorization — Permit.io](https://www.permit.io/blog/best-practices-for-multi-tenant-authorization)
- [ABAC in Message-Driven Architectures — Particular Docs](https://docs.particular.net/architecture/azure/azure-abac-auth)
- [.NET 10 Authentication and Authorization — Auth0](https://auth0.com/blog/authentication-authorization-enhancements-dotnet-10/)
- [Multi-tenant .NET Core Application — Michael McKenna](https://michael-mckenna.com/multi-tenant-asp-dot-net-core-application-tenant-resolution)

### Research Quality

- **Source Verification**: All technical claims verified against current public sources
- **Confidence Level**: High — based on Microsoft official documentation, established community authors, and verified NuGet package documentation
- **Limitations**: Implementation patterns are conceptual blueprints; actual Hexalith integration will require adaptation to existing codebase conventions
- **Methodology**: Structured web research with parallel search execution, cross-source validation, and progressive synthesis

---

**Technical Research Completion Date:** 2026-02-11
**Document Length:** Comprehensive coverage across 8 sections
**Source Verification:** All facts cited with current sources
**Confidence Level:** High — multiple authoritative sources

_This technical research document serves as the authoritative reference for implementing multi-dimensional command authorization in Hexalith.EventStore, providing strategic insights and concrete implementation patterns for the development team._
