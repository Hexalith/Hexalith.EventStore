# Story 6.6 loader policy options

**Status:** The owner selected option 3 on 2026-10-06 by replying “do recommended”.
The [approved amendment](../../story-6-6-trusted-code-amendment.md) records its trust
assumptions and qualification requirements. M1 readiness, V2 writes and
proof-dependent consumers remain fenced.

The [qualification probe](dependency-loader-qualification-2026-10-05.md) showed
that explicit managed loads could execute effects after readiness invalidation,
and explicit native loads could bypass both tested resolver hooks. This is a
failure of the tested candidate, not proof that every possible policy fails.
The [decision record](dependency-loader-decision.md) requires an owner-selected
policy for explicit loads and nested effects, including its trust assumptions.

## Option 1: Keep activation fenced

Continue compatible V1 improvements and dormant implementation. Do not admit
catalog routes or enable V2/proof-dependent consumers until an enforceable
policy and its qualification evidence exist.

**Pros:** Lowest implementation and operating cost. Preserves the current
assurance contract and permits independently verified safety improvements.

**Cons:** Event-evolution activation remains unavailable. This postpones the
trust decision rather than resolving it.

**Use when:** The deployment's code ownership or need for untrusted extensions
has not been established. This remains the current operating posture.

## Option 2: Execute extensions in restricted workers

Keep admission, durable writes and authorized external effects in a trusted
host. Give workers bounded input and validate their output. Restrict worker
credentials, filesystem access and network access using operating-system
permissions or a qualified container boundary. Assess whether the existing
domain-service process can supply this boundary before adding another service.

**Pros:** Stronger containment for untrusted implementations. A compromised
worker need not inherit the host's state-store credentials or write authority.

**Cons:** Adds process communication, deployment, timeout, restart and recovery
work. A separate process alone supplies no isolation. Restricted workers also
do not automatically prove that every executing dependency belonged to a
pinned graph; that assurance requires its own design and qualification.

**Use when:** Running code outside the deployer's review and release control is
an actual requirement. Specify the isolation and graph-assurance contracts
before implementing the boundary.

## Option 3: Admit reviewed, trusted application code

Treat catalog implementations and their dependencies as trusted application
code. Admit immutable artifacts only after checking exact manifests, hashes
and serving-peer pins. Require declared dependencies; prohibit undeclared
explicit managed/native loads through the reviewed code policy. Any permitted
dynamic-loading path needs a declared, reviewed and qualified exception.

Use loader observations to detect violations and fence further admission.
Do not describe those observations as prevention of effects that have already
executed. Hashes establish artifact identity, not safe behavior.

**Pros:** Fits a deployment where the team owns, reviews and releases every
implementation. Keeps runtime and operating complexity low and preserves the
usefulness of pinned artifacts and dependency checks.

**Cons:** Trust extends to the dependency chain. Bugs or compromised admitted
code can violate the policy. This is not confinement of hostile code and
cannot retain the tested candidate's stronger before-effect guarantee by
changing its label. The assurance contract needs explicit owner ratification.

**Use when:** All admitted implementations are controlled and reviewed by the
deployer, and tenant input cannot introduce executing code.

## Recommendation and selected decision

Recommend option 3 for controlled, reviewed modules, with option 1 remaining
in force until the revised assurance contract is recorded and qualified.
Defer option 2 unless untrusted extensions are required.

The proposed decision is to trust admitted code to honor its declared graph;
verify immutable artifact identity before admission; prohibit undeclared
explicit loads; and use observations for detection and subsequent capability
loss. It explicitly makes no hostile-code confinement or universal
before-effect refusal claim. If those stronger guarantees are required, retain
the fence while designing and qualifying a stronger boundary.

The owner's approval authorizes the assurance/spec amendment and implementation
under it. Authoritative domain manifests, catalog qualification and Dapr
production evidence remain separate requirements before activation.

## Primary technical sources

- [AssemblyLoadContext](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.loader.assemblyloadcontext?view=net-10.0): dependency scopes share the process's permissions and supply no security boundary.
- [NativeLibrary.Load](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.nativelibrary.load?view=net-10.0): the simple overload wraps the operating-system loader.
- [.NET secure coding guidance](https://learn.microsoft.com/en-us/dotnet/standard/security/secure-coding-guidelines): use operating-system or virtualization boundaries for untrusted code.
