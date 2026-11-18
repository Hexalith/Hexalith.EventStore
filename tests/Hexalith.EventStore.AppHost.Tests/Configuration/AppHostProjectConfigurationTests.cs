namespace Hexalith.EventStore.AppHost.Tests.Configuration;

using System.Xml.Linq;

using Hexalith.EventStore.Aspire;

/// <summary>
/// Verifies the AppHost project keeps its reviewed Aspire build configuration.
/// </summary>
public sealed class AppHostProjectConfigurationTests
{
    /// <summary>
    /// Verifies the AppHost requires the Aspire 17 CLI bundle and keeps the obsolete AppHost SDK warning suppressed.
    /// </summary>
    [Fact]
    public void AppHostProjectRequiresCliBundleAndSuppressesObsoleteAppHostSdkWarning()
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
        aspireUseCliBundle.Value.ShouldBe("true");
        aspireUseCliBundle.Attribute("Condition").ShouldBeNull();
        aspireUseCliBundle.Parent.ShouldNotBeNull().Attribute("Condition").ShouldBeNull();

        XElement suppressObsoleteSdk = project
            .Descendants()
            .Single(element => string.Equals(
                element.Name.LocalName,
                "SuppressAspireAppHostSdkObsoleteWarning",
                StringComparison.Ordinal));
        suppressObsoleteSdk.Value.ShouldBe("true");
        suppressObsoleteSdk.Attribute("Condition").ShouldBeNull();
        suppressObsoleteSdk.Parent.ShouldNotBeNull().Attribute("Condition").ShouldBeNull();

        XElement noWarn = project
            .Descendants()
            .Single(element => string.Equals(element.Name.LocalName, "NoWarn", StringComparison.Ordinal));
        noWarn.Value.ShouldBe("$(NoWarn);CS0618;ASPIRE012");
        noWarn.Attribute("Condition").ShouldBeNull();
        noWarn.Parent.ShouldNotBeNull().Attribute("Condition").ShouldBeNull();
    }
}
