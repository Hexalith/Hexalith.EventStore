using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Populated nullable scalars and elements must not hide a later sensitive sibling.</summary>
internal sealed class NullablePolicyPayload
{
    /// <summary>Gets or sets an ordinary nullable scalar.</summary>
    public int? Count { get; set; }
    /// <summary>Gets or sets an ordinary array of nullable elements.</summary>
    public int?[] Values { get; set; } = [];
    /// <summary>Gets or sets an ordinary list of nullable elements.</summary>
    public List<int?> Items { get; set; } = [];
    /// <summary>Gets or sets a sensitive sibling after the ordinary nullable values.</summary>
    [PersonalData]
    public string Secret { get; set; } = "";
}
