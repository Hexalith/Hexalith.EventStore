using System.Text.RegularExpressions;

using Hexalith.EventStore.Contracts.Authorization;

namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Fail-closed structural, declaration, join, generation, credential, and digest validation.</summary>
public static partial class RouteCatalogValidator
{
    /// <summary>Validates the complete envelope, including all supplied content digests.</summary>
    public static void Validate(RouteCatalogEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ValidateMetadata(envelope);
        Require(envelope.RouteFacetDigest == RouteCatalogCodec.ComputeRouteFacetDigest(envelope), "$.routeFacetDigest", "digest-mismatch");
        Require(envelope.IdempotencyFacetDigest == RouteCatalogCodec.ComputeIdempotencyFacetDigest(envelope), "$.idempotencyFacetDigest", "digest-mismatch");
    }

    /// <summary>Validates one declaration before hashing it.</summary>
    public static void ValidateDeclaration(RouteContractDeclaration declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        CheckDeclaration(declaration, "$.declaration");
    }

    /// <summary>Validates one static descriptor schema before hashing it.</summary>
    public static void ValidateDescriptor(IdempotencyDescriptorSchema descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        CheckDescriptor(descriptor, "$.descriptor");
    }

    /// <summary>Validates metadata before computing envelope digests; entry digests are never repaired.</summary>
    internal static void ValidateMetadata(RouteCatalogEnvelope envelope)
    {
        Require(envelope is not null, "$", "missing-document");
        Require(envelope.SchemaVersion == 1, "$.schemaVersion", "unsupported-version");
        Require(envelope.Generation is > 0 and <= 9007199254740991, "$.generation", "invalid-generation");
        Token(envelope.Profile, "$.profile");
        Require(!envelope.AllowRuntimeOverrides || envelope.Profile == "Development", "$.allowRuntimeOverrides", "override-outside-development");
        List(envelope.Declarations, "$.declarations", allowEmpty: false);
        List(envelope.Routes, "$.routes", allowEmpty: false);
        List(envelope.Idempotency, "$.idempotency");

        var declarations = new Dictionary<string, RouteContractDeclaration>(StringComparer.Ordinal);
        var exactKeys = new HashSet<string>(StringComparer.Ordinal);
        var fallbackKeys = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < envelope.Declarations.Count; index++)
        {
            RouteContractDeclaration declaration = envelope.Declarations[index];
            string path = $"$.declarations[{index}]";
            CheckDeclaration(declaration, path);
            Require(declarations.TryAdd(declaration.RouteId, declaration), path + ".routeId", "duplicate-route-id");
            string keyPrefix = $"{(declaration.Kind == "projection" ? "projection" : "message")}|{declaration.Domain}|";
            string discriminator = declaration.Kind == "projection" ? declaration.ProjectionType! : declaration.MessageType!;
            if (discriminator == "*")
            {
                foreach (string type in declaration.FallbackTypes)
                {
                    Require(fallbackKeys.Add(keyPrefix + type), path + ".fallbackTypes", "ambiguous-fallback");
                }
            }
            else
            {
                Require(exactKeys.Add(keyPrefix + discriminator), path, "duplicate-route-key");
            }
        }

