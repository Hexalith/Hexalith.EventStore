# Story 5.12: approved initial declarations

Status: approved by the owner in this conversation on 2026-10-26. Prepared 2026-10-26 from EventStore `dcd8b34976d51f447c19b4f84e1198b5950d454d` and the current root-declared Tenants checkout. This decision record amends the parent intent as approved; it does not activate a catalog.

The missing definitions are implementation work we should author. The owner does not need to find an existing metadata document. Source establishes the command behavior and route inventory; the following owner-approved defaults supply the policy choices that source alone cannot authorize.

## Approved decisions

1. **Preserve current MessageId compatibility.** Declare all existing command operations as MessageId v1 initially. The current gateway accepts alphanumeric/hyphen identifiers up to 128 characters; canonical ULIDs remain valid inputs. Do not label existing endpoints v2 merely because some clients generate ULIDs. A deliberate v2 migration remains Story 2.15 work.
2. **Use the Mutation replay tier for all 17 command operations.** This retains a completed command's replayable result for exactly 24 hours. The platform then applies its existing expired-key/tombstone behavior; this proposal does not shorten event history, audit retention, or consumed-key evidence. No inspected command expresses the separate business-commit semantics that would justify the existing seven-calendar-year Commit tier. Revisit any exception explicitly before adopting that command's policy.
3. **Use one versioned intent policy per operation.** Initial descriptor version is 1. Approved identifiers are `hexalith:<domain>:<operation>:intent-v1` for the adapter, `<domain>:<operation>` for its operation, `hexalith:<domain>:<operation>:descriptor-v1` for its schema, and `<domain>:<operation>:policy-v1` for the policy. These stable metadata identifiers are separate from exact wire discriminators and the existing receiving workload operations.
4. **Keep this delivery offline and unactivated.** Author and test the real declarations/adapters, model actual routes, and generate the Development envelope only after parity passes. Do not enable currently unconfigured admission, provision key material, change authorization, rewrite persisted identities, deploy, or activate the catalog. Story 5.13 owns runtime adoption; 2.14 owns platform-scope enforcement/migration.

Owner acceptance of this proposal authorizes implementing these definitions in the owning EventStore/Sample and root-declared Tenants repositories, including the necessary compatible declaration/schema/parity changes. It does not authorize production policy, key provisioning, external publication, Git history changes, or a new operational CLI.

## Command intent inventory

All target identities come from the existing envelope `AggregateIdentity(Tenant, Domain, AggregateId)`, not a replacement identity invented for the catalog. The table lists semantic fields beyond that target. Target-only commands use semantic JSON `{}`. Fields used only for transport or generated outcomes are excluded.

| Domain / operation | Semantic fields beyond target | Existing behavior to preserve |
| --- | --- | --- |
| counter / increment-counter | None | Increment by one; no amount field exists. |
| counter / decrement-counter | None | Decrement by one; zero rejects. |
| counter / reset-counter | None | Zero is a no-op. |
| counter / close-counter | None | Terminates the aggregate; do not infer seven-year replay retention from termination. |
| greeting / send-greeting (policy identifier) | None | Existing wire command is `SendGreeting`; every accepted call emits a greeting. Do not add a nonexistent text field or rename the wire command. |
| tenants / create-tenant | Name, nullable Description | Existing target rejects; timestamp is an outcome. |
| tenants / update-tenant | Name, nullable Description | Null description clears it; unchanged values still emit an update. |
| tenants / disable-tenant | None | Unknown/already-disabled target rejects. |
| tenants / enable-tenant | None | Unknown/already-enabled target rejects. |
| tenants / add-user-to-tenant | UserId, Role | Preserve first-member bootstrap and duplicate-member rejection. |
| tenants / remove-user-from-tenant | UserId | Missing member rejects; current last-owner behavior is unchanged. |
| tenants / change-user-role | UserId, NewRole | Same role is a no-op; current last-owner behavior is unchanged. |
| tenants / set-tenant-configuration | Key, Value | Empty value is valid; same value is a no-op. |
| tenants / remove-tenant-configuration | Key | Missing key rejects. |
| global-administrators / bootstrap-global-admin | Target UserId plus trusted acting subject | Payload user must equal the actor; existing bootstrap caller uses a CLR alias. |
| global-administrators / set-global-administrator | Target UserId plus trusted acting subject | Event attribution and current administrator authority matter. |
| global-administrators / remove-global-administrator | Target UserId plus trusted acting subject | Final administrator removal rejects. |

Approved normalization preserves string values exactly, including case, whitespace and Unicode. It canonicalizes validated roles to their enum names and preserves null versus empty descriptions (for example, an explicit `description-is-null` flag plus a string). Transport casing/default-field behavior must match the existing payload serializer. Redundant payload identifiers that handlers ignore must not silently become target authority; a mismatch rejection would need a separate compatibility decision.

