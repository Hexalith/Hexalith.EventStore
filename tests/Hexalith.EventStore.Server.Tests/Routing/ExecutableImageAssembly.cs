using System.Reflection;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Supplies the existing executable path protocol without inventing a PE or loaded-MVID requirement.</summary>
internal sealed class ExecutableImageAssembly(string path) : Assembly
{
    /// <inheritdoc/>
    public override string Location => path;
}
