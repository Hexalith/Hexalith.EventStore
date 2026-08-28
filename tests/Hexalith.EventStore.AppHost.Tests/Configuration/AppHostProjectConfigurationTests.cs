namespace Hexalith.EventStore.AppHost.Tests.Configuration;

using System.Xml.Linq;

using Hexalith.EventStore.Aspire;

/// <summary>
/// Verifies the AppHost project keeps its reviewed Aspire build configuration.
/// </summary>
public sealed class AppHostProjectConfigurationTests
{
    /// <summary>
    /// Verifies NuGet orchestration remains explicit and only the Aspire bundle reminder is suppressed.
    /// </summary>
    [Fact]
    public void AppHostProjectExplicitlyRetainsNuGetOrchestrationAndSuppressesOnlyAspire010()
    {
        XDocument project = XDocument.Load(Path.Combine(
            RepositoryProjectPaths.GetRepositoryRoot(),
            "src",
            "Hexalith.EventStore.AppHost",
            "Hexalith.EventStore.AppHost.csproj"));

        XElement aspireUseCliBundle = project
            .Descendants()
            .Single(element => string.Equals(
                element.Name.LocalName,
                "AspireUseCliBundle",
                StringComparison.Ordinal));
        aspireUseCliBundle.Value.ShouldBe("false");
        aspireUseCliBundle.Attribute("Condition").ShouldBeNull();
        aspireUseCliBundle.Parent.ShouldNotBeNull().Attribute("Condition").ShouldBeNull();

        XElement noWarn = project
            .Descendants()
            .Single(element => string.Equals(element.Name.LocalName, "NoWarn", StringComparison.Ordinal));
        noWarn.Value.ShouldBe("$(NoWarn);ASPIRE010");
        noWarn.Attribute("Condition").ShouldBeNull();
        noWarn.Parent.ShouldNotBeNull().Attribute("Condition").ShouldBeNull();
    }
}
