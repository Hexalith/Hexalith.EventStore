using System.Text.Json.Serialization;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>A source-generated payload used only by policy discovery fault vectors.</summary>
internal sealed class PolicyLifecyclePayload
{
    /// <summary>Gets or sets the selected fixture value with an escaped serialized name.</summary>
    [PersonalData]
    [JsonPropertyName("a/b~")]
    public string? Secret { get; set; }

    /// <summary>Gets or sets an ordinary fixture value.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets a nested payload for overlap and cycle tests.</summary>
    public PolicyLifecyclePayload? Child { get; set; }
}
