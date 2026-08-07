// Story 4.3 resolver fixtures: one state (and matching projection) per I/O matrix row, so each row is
// exercised on both the rehydrate path and the projection path against the same Apply shape.
//
// Every event type is written `global::`-qualified. The depth fixtures require a namespace whose trailing
// segment repeats another root namespace (EsFixOuter.EsFixDeep vs EsFixDeep); `global::` makes it
// impossible for a future fixture added to an enclosing namespace to silently rebind these names.
using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.Client.Tests.Aggregates.ApplyResolverFixtures;

/// <summary>No-op command used to drive an aggregate through the snapshot-aware envelope replay path.</summary>
internal sealed record TouchAggregate;

/// <summary>State whose only Apply is addressed by its exact full name.</summary>
internal sealed class ExactHitState {
    public string? Applied { get; private set; }

    public void Apply(global::EsFixA.ItemAdded e) => Applied = "EsFixA.ItemAdded:" + e.Name;
}

internal sealed class ExactHitProjection : EventStoreProjection<ExactHitState>;

/// <summary>State whose two event short names are suffixes of one another.</summary>
internal sealed class SuffixCollisionState {
    public string? Applied { get; private set; }

    public void Apply(global::EsFixA.ItemAdded e) => Applied = "ItemAdded:" + e.Name;

    public void Apply(global::EsFixA.SubItemAdded e) => Applied = "SubItemAdded:" + e.Name;
}

internal sealed class SuffixCollisionProjection : EventStoreProjection<SuffixCollisionState>;

/// <summary>State that can only apply the shorter of the two colliding names.</summary>
internal sealed class OnlyItemAddedState {
    public string? Applied { get; private set; }

    public void Apply(global::EsFixA.ItemAdded e) => Applied = "ItemAdded:" + e.Name;
}

internal sealed class OnlyItemAddedProjection : EventStoreProjection<OnlyItemAddedState>;

/// <summary>State with two distinct event types sharing one CLR short name.</summary>
internal sealed class AmbiguousShortState {
    public string? Applied { get; private set; }

    public void Apply(global::EsFixA.ItemAdded e) => Applied = "EsFixA.ItemAdded:" + e.Name;

    public void Apply(global::EsFixB.ItemAdded e) => Applied = "EsFixB.ItemAdded:" + e.Name;
}

internal sealed class AmbiguousShortProjection : EventStoreProjection<AmbiguousShortState>;

/// <summary>
/// Aggregate over <see cref="AmbiguousShortState"/>. Exists so ambiguity can be reached through the
/// snapshot-aware <c>EventEnvelope</c> path, which is the only path carrying message and aggregate ids.
/// </summary>
internal sealed class AmbiguousShortAggregate : EventStoreAggregate<AmbiguousShortState> {
    public static DomainResult Handle(TouchAggregate command, AmbiguousShortState? state) => DomainResult.NoOp();
}

/// <summary>State whose Apply takes a base type that a nested, suffix-anchored event type derives from.</summary>
internal sealed class BaseNotificationState {
    public string? Applied { get; private set; }

    public void Apply(global::EsFixA.BaseNotification e) => Applied = "BaseNotification:" + e.Name;
}

internal sealed class BaseNotificationProjection : EventStoreProjection<BaseNotificationState>;

/// <summary>State whose two candidates match a stored name at different namespace depths.</summary>
internal sealed class DepthState {
    public string? Applied { get; private set; }

    public void Apply(global::EsFixDeep.Foo e) => Applied = "EsFixDeep.Foo:" + e.Name;

    public void Apply(global::EsFixOuter.EsFixDeep.Foo e) => Applied = "EsFixOuter.EsFixDeep.Foo:" + e.Name;
}

internal sealed class DepthProjection : EventStoreProjection<DepthState>;

/// <summary>State whose two candidates can only ever match a stored name at the same depth.</summary>
internal sealed class EqualDepthState {
    public string? Applied { get; private set; }

    public void Apply(global::EsFixA.Foo e) => Applied = "EsFixA.Foo:" + e.Name;

    public void Apply(global::EsFixB.Foo e) => Applied = "EsFixB.Foo:" + e.Name;
}

internal sealed class EqualDepthProjection : EventStoreProjection<EqualDepthState>;

/// <summary>
/// State where one event type lives outside any namespace, so its full name and short name are the same
/// string, and another type in a namespace contributes that same string as a short name.
/// </summary>
internal sealed class GlobalNamespaceCollisionState {
    public string? Applied { get; private set; }

    public void Apply(global::EsFixGlobalCollider e) => Applied = "global:" + e.Name;

    public void Apply(global::EsFixA.EsFixGlobalCollider e) => Applied = "EsFixA:" + e.Name;
}

internal sealed class GlobalNamespaceCollisionProjection : EventStoreProjection<GlobalNamespaceCollisionState>;

/// <summary>State with a single global-namespace event type, used to prove the de-duplication path.</summary>
internal sealed class GlobalNamespaceOnlyState {
    public string? Applied { get; private set; }

    public void Apply(global::EsFixGlobalCollider e) => Applied = "global:" + e.Name;
}

/// <summary>State whose Apply takes a closed generic event type.</summary>
internal sealed class GenericEventState {
    public string? Applied { get; private set; }

    public void Apply(global::EsFixA.Wrapper<global::EsFixA.ItemAdded> e) => Applied = "Wrapper:" + e.Name;
}

/// <summary>State whose only Apply is an open generic overload; it must never be registered.</summary>
internal sealed class GenericApplyState {
    public string? Applied { get; private set; }

    public void Apply<TEvent>(TEvent e) => Applied = typeof(TEvent).Name + (e is null ? "?" : string.Empty);
}

/// <summary>State with a by-ref Apply overload alongside a normal one; only the normal one registers.</summary>
internal sealed class ByRefApplyState {
    public string? Applied { get; private set; }

    public void Apply(global::EsFixA.ItemAdded e) => Applied = "ItemAdded:" + e.Name;

    public void Apply(in global::EsFixA.SubItemAdded e) => Applied = "SubItemAdded:" + e.Name;
}

/// <summary>Base half of a base-plus-new-hiding pair, which yields two Apply methods for one CLR type.</summary>
internal class HidingBaseState {
    public string? Applied { get; protected set; }

    public void Apply(global::EsFixA.ItemAdded e) => Applied = "base:" + e.Name;
}

/// <summary>Derived half that hides the base Apply; resolution must fail loudly rather than pick one.</summary>
internal sealed class HidingDerivedState : HidingBaseState {
    public new void Apply(global::EsFixA.ItemAdded e) => Applied = "derived:" + e.Name;
}
