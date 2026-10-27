#if SELECTED
using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Client.Registration;

namespace PublishedConsumer;

/// <summary>Registers a two-version manifest through the published service API, retaining its real missing-callable boundary.</summary>
public static class ManifestEvolutionFixture
{
    public static string Fingerprint { get; private set; } = string.Empty;

    public static void Register(IServiceCollection services)
    {
        var fixture = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "RegistryFixture.json")))!;
        byte[] descriptor = Convert.FromHexString(fixture["DescriptorRow"]);
        BinaryPrimitives.WriteInt32BigEndian(descriptor.AsSpan(22, 4), 2);
        byte[] first = Convert.FromHexString(fixture["VersionRow"]);
        byte[] second = first.ToArray();
        BinaryPrimitives.WriteInt32BigEndian(second.AsSpan(13, 4), 2);
        using var edge = new MemoryStream();
        void Byte(byte value) => edge.WriteByte(value);
        void Integer(int value) { Span<byte> bytes = stackalloc byte[4]; BinaryPrimitives.WriteInt32BigEndian(bytes, value); edge.Write(bytes); }
        void Text(string value) { byte[] bytes = Encoding.UTF8.GetBytes(value); Integer(bytes.Length); edge.Write(bytes); }
        void Hash(byte[] value) => edge.Write(value);
        Byte(0x45); Text("d"); Text("evt"); Integer(1); Byte(0); Byte(12);
        Byte(1); Integer(2); Byte(2); Text("json"); Byte(3); Text("json"); Byte(4); Text("serializer");
        Byte(5); Hash(new byte[32]); Byte(6); Hash(new byte[32]); Byte(7); Text("p1r-required-upcaster");
        Byte(8); Hash(SHA256.HashData(File.ReadAllBytes(typeof(ManifestEvolutionFixture).Assembly.Location)));
        Byte(9); Hash(new byte[32]); Byte(10); Text("no-payload-identity"); Byte(11); Hash(new byte[32]); Byte(12); Hash(new byte[32]);
        ReadOnlyMemory<byte>[] rows = [Convert.FromHexString(fixture["AliasRow"]), descriptor, first, second, edge.ToArray(), Convert.FromHexString(fixture["SharedRow"])];
        // The public registration accepts a caller pin. Use the loaded production codec to compute this fixture's pin;
        // no internal reader, fake callable, source replacement, or readiness activation is installed.
        Type codec = typeof(EventEvolutionServiceCollectionExtensions).Assembly.GetType("Hexalith.EventStore.Client.Events.EventRegistryFingerprintCodec", throwOnError: true)!;
        Fingerprint = Convert.ToHexStringLower((byte[])codec.GetMethod("Compute", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, ["d", rows, 0L])!);
        services.AddEventStoreEventEvolutionManifestCandidate("d", rows, Fingerprint);
    }
}
#endif
