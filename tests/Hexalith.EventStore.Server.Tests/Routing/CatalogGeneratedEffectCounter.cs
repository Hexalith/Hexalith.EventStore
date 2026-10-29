namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Records only downstream work after generated-host catalog validation.</summary>
internal sealed class CatalogGeneratedEffectCounter
{
    public int Effects { get; private set; }
    public void Record() => Effects++;
}