        var bound = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < envelope.Routes.Count; index++)
        {
            RouteCatalogEntry route = envelope.Routes[index];
            string path = $"$.routes[{index}]";
            Require(declarations.TryGetValue(route.RouteId ?? string.Empty, out RouteContractDeclaration? declaration), path + ".routeId", "missing-declaration");
            Require(bound.Add(route.RouteId!), path + ".routeId", "duplicate-route-id");
            Require(route.Generation == envelope.Generation, path + ".generation", "generation-mismatch");
            Name(route.AppId, path + ".appId");
            Require(IsMatch(route.Method, MethodPattern()), path + ".method", "invalid-method");
            Require(route.ContractVersion == declaration!.ContractVersion, path + ".contractVersion", "declaration-mismatch");
            Require(route.CredentialKind == declaration.CredentialKind, path + ".credentialKind", "declaration-mismatch");
            Require(route.Operation == declaration.Operation, path + ".operation", "declaration-mismatch");
            Require(route.DeclarationDigest == RouteCatalogCodec.ComputeDeclarationDigest(declaration), path + ".declarationDigest", "declaration-digest-mismatch");
            Require(!route.IsRuntimeOverride || (envelope.Profile == "Development" && envelope.AllowRuntimeOverrides), path + ".isRuntimeOverride", "undeclared-override");
        }

        Require(bound.Count == declarations.Count, "$.routes", "missing-binding");
        var commands = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < envelope.Idempotency.Count; index++)
        {
            IdempotencyCatalogEntry entry = envelope.Idempotency[index];
            string path = $"$.idempotency[{index}]";
            Require(declarations.TryGetValue(entry.RouteId ?? string.Empty, out RouteContractDeclaration? declaration) && declaration.Kind == "command", path + ".routeId", "invalid-command-join");
            Require(commands.Add(entry.RouteId!), path + ".routeId", "duplicate-idempotency-entry");
            Require(entry.Domain == declaration!.Domain && entry.CommandType == declaration.MessageType, path, "command-join-mismatch");
            Require(entry.Generation == envelope.Generation, path + ".generation", "generation-mismatch");
            Token(entry.AdapterId, path + ".adapterId");
            Token(entry.OperationId, path + ".operationId");
            Token(entry.PolicyVersion, path + ".policyVersion");
            CheckDescriptor(entry.DescriptorSchema, path + ".descriptorSchema");
            Require(entry.DescriptorDigest == RouteCatalogCodec.ComputeDescriptorDigest(entry.DescriptorSchema), path + ".descriptorDigest", "descriptor-digest-mismatch");
            Require(entry.RetentionTier is "mutation" or "commit", path + ".retentionTier", "invalid-retention-tier");
            Token(entry.ActiveDigestKeyGeneration, path + ".activeDigestKeyGeneration");
            List(entry.ReaderDigestKeyGenerations, path + ".readerDigestKeyGenerations", allowEmpty: false);
            var readers = new HashSet<string>(StringComparer.Ordinal);
            foreach (string reader in entry.ReaderDigestKeyGenerations)
            {
                Token(reader, path + ".readerDigestKeyGenerations");
                Require(readers.Add(reader), path + ".readerDigestKeyGenerations", "duplicate-generation");
            }

            Require(readers.Contains(entry.ActiveDigestKeyGeneration), path + ".activeDigestKeyGeneration", "active-generation-not-readable");
            Name(entry.SecretStore, path + ".secretStore");
            Token(entry.LogicalSecretName, path + ".logicalSecretName");
            Token(entry.SecretMapGeneration, path + ".secretMapGeneration");
            Name(entry.ConsumerAppId, path + ".consumerAppId");
            List(entry.KeyMap, path + ".keyMap", allowEmpty: false);
            var generations = new HashSet<string>(StringComparer.Ordinal);
            var mapKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (DigestKeyMapReference key in entry.KeyMap)
            {
                Token(key.Generation, path + ".keyMap.generation");
                Token(key.MapKey, path + ".keyMap.mapKey");
                Require(generations.Add(key.Generation) && mapKeys.Add(key.MapKey), path + ".keyMap", "duplicate-key-reference");
            }

            Require(generations.SetEquals(readers), path + ".keyMap", "reader-generation-map-mismatch");
        }

        Require(commands.Count == declarations.Values.Count(d => d.Kind == "command"), "$.idempotency", "missing-command-idempotency");
    }

    private static void CheckDeclaration(RouteContractDeclaration declaration, string path)
    {
        Require(declaration is not null, path, "missing-declaration");
        Name(declaration.RouteId, path + ".routeId");
        Require(declaration.Kind is "command" or "query" or "projection", path + ".kind", "unsupported-kind");
        Name(declaration.Domain, path + ".domain");
        Require(IsMatch(declaration.ContractVersion, VersionPattern()), path + ".contractVersion", "invalid-contract-version");
        Require(declaration.CredentialKind == "workload", path + ".credentialKind", "invalid-target-credential");
        string operation = declaration.Kind switch
        {
            "command" => EventStoreOperationIdentifiers.DomainServiceProcess,
            "query" => EventStoreOperationIdentifiers.DomainServiceQuery,
            _ => EventStoreOperationIdentifiers.DomainServiceProject,
        };
        Require(declaration.Operation == operation, path + ".operation", "invalid-target-operation");
        Require(declaration.Kind == "command" ? declaration.MessageIdVersion is 1 or 2 : declaration.MessageIdVersion is null,
            path + ".messageIdVersion", "invalid-message-id-version");
        if (declaration.Kind == "projection")
        {
            Require(declaration.MessageType is null, path + ".messageType", "unexpected-field");
        }
        else
        {
            Require(declaration.MessageType == "*" || IsMatch(declaration.MessageType, NamePattern()), path + ".messageType", "missing-or-invalid-message-type");
        }

        if (declaration.Kind == "command")
        {
            Require(declaration.ProjectionType is null, path + ".projectionType", "unexpected-field");
        }
        else
        {
            Require((declaration.Kind == "projection" && declaration.ProjectionType == "*") || IsMatch(declaration.ProjectionType, NamePattern()),
                path + ".projectionType", "missing-or-invalid-projection-type");
        }

        List(declaration.FallbackTypes, path + ".fallbackTypes");
        bool fallback = (declaration.Kind == "projection" ? declaration.ProjectionType : declaration.MessageType) == "*";
        Require(fallback == (declaration.FallbackTypes.Count > 0), path + ".fallbackTypes", "undeclared-or-unbounded-fallback");
        // Every command needs its own AD-17 declaration and AD-25 join; v1 forbids grouped command policies.
        Require(!fallback || declaration.Kind != "command", path + ".fallbackTypes", "command-requires-exact-declaration");
        var types = new HashSet<string>(StringComparer.Ordinal);
        foreach (string type in declaration.FallbackTypes)
        {
            Name(type, path + ".fallbackTypes");
            Require(types.Add(type), path + ".fallbackTypes", "duplicate-fallback-type");
        }

        if (declaration.PlatformAdmission is { } admission)
        {
            Require(IsMatch(admission.Namespace, PlatformPattern()), path + ".platformAdmission.namespace", "invalid-platform-namespace");
            Require(IsMatch(admission.RequiredHumanPermission, PermissionPattern()), path + ".platformAdmission.requiredHumanPermission", "missing-or-invalid-human-permission");
            Require(!admission.RequiredHumanPermission.StartsWith("domain-service:", StringComparison.Ordinal), path + ".platformAdmission.requiredHumanPermission", "workload-permission-is-not-human-admission");
        }
    }

    private static void CheckDescriptor(IdempotencyDescriptorSchema descriptor, string path)
    {
        Require(descriptor is not null, path, "missing-descriptor-schema");
        Token(descriptor.SchemaId, path + ".schemaId");
        Require(descriptor.Version > 0, path + ".version", "invalid-descriptor-version");
        List(descriptor.Fields, path + ".fields", allowEmpty: false);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (IdempotencyDescriptorField field in descriptor.Fields)
        {
            Name(field.Name, path + ".fields.name");
            Require(names.Add(field.Name), path + ".fields.name", "duplicate-descriptor-field");
            Require(field.Type is "string" or "int64" or "boolean" or "string-list", path + ".fields.type", "unsupported-descriptor-type");
        }
    }

    private static void Name(string? value, string path) => Require(IsMatch(value, NamePattern()), path, "missing-or-invalid-name");

    private static void Token(string? value, string path) => Require(IsMatch(value, TokenPattern()), path, "missing-or-invalid-token");

    private static bool IsMatch(string? value, Regex pattern) => value is { Length: > 0 and <= 128 } && pattern.IsMatch(value);

    private static void List<T>(IReadOnlyList<T>? list, string path, bool allowEmpty = true)
    {
        Require(list is not null && list.Count <= 4096 && (allowEmpty || list.Count > 0), path, "missing-or-invalid-list");
        Require(list.All(item => item is not null), path, "null-entry");
    }

    private static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition, string path, string code)
    {
        if (!condition)
        {
            throw new RouteCatalogValidationException([new(path, code)]);
        }
    }

    [GeneratedRegex("\\A[a-z0-9]+(?:-[a-z0-9]+)*\\z", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();

    [GeneratedRegex("\\A[A-Za-z0-9][A-Za-z0-9._:/-]*\\z", RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();

    [GeneratedRegex("\\Av[1-9][0-9]*\\z", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();

    [GeneratedRegex("\\A[a-z0-9]+(?:[/-][a-z0-9]+)*\\z", RegexOptions.CultureInvariant)]
    private static partial Regex MethodPattern();

    [GeneratedRegex("\\Aplatform:[a-z0-9]+(?:-[a-z0-9]+)*\\z", RegexOptions.CultureInvariant)]
    private static partial Regex PlatformPattern();

    [GeneratedRegex("\\A[a-z0-9]+(?:-[a-z0-9]+)*(?::[a-z0-9]+(?:-[a-z0-9]+)*)+\\z", RegexOptions.CultureInvariant)]
    private static partial Regex PermissionPattern();
}
