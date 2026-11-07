// Approved on-premises supplement: 43fc3400f9e01662eac5ad945407711d0d24a744685e92cef203c2162106e2d3; sections 1-7.
// On-premises authority: 835f96ca5b4253f0598712a40773df468f02bd48f518eb0313794addfdc9b180; approved supplement sections 2, 4-5.
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Reflection-free neutral wire metadata; use the strict codec to validate bounds, duplicates and semantics.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, UseStringEnumConverter = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(PayloadKeyBackendRequest))]
[JsonSerializable(typeof(PayloadKeyBackendResponse))]
[JsonSerializable(typeof(PayloadKeyBackendProfile))]
public partial class PayloadKeyBackendJsonContext : JsonSerializerContext;
