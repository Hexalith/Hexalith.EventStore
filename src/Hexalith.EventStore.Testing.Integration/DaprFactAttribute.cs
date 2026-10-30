using System.Runtime.CompilerServices;

using Xunit;

namespace Hexalith.EventStore.Testing.Integration;

/// <summary>
/// Runs a descriptor-backed test. Missing runner handoffs fail fixture initialization; they never skip tests.
/// </summary>
public sealed class DaprFactAttribute : FactAttribute {
    /// <summary>Initializes a new instance of the <see cref="DaprFactAttribute"/> class.</summary>
    public DaprFactAttribute(
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
        : base(sourceFilePath, sourceLineNumber) {

    }
}
