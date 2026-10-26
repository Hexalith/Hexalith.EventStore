using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Catalog-specific, closed v1/v2 canonical UTF-8 codec and SHA-256 preimages.</summary>
/// <remarks>
/// Objects use ordinal property ordering, BCL default JSON string escaping, decimal integers,
/// explicit nulls, no whitespace/BOM, and no trailing newline. Collections are ordered by RouteId,
/// field Name, key Generation, or ordinal string value. Reader generations retain their declared
/// lookup order. Digests are lowercase hexadecimal SHA-256. Declaration, descriptor, and facet
/// preimages are canonical objects containing a purpose discriminator and schemaVersion. Declaration/descriptor preimages contain only their
/// named payload; idempotency-entry contains every entry field except contentDigest;
/// route-facet contains generation/declarations/routes; idempotency-facet contains
/// generation/entries. The root is SHA-256 of the exact complete canonical envelope bytes and is
/// returned separately, never serialized into its own preimage.
/// A digest never includes itself. These bytes do not change any existing fingerprint protocol.
/// </remarks>
public static class RouteCatalogCodec
{
    /// <summary>Maximum accepted catalog or construction-input byte length.</summary>
    public const int MaximumByteLength = 4 * 1024 * 1024;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        MaxDepth = 32,
    };

    /// <summary>Encodes a complete validated envelope into canonical bytes.</summary>
    public static byte[] Encode(RouteCatalogEnvelope envelope)
    {
        if (envelope is null)
        {
            throw Failure("$", "missing-document");
        }

        RouteCatalogValidator.Validate(envelope);
        byte[] bytes = CanonicalBytes(Normalize(envelope));
        if (bytes.Length > MaximumByteLength)
        {
            throw Failure("$", "size-limit");
        }

        return bytes;
    }

    /// <summary>Rejects malformed, duplicate, unknown, unsupported, invalid, or noncanonical input.</summary>
    public static RouteCatalogEnvelope Decode(ReadOnlyMemory<byte> bytes, string? expectedRootDigest = null)
    {
        RouteCatalogEnvelope envelope = ReadClosedDocument<RouteCatalogEnvelope>(bytes);
        byte[] canonical = Encode(envelope);
        if (!bytes.Span.SequenceEqual(canonical))
        {
            throw Failure("$", "noncanonical-encoding");
        }

        if (expectedRootDigest is not null && expectedRootDigest != Convert.ToHexStringLower(SHA256.HashData(bytes.Span)))
        {
            throw Failure("$", "root-digest-mismatch");
        }

        return envelope;
    }

    /// <summary>Reads a closed construction document using the same duplicate/unknown/required-field rules.</summary>
    /// <typeparam name="T">The explicit catalog construction input model.</typeparam>
    /// <param name="bytes">The JSON document; construction inputs may contain whitespace.</param>
    /// <returns>The input model, which still requires semantic validation.</returns>
    internal static T ReadClosedDocument<T>(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length is 0 or > MaximumByteLength)
        {
            throw Failure("$", "size-limit");
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
            RejectDuplicates(document.RootElement);
            return document.RootElement.Deserialize<T>(Options) ?? throw Failure("$", "missing-document");
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            throw Failure("$", "invalid-json-or-closed-schema");
        }
    }

    /// <summary>Validates metadata and calculates both facet digests. Existing entry digests must already match.</summary>
    public static RouteCatalogEnvelope Seal(RouteCatalogEnvelope envelope)
    {
        if (envelope is null)
        {
            throw Failure("$", "missing-document");
        }

        RouteCatalogValidator.ValidateMetadata(envelope);
        RouteCatalogEnvelope normalized = Normalize(envelope);
        normalized = normalized with
        {
            RouteFacetDigest = ComputeRouteFacetDigest(normalized),
            IdempotencyFacetDigest = ComputeIdempotencyFacetDigest(normalized),
        };
        return normalized;
    }

    /// <summary>Hashes a validated explicit contract declaration using its schema-versioned declaration preimage.</summary>
    public static string ComputeDeclarationDigest(RouteContractDeclaration declaration)
    {
        if (declaration is null)
        {
            throw Failure("$", "missing-document");
        }

        RouteCatalogValidator.ValidateDeclaration(declaration);
        return Hash(new { purpose = "route-declaration", schemaVersion = declaration.Execution is null ? 1 : 2, declaration = Normalize(declaration) });
    }

    /// <summary>Hashes a validated static descriptor schema, never an intent or key value.</summary>
    public static string ComputeDescriptorDigest(IdempotencyDescriptorSchema descriptor)
    {
        if (descriptor is null)
        {
            throw Failure("$", "missing-document");
        }

        RouteCatalogValidator.ValidateDescriptor(descriptor);
        return Hash(new { purpose = "idempotency-descriptor", schemaVersion = 1, descriptor = Normalize(descriptor) });
    }

    /// <summary>Hashes all static idempotency entry metadata except its own content digest.</summary>
    public static string ComputeIdempotencyEntryDigest(IdempotencyCatalogEntry entry)
    {
        if (entry is null)
        {
            throw Failure("$", "missing-document");
        }

        RouteCatalogValidator.ValidateIdempotencyMetadata(entry, "$.idempotencyEntry");
        return Hash(new
        {
            purpose = "idempotency-entry",
            schemaVersion = 1,
            entry = new
            {
                entry.RouteId,
                entry.Generation,
                entry.Domain,
                entry.CommandType,
                entry.AdapterId,
                entry.OperationId,
                entry.PolicyVersion,
                DescriptorSchema = Normalize(entry.DescriptorSchema),
                entry.DescriptorDigest,
                entry.RetentionTier,
                entry.ActiveDigestKeyGeneration,
                ReaderDigestKeyGenerations = entry.ReaderDigestKeyGenerations.ToArray(),
                entry.SecretStore,
                entry.LogicalSecretName,
                entry.SecretMapGeneration,
                KeyMap = entry.KeyMap.OrderBy(k => k.Generation, StringComparer.Ordinal).ToArray(),
                entry.ConsumerAppId,
            },
        });
    }

    /// <summary>Computes the complete routing facet preimage.</summary>
    internal static string ComputeRouteFacetDigest(RouteCatalogEnvelope envelope) => Hash(new
    {
        purpose = "route-facet",
        envelope.SchemaVersion,
        envelope.Generation,
        Normalize(envelope).Declarations,
        Normalize(envelope).Routes,
    });

    /// <summary>Computes the complete idempotency facet preimage.</summary>
    internal static string ComputeIdempotencyFacetDigest(RouteCatalogEnvelope envelope) => Hash(new
    {
        purpose = "idempotency-facet",
        envelope.SchemaVersion,
        envelope.Generation,
        entries = Normalize(envelope).Idempotency,
    });

    /// <summary>Returns lowercase SHA-256 of the exact canonical envelope bytes, outside the envelope.</summary>
    public static string ComputeRootDigest(RouteCatalogEnvelope envelope) => Convert.ToHexStringLower(SHA256.HashData(Encode(envelope)));

    private static string Hash<T>(T value) => Convert.ToHexStringLower(SHA256.HashData(CanonicalBytes(value)));

    private static byte[] CanonicalBytes<T>(T value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteCanonical(writer, JsonSerializer.SerializeToElement(value, Options));
        }

        return stream.ToArray();
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (JsonProperty property in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                WriteCanonical(writer, property.Value);
            }

            writer.WriteEndObject();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (JsonElement item in element.EnumerateArray())
            {
                WriteCanonical(writer, item);
            }

            writer.WriteEndArray();
        }
        else
        {
            element.WriteTo(writer);
        }
    }

    private static void RejectDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw Failure("$", "duplicate-field");
                }

                RejectDuplicates(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                RejectDuplicates(item);
            }
        }
    }

    private static RouteContractDeclaration Normalize(RouteContractDeclaration declaration) => declaration with
    {
        FallbackTypes = declaration.FallbackTypes.Order(StringComparer.Ordinal).ToArray(),
        Execution = declaration.Execution is { } execution ? execution with
        {
            Aliases = execution.Aliases.Order(StringComparer.Ordinal).ToArray(),
            ProjectionOutputs = execution.ProjectionOutputs.Order(StringComparer.Ordinal).ToArray(),
        } : null,
    };

    private static IdempotencyDescriptorSchema Normalize(IdempotencyDescriptorSchema schema) => schema with
    {
        Fields = schema.Fields.OrderBy(f => f.Name, StringComparer.Ordinal).ToArray(),
    };

    private static RouteCatalogEnvelope Normalize(RouteCatalogEnvelope envelope) => envelope with
    {
        Declarations = envelope.Declarations.Select(Normalize).OrderBy(d => d.RouteId, StringComparer.Ordinal).ToArray(),
        Routes = envelope.Routes.OrderBy(r => r.RouteId, StringComparer.Ordinal).ToArray(),
        Idempotency = envelope.Idempotency.Select(i => i with
        {
            DescriptorSchema = Normalize(i.DescriptorSchema),
            ReaderDigestKeyGenerations = i.ReaderDigestKeyGenerations.ToArray(),
            KeyMap = i.KeyMap.OrderBy(k => k.Generation, StringComparer.Ordinal).ToArray(),
        }).OrderBy(i => i.RouteId, StringComparer.Ordinal).ToArray(),
    };

    private static RouteCatalogValidationException Failure(string path, string code) => new([new(path, code)]);
}
