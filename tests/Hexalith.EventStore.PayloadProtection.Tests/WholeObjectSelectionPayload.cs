using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Whole-subtree selection needs no correlation of a polymorphic descendant.</summary>
internal sealed class WholeObjectSelectionPayload
{
    /// <summary>Gets a whole selected value with intentionally opaque declared shape.</summary>
    [PersonalData]
    public object Child { get; } = new PolicyLifecyclePayload { Secret = "whole-selection-private-canary" };
}
