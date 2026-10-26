#!/usr/bin/env dotnet
#:project ../src/Hexalith.EventStore.Client/Hexalith.EventStore.Client.csproj
#:property IsPackable=false
#:property PublishAot=false

using System.Text.Json;

using Hexalith.EventStore.Client.Routing;
using Hexalith.EventStore.Contracts.Routing;

// Repository-local offline developer tool. No operator service, activation, or signing authority.
// dotnet run tools/routing-catalog.cs -p:UseHexalithProjectReferences=true -- generate INPUT OUTPUT
// dotnet run tools/routing-catalog.cs -p:UseHexalithProjectReferences=true -- validate CATALOG [ROOT_SHA256]
// dotnet run tools/routing-catalog.cs -p:UseHexalithProjectReferences=true -- inspect APPSETTINGS DEVELOPMENT
try
{
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

    Console.Error.WriteLine("Usage: generate INPUT OUTPUT | validate CATALOG [ROOT_SHA256] | inspect APPSETTINGS DEVELOPMENT");
    return 64;
}
catch (RouteCatalogValidationException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine("Route catalog file access failed.");
    return 3;
}

static ReadOnlyMemory<byte> ReadBounded(string path)
{
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
