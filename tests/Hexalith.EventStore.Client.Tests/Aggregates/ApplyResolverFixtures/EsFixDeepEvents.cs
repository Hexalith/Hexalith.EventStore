// Story 4.3 resolver fixtures — depth pair.
//
// `EsFixOuter.EsFixDeep.Foo` is a deeper namespace match for the same short name as `EsFixDeep.Foo`,
// which is what the longest-anchored-match rule has to separate. The nested segment must repeat the root
// namespace name for that relationship to exist, so this pair is kept in namespaces used by nothing else
// and no type is ever declared directly in `EsFixOuter`. Fixture states additionally qualify every event
// type with `global::` so a future addition cannot silently rebind these names.
namespace EsFixDeep;

internal sealed class Foo {
    public string Name { get; init; } = string.Empty;
}
