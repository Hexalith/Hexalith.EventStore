using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Configuration;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Streams;

/// <summary>Proves metadata-only enrollment is retained in the real independent authentication observation.</summary>
public sealed class NativeSourceMetadataCatalogObservationTests
{
    /// <summary>Changing receiver authority changes observed authentication bytes without exposing private signing material.</summary>
    [Fact]
    public async Task SourceOnlyReceiverEnrollmentIsPartOfTheIndependentCatalogObservation()
    {
        string privateSentinel = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        string directory = Path.Combine(Path.GetTempPath(), "native-source-catalog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string component = Path.Combine(directory, "component.json"), acl = Path.Combine(directory, "acl.yaml");
            await File.WriteAllTextAsync(component, "{}", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(acl, "defaultAction: deny", TestContext.Current.CancellationToken);
            IConfigurationRoot configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EventStore:RouteCatalog:DevelopmentBootstrap:HostId"] = "actual-host",
                ["EventStore:RouteCatalog:DevelopmentBootstrap:ComponentPath"] = component,
                ["EventStore:RouteCatalog:DevelopmentBootstrap:AccessControlPath"] = acl,
                ["Authentication:BoundStreamRead:MetadataOnlyCallers:folders"] = "true",
                ["Authentication:BoundStreamRead:PrivateKey"] = privateSentinel,
                ["Authentication:NativeSourceScan:PrivateKey"] = privateSentinel,
                ["Authentication:NativeSourceScan:PublicKeys:folders"] = privateSentinel,
                ["Authentication:NativeSourceScan:AllowedDomains:folders"] = "folders",
            }).Build();
            var before = await DevelopmentRouteCatalogInputs.CaptureAsync(configuration, "eventstore", [], [], [], [], [], TestContext.Current.CancellationToken);
            using JsonDocument observed = JsonDocument.Parse(before.AuthenticationProfile);
            observed.RootElement.GetProperty("settings").GetProperty("Authentication:BoundStreamRead:MetadataOnlyCallers:folders")
                .GetString().ShouldBe("true");
            System.Text.Encoding.UTF8.GetString(before.AuthenticationProfile).ShouldNotContain(privateSentinel);
            observed.RootElement.GetProperty("nativeScanProofCallers").EnumerateArray().Select(item => item.GetString()).ShouldBe(["folders"]);
            observed.RootElement.GetProperty("settings").GetProperty("Authentication:NativeSourceScan:AllowedDomains:folders").GetString().ShouldBe("folders");
            configuration["Authentication:NativeSourceScan:AllowedDomains:folders"] = "other-domain";
            var scanChanged = await DevelopmentRouteCatalogInputs.CaptureAsync(configuration, "eventstore", [], [], [], [], [], TestContext.Current.CancellationToken);
            before.AuthenticationProfile.SequenceEqual(scanChanged.AuthenticationProfile).ShouldBeFalse();
            System.Text.Encoding.UTF8.GetString(scanChanged.AuthenticationProfile).ShouldNotContain(privateSentinel);
            configuration["Authentication:NativeSourceScan:AllowedDomains:folders"] = "folders";
            configuration["Authentication:BoundStreamRead:MetadataOnlyCallers:folders"] = null;
            var after = await DevelopmentRouteCatalogInputs.CaptureAsync(configuration, "eventstore", [], [], [], [], [], TestContext.Current.CancellationToken);
            before.AuthenticationProfile.SequenceEqual(after.AuthenticationProfile).ShouldBeFalse();
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
