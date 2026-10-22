// Story 4.3 resolver fixtures.
//
// The resolution rules under test are namespace-shaped: they depend on full CLR names, on the '.' and
// '+' name boundaries, and on how deep a candidate's namespace is. Expressing those cases requires real,
// differently-nested namespaces rather than nested classes (nested classes render with '+', not '.').
//
// Every fixture type is deliberately `internal`: AssemblyScanner discovers domain types through
// Assembly.GetExportedTypes(), so internal fixtures cannot leak into any assembly-wide discovery test.
namespace EsFixA;

internal sealed class ItemAdded {
    public string Name { get; init; } = string.Empty;
}

internal sealed class SubItemAdded {
    public string Name { get; init; } = string.Empty;
}

internal sealed class Foo {
    public string Name { get; init; } = string.Empty;
}

/// <summary>Shares a short name with the global-namespace <c>EsFixGlobalCollider</c>.</summary>
internal sealed class EsFixGlobalCollider {
    public string Name { get; init; } = string.Empty;
}

/// <summary>
/// Generic event type. A closed construction's <c>FullName</c> renders its argument assembly-qualified
/// inside a bracket group, which is where naive comma splitting truncated the name.
/// </summary>
/// <typeparam name="TItem">The wrapped item type.</typeparam>
internal sealed class Wrapper<TItem> {
    public string Name { get; init; } = string.Empty;
}

/// <summary>Base event type used to prove the assignability guard does not reject a legitimate match.</summary>
internal class BaseNotification {
    public string Name { get; init; } = string.Empty;
}

/// <summary>
/// Declaring type for a nested event whose <c>FullName</c> anchors on the <c>+</c> boundary against the
/// short name <c>BaseNotification</c> while genuinely deriving from it.
/// </summary>
internal sealed class Holder {
    internal sealed class BaseNotification : global::EsFixA.BaseNotification;
}
