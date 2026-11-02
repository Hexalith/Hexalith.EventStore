using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>A value object with a sensitive member below a nullable wrapper.</summary>
internal struct NestedSensitiveValue
{
    /// <summary>Gets or sets the sensitive member.</summary>
    [PersonalData]
    public string Secret { get; set; }
}