Trusted subject binding must come from the authenticated command context, never arbitrary extensions. The present `IdempotencyIntentCommand` omits that subject, so any additive seam needed to express it must be tested without changing existing authorization. Current grants, timestamps, tokens, message/correlation IDs, clocks and retry metadata are excluded from intent. This declaration work does not authorize bypassing current domain checks when replay is enabled; adoption must prove current authorization before disclosing replay results.

## Technical work established by the source

These are implementation requirements, not questions the owner needs to answer:

- Preserve command aliases. The aggregate dispatcher accepts contract names, CLR short names, and qualified/assembly-qualified forms. The catalog must describe compatibility resolution to a finite set of actual handlers and retain exact wire discriminators. Keep route IDs separate. Do not force legacy callers to change names to satisfy the current lowercase-only validator.
- Represent actual query kinds and targets. There are nine query types: Counter status, six public Tenants queries, and two owner-authority queries. The owner-authority discriminators contain uppercase/dotted names; a computed result can have no projection type. Counter status invokes EventStore's `ProjectionActor.QueryAsync`, whereas Tenants handlers use `tenants/query`. An actor target must not be represented as a nonexistent `sample/query` HTTP endpoint.
- Bind actual projection outputs. Counter provides `counter`; Tenants' explicit dispatcher provides tenant/global-administrator read models and secondary index/audit outputs. Greeting has no projection. Do not invent a Greeting projection or advertise secondary outputs as independently callable dispatch handlers.
- Preserve existing catalog golden bytes and digest protocols while accommodating these real declaration shapes. Use an explicit schema-version change if the representation requires it. Validate actual resolver precedence, handler discovery and the actor path. Missing operational metadata or unbounded/unrepresented fallback behavior must fail the profile completeness check, not manufacture a route.
- Domain semantics belong in their owners' libraries; reusable declaration, alias, encoding and parity machinery belongs in EventStore. Add source drift guards so copied manifests cannot become a second authority.

## Approved Development key-binding metadata

There is currently no checked-in `EventStore:IdempotencyAdmission` configuration and no idempotency secret map in the default local topology. Do not describe one as already deployed.

Approved future, value-free Development binding: consumer `eventstore`, Dapr secret component `secretstore`, logical map `eventstore-idempotency-ring`, map marker `development-v1`, active generation `development-v1`, and map entry `development-v1`. Catalog readable generations are `[development-v1]`; runtime retained-reader options would be empty because they exclude active. Every key-map entry uses `MapKey == Generation`, as required by the existing Dapr provider; `generation` remains reserved for the marker.

These names are approved metadata for an unactivated profile, not existing secrets or permission to rotate/replace keys. Secret provisioning, component topology, real retrieval/readiness, adapter registration and admission enablement remain adoption work. Catalog construction/parity reports must distinguish verified routes and adapter declarations from unverified runtime secret availability. Production OpenBao composition remains outside this proposal.

## Approved platform metadata

Use namespaces `platform:tenants` and `platform:global-administrators`. Approved exact human permissions are `tenants:create`, `tenants:update`, `tenants:disable`, `tenants:enable`, `tenants:add-user`, `tenants:remove-user`, `tenants:change-user-role`, `tenants:set-configuration`, `tenants:remove-configuration`, and `global-administrators:bootstrap`, `global-administrators:set`, `global-administrators:remove`.

These are declarations, not newly granted access. Internal authority queries retain their separate workload/delegation classification. Existing stored `system` identities remain unchanged; enforcing the distinct public platform scope belongs to 2.14.

## Source anchors

- `samples/Hexalith.EventStore.Sample.Contracts/Counter/Commands/` and `samples/Hexalith.EventStore.Sample/Counter/CounterAggregate.cs`.
- `samples/Hexalith.EventStore.Sample/Greeting/Commands/SendGreeting.cs` and `GreetingAggregate.cs` in its parent directory.
- `references/Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Commands/`, `Queries/`, and `Identity/TenantIdentity.cs`; Server `Aggregates/TenantAggregate.cs` and `GlobalAdministratorsAggregate.cs`.
- `src/Hexalith.EventStore/Validation/SubmitCommandRequestValidator.cs`; Client `Aggregates/EventStoreAggregate.cs`; `src/Hexalith.EventStore/Queries/HandlerAwareQueryRouter.cs` and `DaprDomainQueryInvoker.cs`; Server `Queries/DefaultProjectionActorInvoker.cs`.
- `src/Hexalith.EventStore.Contracts/Commands/IdempotencyReplayRetentionTier.cs`; DomainService `IdempotencyCanonicalIntent.cs` and `IdempotencyIntentCommand.cs`; Server `Commands/IdempotencyIntentAdapterRegistry.cs`, `DaprSecretIdempotencyDigestKeyProvider.cs`, and `Configuration/ValidateIdempotencyAdmissionOptions.cs`.

No application code, existing frozen intent, runtime settings, or submodule content was changed in preparing this proposal. Owner acceptance was received as “approve”; implementation and verification follow separately.
