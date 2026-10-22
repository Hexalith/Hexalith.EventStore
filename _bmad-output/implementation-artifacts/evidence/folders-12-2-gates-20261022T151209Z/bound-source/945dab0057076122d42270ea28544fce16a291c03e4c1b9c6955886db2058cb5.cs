
using System.Reflection;

using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Aggregates;
using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Testing.Compliance;
/// <summary>
/// Test-time helpers that convert the runtime-only Apply-method obligations of
/// <see cref="ITerminatable"/> aggregate states into Tier 1 assertions.
/// </summary>
/// <remarks>
/// <see cref="ITerminatable"/> carries a runtime-only constraint declared in its own
/// remarks: any state implementing the interface MUST also declare a no-op
/// <c>Apply(<see cref="AggregateTerminated"/>)</c> method, because
/// <see cref="AggregateTerminated"/> rejection events are persisted to the event
/// stream and replayed during actor rehydration. Domain teams that omit the method
/// pass first-close and first-rejection tests, then fail only after actor
/// deactivation and reactivation when the persisted rejection event replays.
/// This helper turns that latent failure mode into a deterministic Tier 1
/// assertion. Failures throw the same <see cref="MissingApplyMethodException"/>
/// that aggregate rehydration throws at runtime, so test-time and runtime
/// failure ergonomics are identical.
/// <para>
/// Recommended: call this from every aggregate state's primary test class, even
/// when the state does not currently implement <see cref="ITerminatable"/>. The
/// helper is a no-op for non-terminatable states, so the call activates
/// automatically the moment the interface is later added.
/// </para>
/// </remarks>
public static class TerminatableComplianceAssertions {
    /// <summary>
    /// Asserts that <typeparamref name="TState"/> satisfies the
    /// <see cref="ITerminatable"/> Apply-method contract: a public instance
    /// <c>void Apply(<see cref="AggregateTerminated"/>)</c> method must exist.
    /// No-op for state types that do not implement <see cref="ITerminatable"/>.
    /// </summary>
    /// <typeparam name="TState">The aggregate state class to verify.</typeparam>
    /// <exception cref="MissingApplyMethodException">
    /// Thrown when <typeparamref name="TState"/> implements
    /// <see cref="ITerminatable"/> but has no public instance
    /// <c>void Apply(<see cref="AggregateTerminated"/>)</c> method.
    /// </exception>
    public static void AssertTerminatableCompliance<TState>()
        where TState : class {
        Type stateType = typeof(TState);
        if (!typeof(ITerminatable).IsAssignableFrom(stateType)) {
            return;
        }

        MethodInfo? applyMethod = stateType.GetMethod(
            name: "Apply",
            bindingAttr: BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: new[] { typeof(AggregateTerminated) },
            modifiers: null);

        if (applyMethod is null || applyMethod.ReturnType != typeof(void)) {
            throw new MissingApplyMethodException(
                stateType: stateType,
                eventTypeName: nameof(AggregateTerminated));
        }
    }
}
