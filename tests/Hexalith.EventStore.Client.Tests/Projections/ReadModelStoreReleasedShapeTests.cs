using System.Reflection;

using Hexalith.EventStore.Client.Projections;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

/// <summary>
/// Additive-compatibility guard for Story 1.9: the released <see cref="IReadModelStore"/> contract must
/// stay source/binary compatible for third-party implementations. The ETag-conditional erase and
/// read-etag capability added by Story 1.9 lives only on the additive <see cref="IReadModelConditionalEraser"/>
/// companion and must never leak onto the released store contract.
/// </summary>
public class ReadModelStoreReleasedShapeTests {
    [Fact]
    public void ReleasedReadModelStoreInterface_PublicShapeUnchanged() {
        string[] expected =
        [
            nameof(IReadModelStore.GetAsync),
            nameof(IReadModelStore.SaveAsync),
            nameof(IReadModelStore.TrySaveAsync),
        ];

        string[] actual = typeof(IReadModelStore)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(method => method.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        actual.ShouldBe(expected.OrderBy(name => name, StringComparer.Ordinal).ToArray(), ignoreOrder: false);

        // The additive erase/read-etag members must never appear on the released store contract.
        actual.ShouldNotContain("TryEraseAsync");
        actual.ShouldNotContain("TryReadEtagAsync");
    }

    [Fact]
    public void ConditionalEraserCompanion_OwnsTheAdditiveEraseAndReadEtagMembers() {
        string[] eraserMembers = typeof(IReadModelConditionalEraser)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(method => method.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        eraserMembers.ShouldBe(["TryEraseAsync", "TryReadEtagAsync"], ignoreOrder: false);
    }
}
