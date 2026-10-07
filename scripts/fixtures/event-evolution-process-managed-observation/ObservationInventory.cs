using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Client.Tests;

/// <summary>Supplies exact local fixture declarations; it is not an authoritative deployment catalog.</summary>
internal sealed class ObservationInventory : IDisposable
{
    private ObservationInventory(EventDomainRegistry registry, EventResolvedDependency[] graph,
        EventManagedLoadContext[] contexts, long manifestBytes)
    {
        Registry = registry;
        Graph = graph;
        Contexts = contexts;
        ManifestBytes = manifestBytes;
    }

    internal EventDomainRegistry Registry { get; }

    internal EventResolvedDependency[] Graph { get; }

    internal EventManagedLoadContext[] Contexts { get; }

    internal long ManifestBytes { get; }

    internal static ObservationInventory Create(AssemblyLoadContext context, AssemblyLoadContext? secondDeclared = null)
    {
        Dictionary<string, string> fixture = JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "EventRegistryV17.json")))!;
        var rows = new List<ReadOnlyMemory<byte>>
        {
            Convert.FromHexString(fixture["AliasRow"]),
            Convert.FromHexString(fixture["DescriptorRow"]),
            Convert.FromHexString(fixture["VersionRow"]),
            Convert.FromHexString(fixture["SharedRow"]),
        };
        var nodes = new List<EventResolvedDependency>();
        string[] platformFiles = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator).Distinct(StringComparer.Ordinal).ToArray();
        foreach (string file in platformFiles)
        {
            AddManagedFile(file, "default", rows, nodes);
        }

        AddManagedFile(typeof(EventDomainRegistry).Assembly.Location, "declared-context", rows, nodes);
        var contexts = new List<EventManagedLoadContext>
        {
            new("default", AssemblyLoadContext.Default), new("declared-context", context),
        };
        if (secondDeclared is not null)
        {
            AddManagedFile(typeof(EventDomainRegistry).Assembly.Location, "other-declared-context", rows, nodes);
            contexts.Add(new("other-declared-context", secondDeclared));
        }
        long bytes = rows.Sum(static row => (long)row.Length);
        return new ObservationInventory(
            new EventDomainRegistry("d", rows, new EventEvolutionCapabilityLoss()), nodes.ToArray(),
            contexts.ToArray(), bytes);
    }

    public void Dispose() => Registry.Dispose();

    private static void AddManagedFile(string file, string context, List<ReadOnlyMemory<byte>> rows,
        List<EventResolvedDependency> nodes)
    {
        AssemblyName name = AssemblyName.GetAssemblyName(file);
        string identity = context + ":" + name.FullName;
        string version = name.Version!.ToString();
        using FileStream artifact = File.OpenRead(file);
        byte[] hash = SHA256.HashData(artifact);
        using var writer = new EventEvolutionBinaryWriter(4096);
        writer.WriteByte(0x47);
        writer.WriteString("d");
        writer.WriteString(identity);
        writer.WriteString("managed");
        writer.WriteUInt16(3);
        writer.WriteByte(1); writer.WriteString(version);
        writer.WriteByte(2); writer.WriteHash(hash);
        writer.WriteByte(3); writer.WriteString(context);
        rows.Add(writer.CopyEncodedBytes());
        nodes.Add(new(new(identity, "managed"), version, context, file, []));
    }
}
