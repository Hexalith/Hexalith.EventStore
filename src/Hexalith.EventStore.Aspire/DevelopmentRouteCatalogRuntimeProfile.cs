namespace Hexalith.EventStore.Aspire;

/// <summary>A matched immutable native runtime and control-plane selection for disposable Development qualification.</summary>
public sealed class DevelopmentRouteCatalogRuntimeProfile
{
    private DevelopmentRouteCatalogRuntimeProfile(string name, string version, string runtimeSha256, string cliSha256,
        string sentryImage, string placementImage, string schedulerImage)
    {
        Name = name; Version = version; RuntimeSha256 = runtimeSha256; CliSha256 = cliSha256;
        SentryImage = sentryImage; PlacementImage = placementImage; SchedulerImage = schedulerImage;
    }

    /// <summary>The original profile; existing constructors and constants retain this selection.</summary>
    public static DevelopmentRouteCatalogRuntimeProfile Original { get; } = new("original", "1.18.7",
        "e988a6f53abec017fc9ebcb4f89de9bed11bce005eb49bd8e85e8d3d6adbf488", "43429557ff18a7230737b945699417ec391ad41bd431244c175b79ab3c904ce5",
        "ghcr.io/dapr/sentry@sha256:b9fc166085289661f516150dc1c47ac3495b9e6d1e666079cb71cd2374ca7256", "ghcr.io/dapr/placement@sha256:c9da106eddcbb625c94baea31af4ed576d32750741b442c99f51518c420e76cc", "ghcr.io/dapr/scheduler@sha256:2cc2da2f7c4ece604041e6a420de04a297afb24cea8b3c9394bd0e126a334444");

    /// <summary>The explicitly selected 1.19.1 candidate; this conveys no production qualification.</summary>
    public static DevelopmentRouteCatalogRuntimeProfile Dapr1191 { get; } = new("1.19.1", "1.19.1",
        "b1ece5da76674bf6e15126aeb019fa58e444b5b8edf4d0ac59e32df895ae8fd2", "b651f5fcfe0b261ca6b26058dde31927efc8e17b12267d6743bdbeddca09bafd", "ghcr.io/dapr/sentry@sha256:f5f057f504d6b428f68e380421613e71a4a1f3cedf1fd66beacaa6fcc866b414", "ghcr.io/dapr/placement@sha256:6dcfbcd606482976c5f4b821d0d0755e88db796bdfcafc395090708ebdba6c66", "ghcr.io/dapr/scheduler@sha256:ae0f1e04a0c657cb5a7c67ca4f520cac54c939a01e5810923e9d0b48a63ab739");

    /// <summary>The stable selection key.</summary>
    public string Name { get; }
    /// <summary>The native runtime version.</summary>
    public string Version { get; }
    /// <summary>The standard Linux amd64 executable checksum.</summary>
    public string RuntimeSha256 { get; }
    /// <summary>The matching Linux amd64 CLI executable checksum.</summary>
    public string CliSha256 { get; }
    /// <summary>The immutable Sentry index reference.</summary>
    public string SentryImage { get; }
    /// <summary>The immutable placement index reference.</summary>
    public string PlacementImage { get; }
    /// <summary>The immutable scheduler index reference.</summary>
    public string SchedulerImage { get; }

    /// <summary>Resolves an explicit supported key; absent selection preserves the original profile.</summary>
    public static DevelopmentRouteCatalogRuntimeProfile Select(string? selection) => selection switch
    {
        null or "" or "original" => Original,
        "1.19.1" => Dapr1191,
        _ => throw new ArgumentException("Unsupported Dapr runtime qualification profile.", nameof(selection)),
    };
}
