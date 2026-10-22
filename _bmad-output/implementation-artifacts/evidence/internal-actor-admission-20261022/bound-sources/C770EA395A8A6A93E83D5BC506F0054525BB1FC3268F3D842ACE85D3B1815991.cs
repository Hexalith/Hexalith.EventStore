using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>Private staged replay context, carried outside the unchanged public projection JSON contracts.</summary>
internal sealed record ProjectionReplayTransportContext(int Version, string HeadMessageId,
    IReadOnlyDictionary<string, string>? OwnerGenerations, IReadOnlyDictionary<string, string>? OwnerIdentities)
{
    internal const string HeaderName = "x-hexalith-projection-replay-context";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OwnerBindingFingerprint { get; init; }

    internal static ProjectionReplayTransportContext? Capture(ProjectionRequest request)
        => request.RebuildOwnerBindingFingerprint is not null ? CaptureCanonical(request)
            : request.RebuildSourceHeadMessageId is null && request.RebuildOwnerGenerations is null && request.RebuildOwnerIdentities is null
            ? null : new(1, request.RebuildSourceHeadMessageId!, request.RebuildOwnerGenerations, request.RebuildOwnerIdentities);

    internal static bool HasOwners(ProjectionRequest request)
        => request.RebuildOwnerGenerations is not null || request.RebuildOwnerBindingFingerprint is not null;

    internal static ProjectionReplayTransportContext? CaptureCanonical(ProjectionRequest request)
    {
        if (request.RebuildSourceHeadMessageId is null && request.RebuildOwnerGenerations is null
            && request.RebuildOwnerIdentities is null && request.RebuildOwnerBindingFingerprint is null) return null;
        if (string.IsNullOrWhiteSpace(request.RebuildSourceHeadMessageId)) throw new JsonException();
        return new(2, Digest(request.RebuildSourceHeadMessageId), null, null)
        { OwnerBindingFingerprint = BindingFingerprint(request) };
    }

    internal static bool MatchesOwners(ProjectionRequest request, IReadOnlyList<ProjectionOwnerSelection> owners)
    {
        try
        {
            if (owners.Any(static owner => owner is null)
                || owners.Select(static owner => owner.ProjectionName).Distinct(StringComparer.Ordinal).Count() != owners.Count) return false;
            string? expected = BindingFingerprint(request);
            return expected is not null && expected == ComputeBindings(
                owners.ToDictionary(static owner => owner.ProjectionName, static owner => owner.OwnerGeneration, StringComparer.Ordinal),
                owners.ToDictionary(static owner => owner.ProjectionName, static owner => owner.OwnerIdentity, StringComparer.Ordinal));
        }
        catch (JsonException) { return false; }
        catch (ArgumentException) { return false; }
    }

    internal static string? Header(ProjectionDispatchRequest request)
    {
        if (CaptureCanonical(request.Request) is not { } context) return null;
        ValidateRoutes(request);
        string header = JsonSerializer.Serialize(context, Json);
        _ = Bind(request, header);
        return header;
    }

    internal static ProjectionDispatchRequest Bind(ProjectionDispatchRequest request, string? header)
    {
        if (header is null) return request;
        if (Encoding.UTF8.GetByteCount(header) > 4096) throw new JsonException();
        using JsonDocument document = JsonDocument.Parse(header, new JsonDocumentOptions { MaxDepth = 4 });
        RequireUniqueProperties(document.RootElement);
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || document.RootElement.EnumerateObject().Any(static property => property.Name is not
                ("version" or "headMessageId" or "ownerGenerations" or "ownerIdentities" or "ownerBindingFingerprint"))) throw new JsonException();
        ProjectionReplayTransportContext context = JsonSerializer.Deserialize<ProjectionReplayTransportContext>(header, Json) ?? throw new JsonException();
        return context.Apply(request);
    }

    internal ProjectionDispatchRequest Apply(ProjectionDispatchRequest request)
    {
        if (Version == 2)
        {
            if (request?.Request is null || request.ProjectionTypes is null || string.IsNullOrWhiteSpace(request.DispatchId)
                || HeadMessageId != Digest(request.DispatchId) || OwnerGenerations is not null || OwnerIdentities is not null
                || OwnerBindingFingerprint is not null && !ValidDigest(OwnerBindingFingerprint)) throw new JsonException();
            ValidateRoutes(request);
            return request with { Request = request.Request with { RebuildSourceHeadMessageId = request.DispatchId,
                RebuildOwnerGenerations = null, RebuildOwnerIdentities = null,
                RebuildOwnerBindingFingerprint = OwnerBindingFingerprint } };
        }
        if (request?.Request is null || request.ProjectionTypes is null
            || Version != 1 || OwnerBindingFingerprint is not null || string.IsNullOrWhiteSpace(HeadMessageId) || HeadMessageId != request.DispatchId
            || (OwnerGenerations is null) != (OwnerIdentities is null)
            || OwnerGenerations is not null && (OwnerGenerations.Count != request.ProjectionTypes.Count
                || OwnerIdentities!.Count != request.ProjectionTypes.Count
                || request.ProjectionTypes.Any(route => !OwnerGenerations.TryGetValue(route, out string? generation)
                    || string.IsNullOrWhiteSpace(generation) || !OwnerIdentities.TryGetValue(route, out string? owner)
                    || string.IsNullOrWhiteSpace(owner)))) throw new JsonException();
        return request with { Request = request.Request with { RebuildSourceHeadMessageId = HeadMessageId,
            RebuildOwnerGenerations = OwnerGenerations, RebuildOwnerIdentities = OwnerIdentities, RebuildOwnerBindingFingerprint = null } };
    }

    private static void ValidateRoutes(ProjectionDispatchRequest request)
    {
        if (request.ProjectionTypes.Count == 0 || request.ProjectionTypes.Any(string.IsNullOrWhiteSpace)
            || request.ProjectionTypes.Distinct(StringComparer.Ordinal).Count() != request.ProjectionTypes.Count
            || request.Request.RebuildOwnerGenerations is { } generations && (generations.Count != request.ProjectionTypes.Count
                || request.ProjectionTypes.Any(route => !generations.ContainsKey(route)))) throw new JsonException();
    }

    private static string? BindingFingerprint(ProjectionRequest request)
    {
        if ((request.RebuildOwnerGenerations is null) != (request.RebuildOwnerIdentities is null)) throw new JsonException();
        if (request.RebuildOwnerGenerations is not { } generations)
            return request.RebuildOwnerBindingFingerprint;
        string fingerprint = ComputeBindings(generations, request.RebuildOwnerIdentities!);
        if (request.RebuildOwnerBindingFingerprint is { } existing && existing != fingerprint) throw new JsonException();
        return fingerprint;
    }

    private static string ComputeBindings(IReadOnlyDictionary<string, string> generations, IReadOnlyDictionary<string, string> identities)
    {
        if (generations.Count == 0 || identities.Count != generations.Count) throw new JsonException();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append("projection-replay-owner-bindings/v2");
        foreach (string route in generations.Keys.Order(StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(route) || string.IsNullOrWhiteSpace(generations[route])
                || !identities.TryGetValue(route, out string? owner) || string.IsNullOrWhiteSpace(owner)) throw new JsonException();
            Append(route); Append(owner); Append(generations[route]);
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());

        void Append(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            Span<byte> length = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            hash.AppendData(length); hash.AppendData(bytes);
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MetadataSizingBytes, bytes.Length + length.Length);
        }
    }

    private static string Digest(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MetadataSizingBytes, bytes.Length);
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
    private static bool ValidDigest(string value) => value.Length == 64
        && value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void RequireUniqueProperties(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return;
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!names.Add(property.Name)) throw new JsonException();
            RequireUniqueProperties(property.Value);
        }
    }
}
