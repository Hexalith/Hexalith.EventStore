#!/usr/bin/env dotnet
#:project ../src/Hexalith.EventStore.Client/Hexalith.EventStore.Client.csproj
#:property IsPackable=false

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
            DevelopmentRouteCatalogBuilder.ReadInput(File.ReadAllBytes(inputPath)));
        byte[] bytes = RouteCatalogCodec.Encode(catalog);
        // Validation completes before opening the destination. Incomplete inputs never create a catalog.
        File.WriteAllBytes(outputPath, bytes);
        Console.WriteLine(RouteCatalogCodec.ComputeRootDigest(catalog));
        return 0;
    }

    if (args.Length is 2 or 3 && args[0] == "validate")
    {
        RouteCatalogEnvelope catalog = RouteCatalogCodec.Decode(File.ReadAllBytes(args[1]), args.Length == 3 ? args[2] : null);
        Console.WriteLine(RouteCatalogCodec.ComputeRootDigest(catalog));
        return 0;
    }

    if (args is ["inspect", string baseSettings, string developmentSettings])
    {
        DevelopmentCatalogInspection inspection = DevelopmentRouteCatalogBuilder.Inspect(
            [File.ReadAllBytes(baseSettings), File.ReadAllBytes(developmentSettings)]);
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
