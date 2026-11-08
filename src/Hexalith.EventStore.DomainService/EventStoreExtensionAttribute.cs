namespace Hexalith.EventStore.DomainService;

/// <summary>Declares the stable identity and the EventStore extension-API major an extension package implements.</summary>
/// <remarks>
/// The composed gateway compares this attribute with the module declaration before it registers any adapter, so a
/// declaration cannot drift from the binary it enrolls.
/// </remarks>
/// <param name="id">The stable extension identity declared by the owning module.</param>
/// <param name="major">The implemented extension-API major; see <see cref="EventStoreExtensionApi"/>.</param>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class EventStoreExtensionAttribute(string id, int major) : Attribute
{
    /// <summary>Gets the stable extension identity.</summary>
    public string Id { get; } = id;

    /// <summary>Gets the implemented extension-API major.</summary>
    public int Major { get; } = major;
}
