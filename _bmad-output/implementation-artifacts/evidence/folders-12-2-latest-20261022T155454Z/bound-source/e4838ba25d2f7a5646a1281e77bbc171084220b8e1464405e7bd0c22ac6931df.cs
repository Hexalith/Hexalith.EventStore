// Story 4.3 resolver fixtures. See EsFixAEvents.cs for why these live in real, separate namespaces.
namespace EsFixB;

internal sealed class ItemAdded {
    public string Name { get; init; } = string.Empty;
}

internal sealed class Foo {
    public string Name { get; init; } = string.Empty;
}
