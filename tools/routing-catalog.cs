#!/usr/bin/env dotnet
#:project ../src/Hexalith.EventStore.Client/Hexalith.EventStore.Client.csproj
#:project Hexalith.EventStore.CatalogConstruction/Hexalith.EventStore.CatalogConstruction.csproj
#:property IsPackable=false
#:property PublishAot=false

using System.Text.Json;

using Hexalith.EventStore.Client.Routing;
using Hexalith.EventStore.CatalogConstruction;
using Hexalith.EventStore.Contracts.Routing;

// Repository-local offline developer tool. No operator service, activation, or signing authority.
// Development construction/checking verify declarations. The separate compiled-profile boundary
// rejects the unbounded query fallback pending 5.13; it inspects no running deployment or catalog file.
// The retained 29-route file is an unactivated runtime candidate.
// dotnet run tools/routing-catalog.cs -c Debug -p:UseHexalithProjectReferences=true -- development . deploy/dapr/eventstore-routing-catalog.json
// dotnet run tools/routing-catalog.cs -c Debug -p:UseHexalithProjectReferences=true -- check-development . deploy/dapr/eventstore-routing-catalog.json
// dotnet run tools/routing-catalog.cs -c Debug -p:UseHexalithProjectReferences=true -- check-development-runtime .
// dotnet run tools/routing-catalog.cs -p:UseHexalithProjectReferences=true -- generate INPUT OUTPUT
// dotnet run tools/routing-catalog.cs -p:UseHexalithProjectReferences=true -- validate CATALOG [ROOT_SHA256]
// dotnet run tools/routing-catalog.cs -p:UseHexalithProjectReferences=true -- inspect APPSETTINGS DEVELOPMENT
// Authoring: supply every route, binding, adapter, policy, schema, retention, generation, logical
// secret reference and consumer explicitly in owner-input.json; never include key or intent values.
// Start with empty digest strings, then prepare them in this order using the shared codec:
// var input = DevelopmentRouteCatalogBuilder.ReadInput(File.ReadAllBytes("owner-input.json"));
// input = input with { Idempotency = input.Idempotency.Select(entry =>
// {
//     entry = entry with { DescriptorDigest = RouteCatalogCodec.ComputeDescriptorDigest(entry.DescriptorSchema) };
//     return entry with { ContentDigest = RouteCatalogCodec.ComputeIdempotencyEntryDigest(entry) };
// }).ToArray() };
// var catalog = DevelopmentRouteCatalogBuilder.Build(input); // Derives declaration and facet digests.
// For generate, serialize the prepared input with JsonNamingPolicy.CamelCase into INPUT first.
// Encode/catalog hashing always uses RouteCatalogCodec; do not recreate canonicalization here.
try
{
    if (args is ["development" or "check-development", string repositoryRoot, string catalogPath])
    {
        string root = Path.GetFullPath(repositoryRoot);
        if (args[0] == "development") ValidateDevelopmentOutputPath(root, catalogPath);
        RouteCatalogEnvelope actual = await DevelopmentCatalogProfile.BuildDeclarationsAsync(root).ConfigureAwait(false);
        byte[] bytes = RouteCatalogCodec.Encode(actual);
        if (args[0] == "development") WriteAtomically(catalogPath, bytes);
        else if (!ReadBounded(catalogPath).Span.SequenceEqual(bytes))
            throw new RouteCatalogValidationException([new("$", "development-catalog-drift")]);
        Console.WriteLine(RouteCatalogCodec.ComputeRootDigest(actual));
        Console.Error.WriteLine("Development declarations verified; runtime certification, secret availability and catalog activation are unverified.");
        return 0;
    }

    if (args is ["check-development-runtime", string runtimeRepositoryRoot])
    {
        RouteCatalogEnvelope actual = await DevelopmentCatalogProfile.BuildAsync(Path.GetFullPath(runtimeRepositoryRoot)).ConfigureAwait(false);
        Console.WriteLine(RouteCatalogCodec.ComputeRootDigest(actual));
        Console.Error.WriteLine("Development compiled-profile completeness check passed; running deployments, supplied catalog files, secret availability and activation are unverified.");
        return 0;
    }

    if (args is ["generate", string inputPath, string outputPath])
    {
        RouteCatalogEnvelope catalog = DevelopmentRouteCatalogBuilder.Build(
            DevelopmentRouteCatalogBuilder.ReadInput(ReadBounded(inputPath)));
        byte[] bytes = RouteCatalogCodec.Encode(catalog);
        WriteAtomically(outputPath, bytes);
        Console.WriteLine(RouteCatalogCodec.ComputeRootDigest(catalog));
        return 0;
    }

    if (args.Length is 2 or 3 && args[0] == "validate")
    {
        RouteCatalogEnvelope catalog = RouteCatalogCodec.Decode(ReadBounded(args[1]), args.Length == 3 ? args[2] : null);
        Console.WriteLine(RouteCatalogCodec.ComputeRootDigest(catalog));
        return 0;
    }

    if (args is ["inspect", string baseSettings, string developmentSettings])
    {
        DevelopmentCatalogInspection inspection = DevelopmentRouteCatalogBuilder.Inspect(
            [ReadBounded(baseSettings), ReadBounded(developmentSettings)]);
        Console.WriteLine(JsonSerializer.Serialize(inspection, new JsonSerializerOptions { WriteIndented = true }));
        return 2;
    }

    Console.Error.WriteLine("Usage: development ROOT OUTPUT | check-development ROOT CATALOG | check-development-runtime ROOT | generate INPUT OUTPUT | validate CATALOG [ROOT_SHA256] | inspect APPSETTINGS DEVELOPMENT");
    return 64;
}
catch (RouteCatalogValidationException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
{
    Console.Error.WriteLine("Route catalog file access failed.");
    return 3;
}

static void ValidateDevelopmentOutputPath(string repositoryRoot, string outputPath)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
    string destination = ResolveParentAliases(outputPath);
    StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    foreach (string name in new[] { "appsettings.json", "appsettings.Development.json" })
    {
        string input = ResolveParentAliases(Path.Combine(repositoryRoot, "src", "Hexalith.EventStore", name));
        if (string.Equals(destination, input, comparison))
            throw new RouteCatalogValidationException([new("$", "development-output-input-overlap")]);
    }

    static string ResolveParentAliases(string path)
    {
        string fullPath = Path.GetFullPath(path);
        return Path.Combine(ResolveDirectory(Path.GetDirectoryName(fullPath)!, 40), Path.GetFileName(fullPath));
    }

    static string ResolveDirectory(string path, int remainingLinks)
    {
        var directory = new DirectoryInfo(path);
        if (directory.Parent is not { } parent) return directory.FullName;
        string resolved = Path.Combine(ResolveDirectory(parent.FullName, remainingLinks), directory.Name);
        FileSystemInfo? target = new DirectoryInfo(resolved).ResolveLinkTarget(returnFinalTarget: true);
        if (target is null) return resolved;
        if (remainingLinks == 0) throw new IOException();
        return ResolveDirectory(target.FullName, remainingLinks - 1);
    }
}

static ReadOnlyMemory<byte> ReadBounded(string path)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(path);
    using FileStream input = File.OpenRead(path);
    byte[] buffer = new byte[RouteCatalogCodec.MaximumByteLength + 1];
    int length = input.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
    if (length > RouteCatalogCodec.MaximumByteLength)
    {
        throw new RouteCatalogValidationException([new("$", "size-limit")]);
    }

    return buffer.AsMemory(0, length);
}

static void WriteAtomically(string path, byte[] bytes)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(path);
    string destination = Path.GetFullPath(path);
    string temporary = Path.Combine(Path.GetDirectoryName(destination)!, ".routing-catalog-" + Guid.NewGuid().ToString("N") + ".tmp");
    try
    {
        using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            output.Write(bytes);
            output.Flush(flushToDisk: true);
        }

        File.Move(temporary, destination, overwrite: true);
    }
    finally
    {
        File.Delete(temporary);
    }
}
