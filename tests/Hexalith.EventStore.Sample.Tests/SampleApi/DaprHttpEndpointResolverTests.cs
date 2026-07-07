extern alias SampleApi;
extern alias BlazorUI;

using ApiDaprHttpEndpointResolver = SampleApi::Hexalith.EventStore.Sample.Api.Services.DaprHttpEndpointResolver;
using BlazorDaprHttpEndpointResolver = BlazorUI::Hexalith.EventStore.Sample.BlazorUI.Services.DaprHttpEndpointResolver;

using Microsoft.Extensions.Configuration;

using Shouldly;

namespace Hexalith.EventStore.Sample.Tests.SampleApi;

public sealed class DaprHttpEndpointResolverTests
{
    public static TheoryData<string> ResolverNames()
        => new()
        {
            "api",
            "blazor",
        };

    [Theory]
    [MemberData(nameof(ResolverNames))]
    public void Resolve_WhenNoSidecarConfigurationExists_ReturnsDefaultLocalEndpoint(string resolverName)
    {
        string endpoint = Resolve(resolverName, Configuration());

        endpoint.ShouldBe("http://localhost:3500");
    }

    [Theory]
    [MemberData(nameof(ResolverNames))]
    public void Resolve_WhenEndpointOriginExists_ReturnsNormalizedOrigin(string resolverName)
    {
        string endpoint = Resolve(resolverName, Configuration(
            ("DAPR_HTTP_ENDPOINT", " https://LOCALHOST:3600/ ")));

        endpoint.ShouldBe("https://localhost:3600");
    }

    [Theory]
    [MemberData(nameof(ResolverNames))]
    public void Resolve_WhenEndpointContainsPathQueryOrFragment_ThrowsConfigurationError(string resolverName)
    {
        string[] invalidEndpoints =
        [
            "http://localhost:3500/v1.0",
            "http://localhost:3500?x=1",
            "http://localhost:3500#sidecar",
            "ftp://localhost:3500",
            "localhost:3500",
        ];

        foreach (string invalidEndpoint in invalidEndpoints)
        {
            InvalidOperationException exception = Should.Throw<InvalidOperationException>(() =>
                Resolve(resolverName, Configuration(("DAPR_HTTP_ENDPOINT", invalidEndpoint))));
            exception.Message.ShouldBe("DAPR_HTTP_ENDPOINT must be an absolute HTTP or HTTPS origin URI.");
        }
    }

    [Theory]
    [MemberData(nameof(ResolverNames))]
    public void Resolve_WhenPortExists_ReturnsNormalizedLocalEndpoint(string resolverName)
    {
        string endpoint = Resolve(resolverName, Configuration(
            ("DAPR_HTTP_PORT", " 03500 ")));

        endpoint.ShouldBe("http://localhost:3500");
    }

    [Theory]
    [MemberData(nameof(ResolverNames))]
    public void Resolve_WhenPortIsMalformedOrOutOfRange_ThrowsConfigurationError(string resolverName)
    {
        string[] invalidPorts =
        [
            "0",
            "65536",
            "+3500",
            "35OO",
        ];

        foreach (string invalidPort in invalidPorts)
        {
            InvalidOperationException exception = Should.Throw<InvalidOperationException>(() =>
                Resolve(resolverName, Configuration(("DAPR_HTTP_PORT", invalidPort))));
            exception.Message.ShouldBe("DAPR_HTTP_PORT must be a TCP port number between 1 and 65535.");
        }
    }

    private static IConfiguration Configuration(params (string Key, string? Value)[] values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(static value => new KeyValuePair<string, string?>(value.Key, value.Value)))
            .Build();

    private static string Resolve(string resolverName, IConfiguration configuration)
        => resolverName switch
        {
            "api" => ApiDaprHttpEndpointResolver.Resolve(configuration),
            "blazor" => BlazorDaprHttpEndpointResolver.Resolve(configuration),
            _ => throw new ArgumentOutOfRangeException(nameof(resolverName), resolverName, "Unknown resolver name."),
        };
}
