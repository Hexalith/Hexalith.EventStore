
using System.Diagnostics.CodeAnalysis;

using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Aggregates;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Testing.Compliance;

namespace Hexalith.EventStore.Testing.Tests.Compliance;

public class TerminatableComplianceAssertionsTests {
    [Fact]
    public void AssertTerminatableCompliance_CompliantState_DoesNotThrow() {
        TerminatableComplianceAssertions.AssertTerminatableCompliance<CompliantTerminatableState>();
    }

    [Fact]
    public void AssertTerminatableCompliance_NonTerminatableState_DoesNotThrow() {
        TerminatableComplianceAssertions.AssertTerminatableCompliance<NonTerminatableState>();
    }

    [Fact]
    public void AssertTerminatableCompliance_StateInheritsApplyFromBase_DoesNotThrow() {
        // Pins AC #6: inherited public instance Apply methods on a base class satisfy the
        // contract — the helper mirrors DomainProcessorStateRehydrator's discovery walk.
        TerminatableComplianceAssertions.AssertTerminatableCompliance<InheritedApplyTerminatableState>();
    }

    [Fact]
    public void AssertTerminatableCompliance_StateWithNoApplyMethods_ThrowsMissingApplyMethodException() {
        MissingApplyMethodException ex = Assert.Throws<MissingApplyMethodException>(
            TerminatableComplianceAssertions.AssertTerminatableCompliance<EmptyBrokenTerminatableState>);

        Assert.Equal(typeof(EmptyBrokenTerminatableState), ex.StateType);
        Assert.Equal(nameof(AggregateTerminated), ex.EventTypeName);
    }

    [Fact]
    public void AssertTerminatableCompliance_StateMissingOnlyTerminatedApply_StillThrows() {
        MissingApplyMethodException ex = Assert.Throws<MissingApplyMethodException>(
            TerminatableComplianceAssertions.AssertTerminatableCompliance<PartiallyBrokenTerminatableState>);

        Assert.Equal(typeof(PartiallyBrokenTerminatableState), ex.StateType);
        Assert.Equal(nameof(AggregateTerminated), ex.EventTypeName);
        // Pins the helper-to-exception integration: BuildMessage emits the ITerminatable hint
        // for any state implementing the interface (already covered end-to-end by R1-A6 tests).
        Assert.Contains("ITerminatable", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AssertTerminatableCompliance_WrongReturnType_ThrowsMissingApplyMethodException() {
        MissingApplyMethodException ex = Assert.Throws<MissingApplyMethodException>(
            TerminatableComplianceAssertions.AssertTerminatableCompliance<WrongReturnTypeTerminatableState>);

        Assert.Equal(typeof(WrongReturnTypeTerminatableState), ex.StateType);
        Assert.Equal(nameof(AggregateTerminated), ex.EventTypeName);
    }

    private sealed record SomeOtherEvent : IEventPayload;

    private sealed class CompliantTerminatableState : ITerminatable {
        public bool IsTerminated => false;

        [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Helper requires public instance Apply method per ITerminatable runtime contract.")]
        public void Apply(AggregateTerminated e) {
            // No-op: matches the production contract used by CounterState.
        }
    }

    private sealed class EmptyBrokenTerminatableState : ITerminatable {
        public bool IsTerminated => false;
    }

    private sealed class PartiallyBrokenTerminatableState : ITerminatable {
        public bool IsTerminated => false;

        // Models the realistic failure mode: domain author wrote several Apply methods
        // and forgot the one for AggregateTerminated.
        [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Test fixture must declare a public instance Apply method to mirror the realistic failure mode.")]
        public void Apply(SomeOtherEvent e) {
            // No-op: present only to make the missing-Apply(AggregateTerminated) the lone gap.
        }
    }

    private sealed class WrongReturnTypeTerminatableState : ITerminatable {
        public bool IsTerminated => false;

        // Test fixture intentionally violates the Apply(AggregateTerminated) contract:
        // return type must be void per AC #6. The `_` parameter name sidesteps unused-parameter analyzers.
        [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Test fixture intentionally violates the Apply contract while keeping the method as a public instance member to be discoverable by the helper.")]
        public bool Apply(AggregateTerminated _) => true;
    }

    private sealed class NonTerminatableState {
    }

    private class BaseStateWithApply {
        [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Base-class fixture: helper must discover this inherited public instance Apply method via reflection.")]
        public void Apply(AggregateTerminated e) {
            // No-op: provided by base to model a shared aggregate-state base class.
        }
    }

    private sealed class InheritedApplyTerminatableState : BaseStateWithApply, ITerminatable {
        public bool IsTerminated => false;
    }
}
