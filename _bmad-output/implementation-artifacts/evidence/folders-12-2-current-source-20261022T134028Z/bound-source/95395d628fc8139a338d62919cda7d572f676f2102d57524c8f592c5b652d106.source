// Story 4.3 resolver fixtures — global-namespace collider.
//
// A type declared outside any namespace has FullName == Name, so it registers key
// "EsFixGlobalCollider" as a *full name* while EsFixA.EsFixGlobalCollider registers the same key as a
// *short name*. The suffix scan must union both candidate sets; selecting one map over the other would
// silently bind one of two genuinely matching Apply methods.
//
// Deliberately internal, like every other resolver fixture, so AssemblyScanner (which enumerates
// Assembly.GetExportedTypes()) cannot see it.
internal sealed class EsFixGlobalCollider {
    public string Name { get; init; } = string.Empty;
}
