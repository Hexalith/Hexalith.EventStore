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
        Require(envelope.SchemaVersion is 1 or 2 or 3, "$.schemaVersion", "unsupported-version");
        Require(envelope.Generation is > 0 and <= 9007199254740991, "$.generation", "invalid-generation");
        Token(envelope.Profile, "$.profile");
        Require(!envelope.AllowRuntimeOverrides || envelope.Profile == "Development", "$.allowRuntimeOverrides", "override-outside-development");
        List(envelope.Declarations, "$.declarations", allowEmpty: false);
        List(envelope.Routes, "$.routes", allowEmpty: false);
        List(envelope.Idempotency, "$.idempotency");

        var declarations = new Dictionary<string, RouteContractDeclaration>(StringComparer.Ordinal);
        var exactKeys = new HashSet<string>(StringComparer.Ordinal);
        var aliasKeys = new HashSet<string>(StringComparer.Ordinal);
        var compatibilityAliasKeys = new HashSet<string>(StringComparer.Ordinal);
        var normalizedExactAliasKeys = new HashSet<string>(StringComparer.Ordinal);
        var fallbackKeys = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < envelope.Declarations.Count; index++)
        {
            RouteContractDeclaration declaration = envelope.Declarations[index];
            string path = $"$.declarations[{index}]";
            CheckDeclaration(declaration, path);
            Require((envelope.SchemaVersion >= 2) == (declaration.Execution is not null), path + ".execution", "schema-execution-mismatch");
            Require(declarations.TryAdd(declaration.RouteId, declaration), path + ".routeId", "duplicate-route-id");
            string keyPrefix = $"{(declaration.Kind == "projection" ? "projection" : "message")}|{declaration.Domain}|";
            if (declaration.Execution is { } execution)
            {
                foreach (string alias in execution.Aliases)
                {
                    Require(aliasKeys.Add(keyPrefix + alias), path + ".execution.aliases", "ambiguous-handler-alias");
                    string normalizedKey = keyPrefix + CommandDiscriminatorCompatibility.LookupKey(alias);
                    if (execution.AliasPolicy == "clr-short-name-v1")
                    {
                        Require(!normalizedExactAliasKeys.Contains(normalizedKey), path + ".execution.aliases", "ambiguous-handler-alias");
                        compatibilityAliasKeys.Add(normalizedKey);
                    }
                    else
                    {
                        Require(!compatibilityAliasKeys.Contains(normalizedKey), path + ".execution.aliases", "ambiguous-handler-alias");
                        normalizedExactAliasKeys.Add(normalizedKey);
                    }
                }
            }

            string discriminator = declaration.Kind == "projection" ? declaration.ProjectionType! : declaration.MessageType!;
            if (discriminator == "*")
            {
                for (int typeIndex = 0; typeIndex < declaration.FallbackTypes.Count; typeIndex++)
                {
                    Require(fallbackKeys.Add(keyPrefix + declaration.FallbackTypes[typeIndex]), $"{path}.fallbackTypes[{typeIndex}]", "ambiguous-fallback");
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
            AppId(route.AppId, path + ".appId");
            Require(envelope.SchemaVersion == 1 ? route.ResolverTenantPattern is null
                : route.ResolverTenantPattern == "*" || (route.ResolverTenantPattern is { Length: > 0 and <= 64 } && DomainPattern().IsMatch(route.ResolverTenantPattern)),
                path + ".resolverTenantPattern", "invalid-resolver-tenant-pattern");
            Require(declaration!.Execution?.TargetKind == "projection-actor" ? route.Method == "QueryAsync" : IsMatch(route.Method, MethodPattern()), path + ".method", "invalid-method");
            Require(route.ContractVersion == declaration!.ContractVersion, path + ".contractVersion", "declaration-mismatch");
            Require(route.CredentialKind == declaration.CredentialKind, path + ".credentialKind", "declaration-mismatch");
            Require(route.Operation == declaration.Operation, path + ".operation", "declaration-mismatch");
            Require(route.DeclarationDigest == RouteCatalogCodec.ComputeDeclarationDigest(declaration), path + ".declarationDigest", "declaration-digest-mismatch");
            Require(!route.IsRuntimeOverride || (envelope.Profile == "Development" && envelope.AllowRuntimeOverrides), path + ".isRuntimeOverride", "undeclared-override");
        }

        Require(bound.Count == declarations.Count, "$.routes", "missing-binding");
        if (envelope.SchemaVersion == 3)
        {
            List(envelope.Operations, "$.operations", allowEmpty: false);
            List(envelope.RetainedIdempotency, "$.retainedIdempotency");
            var operations = new HashSet<string>(StringComparer.Ordinal);
            foreach (RouteOperationContract operation in envelope.Operations!)
            {
                ValidateOperationContract(operation);
                Require(operations.Add(operation.RouteId), operation.RouteId, "duplicate-operation-contract");
                Require(declarations.TryGetValue(operation.RouteId, out var declaration)
                    && declaration.ContractVersion == operation.ContractVersion && declaration.Kind == operation.Kind
                    && declaration.Domain == operation.Domain && (declaration.MessageType ?? declaration.ProjectionType) == operation.MessageType,
                    operation.RouteId, "invalid-operation-join");
                Require(operation.Ingress != "public" || declaration!.Kind is "command" or "query"
                    && declaration.Execution?.RequiresDelegatedContext != true, operation.RouteId, "internal-operation-is-public");
                Require(operation.Digest == RouteCatalogCodec.ComputeOperationContractDigest(operation), operation.RouteId, "operation-contract-digest-mismatch");
                Require(envelope.Routes.Single(r => r.RouteId == operation.RouteId).OperationContractDigest == operation.Digest,
                    operation.RouteId, "route-operation-digest-mismatch");
            }
            Require(operations.Count == declarations.Count, "$.operations", "missing-operation-contract");
            var retained = new HashSet<string>(StringComparer.Ordinal);
            var historicalIdentities = new Dictionary<(string Route, long Generation), string>();
            foreach (RetainedIdempotencyCatalogEntry historical in envelope.RetainedIdempotency!)
            {
                ValidateIdempotencyMetadata(historical.Entry, "$.retainedIdempotency");
                Require(historical.Entry.Generation < envelope.Generation, "$.retainedIdempotency", "invalid-historical-generation");
                Require(historical.Entry.ContentDigest == RouteCatalogCodec.ComputeIdempotencyEntryDigest(historical.Entry),
                    historical.Entry.RouteId, "historical-content-digest-mismatch");
                Require(retained.Add(historical.Entry.ContentDigest), historical.Entry.RouteId, "duplicate-historical-entry");
                var identity = (historical.Entry.RouteId, historical.Entry.Generation);
                Require(!historicalIdentities.TryGetValue(identity, out string? digest) || digest == historical.Entry.ContentDigest,
                    historical.Entry.RouteId, "conflicting-historical-identity");
                historicalIdentities[identity] = historical.Entry.ContentDigest;
            }
        }
        else
        {
            Require(envelope.Operations is null && envelope.RetainedIdempotency is null
                && envelope.Routes.All(r => r.OperationContractDigest is null), "$", "schema-three-fields-in-historical-envelope");
        }
        var commands = new HashSet<string>(StringComparer.Ordinal);
        var secretMapGenerations = new Dictionary<(string Consumer, string Store, string Name), string>();
        var secretMapKeys = new Dictionary<(string Consumer, string Store, string Name, string Generation), string>();
        var secretKeyGenerations = new Dictionary<(string Consumer, string Store, string Name, string MapKey), string>();
        for (int index = 0; index < envelope.Idempotency.Count; index++)
        {
            IdempotencyCatalogEntry entry = envelope.Idempotency[index];
            string path = $"$.idempotency[{index}]";
            Require(declarations.TryGetValue(entry.RouteId ?? string.Empty, out RouteContractDeclaration? declaration) && declaration.Kind == "command", path + ".routeId", "invalid-command-join");
            Require(commands.Add(entry.RouteId!), path + ".routeId", "duplicate-idempotency-entry");
            Require(entry.Domain == declaration!.Domain && entry.CommandType == declaration.MessageType, path, "command-join-mismatch");
            Require(entry.Generation == envelope.Generation, path + ".generation", "generation-mismatch");
            ValidateIdempotencyMetadata(entry, path);
            var secretReference = (entry.ConsumerAppId, entry.SecretStore, entry.LogicalSecretName);
            Require(!secretMapGenerations.TryGetValue(secretReference, out string? generation) || generation == entry.SecretMapGeneration,
                path + ".secretMapGeneration", "conflicting-secret-map-generation");
            secretMapGenerations[secretReference] = entry.SecretMapGeneration;
            for (int keyIndex = 0; keyIndex < entry.KeyMap.Count; keyIndex++)
            {
                DigestKeyMapReference key = entry.KeyMap[keyIndex];
                var reference = (entry.ConsumerAppId, entry.SecretStore, entry.LogicalSecretName, key.Generation);
                Require(!secretMapKeys.TryGetValue(reference, out string? mapKey) || mapKey == key.MapKey,
                    $"{path}.keyMap[{keyIndex}].mapKey", "conflicting-secret-map-key");
                secretMapKeys[reference] = key.MapKey;
                var keyReference = (entry.ConsumerAppId, entry.SecretStore, entry.LogicalSecretName, key.MapKey);
                Require(!secretKeyGenerations.TryGetValue(keyReference, out string? keyGeneration) || keyGeneration == key.Generation,
                    $"{path}.keyMap[{keyIndex}].generation", "conflicting-secret-map-key-generation");
                secretKeyGenerations[keyReference] = key.Generation;
            }
            Require(entry.ContentDigest == RouteCatalogCodec.ComputeIdempotencyEntryDigest(entry), path + ".contentDigest", "idempotency-content-digest-mismatch");
        }

        Require(commands.Count == declarations.Values.Count(d => d.Kind == "command"), "$.idempotency", "missing-command-idempotency");
    }

    /// <summary>Validates static idempotency metadata before entry hashing; no join or digest is inferred.</summary>
    internal static void ValidateIdempotencyMetadata(IdempotencyCatalogEntry entry, string path)
    {
        Require(entry is not null, path, "missing-idempotency-entry");
        Name(entry.RouteId, path + ".routeId");
        Domain(entry.Domain, path + ".domain");
        Discriminator(entry.CommandType, path + ".commandType");
        Require(entry.Generation is > 0 and <= 9007199254740991, path + ".generation", "invalid-generation");
        Token(entry.AdapterId, path + ".adapterId");
        Token(entry.OperationId, path + ".operationId");
        Token(entry.PolicyVersion, path + ".policyVersion");
        CheckDescriptor(entry.DescriptorSchema, path + ".descriptorSchema");
        Require(entry.DescriptorDigest == RouteCatalogCodec.ComputeDescriptorDigest(entry.DescriptorSchema), path + ".descriptorDigest", "descriptor-digest-mismatch");
        Require(entry.RetentionTier is "mutation" or "commit", path + ".retentionTier", "invalid-retention-tier");
        Token(entry.ActiveDigestKeyGeneration, path + ".activeDigestKeyGeneration");
        List(entry.ReaderDigestKeyGenerations, path + ".readerDigestKeyGenerations", allowEmpty: false);
        var readers = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < entry.ReaderDigestKeyGenerations.Count; index++)
        {
            string reader = entry.ReaderDigestKeyGenerations[index];
            string readerPath = $"{path}.readerDigestKeyGenerations[{index}]";
            Token(reader, readerPath);
            Require(readers.Add(reader), readerPath, "duplicate-generation");
        }

        Require(readers.Contains(entry.ActiveDigestKeyGeneration), path + ".activeDigestKeyGeneration", "active-generation-not-readable");
        Name(entry.SecretStore, path + ".secretStore");
        Token(entry.LogicalSecretName, path + ".logicalSecretName");
        Token(entry.SecretMapGeneration, path + ".secretMapGeneration");
        AppId(entry.ConsumerAppId, path + ".consumerAppId");
        List(entry.KeyMap, path + ".keyMap", allowEmpty: false);
        var generations = new HashSet<string>(StringComparer.Ordinal);
        var mapKeys = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < entry.KeyMap.Count; index++)
        {
            DigestKeyMapReference key = entry.KeyMap[index];
            string keyPath = $"{path}.keyMap[{index}]";
            Token(key.Generation, keyPath + ".generation");
            Token(key.MapKey, keyPath + ".mapKey");
            Require(key.MapKey != "generation", keyPath + ".mapKey", "reserved-generation-map-key");
            Require(generations.Add(key.Generation), keyPath + ".generation", "duplicate-key-reference");
            Require(mapKeys.Add(key.MapKey), keyPath + ".mapKey", "duplicate-key-reference");
        }

        Require(generations.SetEquals(readers), path + ".keyMap", "reader-generation-map-mismatch");
    }

    /// <summary>Validates value-free owner schemas and the closed eligibility vocabulary before hashing.</summary>
    public static void ValidateOperationContract(RouteOperationContract operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        Name(operation.RouteId, "$.operation.routeId");
        Require(IsMatch(operation.ContractVersion, VersionPattern()), operation.RouteId, "invalid-operation-version");
        Require(operation.Kind is "command" or "query" or "projection", operation.RouteId, "invalid-operation-kind");
        Domain(operation.Domain, operation.RouteId + ".domain");
        Discriminator(operation.MessageType, operation.RouteId + ".messageType");
        Require(operation.Ingress is "public" or "internal", operation.RouteId, "invalid-operation-ingress");
        Require(operation.Eligibility is "agent-eligible" or "UI-only" or "confirmation-required" or "step-up",
            operation.RouteId, "invalid-operation-eligibility");
        Require(operation.RequestSchema.ValueKind == System.Text.Json.JsonValueKind.Object
            && operation.ResultSchema.ValueKind == System.Text.Json.JsonValueKind.Object, operation.RouteId, "missing-operation-schema");
    }

    private static void CheckDeclaration(RouteContractDeclaration declaration, string path)
    {
        Require(declaration is not null, path, "missing-declaration");
        Name(declaration.RouteId, path + ".routeId");
        Require(declaration.Kind is "command" or "query" or "projection", path + ".kind", "unsupported-kind");
        Domain(declaration.Domain, path + ".domain");
        Require(IsMatch(declaration.ContractVersion, VersionPattern()), path + ".contractVersion", "invalid-contract-version");
        Require(declaration.CredentialKind == "workload", path + ".credentialKind", "invalid-target-credential");
        string operation = declaration.Kind switch
        {
            "command" => EventStoreOperationIdentifiers.DomainServiceProcess,
            "query" => declaration.Execution?.TargetKind == "projection-actor" ? EventStoreOperationIdentifiers.ProjectionQuery : EventStoreOperationIdentifiers.DomainServiceQuery,
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
            Require(declaration.MessageType == "*" || (declaration.Execution is null ? IsMatch(declaration.MessageType, NamePattern()) : IsDiscriminator(declaration.MessageType)), path + ".messageType", "missing-or-invalid-message-type");
        }

        if (declaration.Kind == "command")
        {
            Require(declaration.ProjectionType is null, path + ".projectionType", "unexpected-field");
        }
        else
        {
            Require((declaration.Kind == "query" && declaration.Execution?.QueryKind == "computed" && declaration.ProjectionType is null) || (declaration.Kind == "projection" && declaration.ProjectionType == "*") || IsMatch(declaration.ProjectionType, NamePattern()),
                path + ".projectionType", "missing-or-invalid-projection-type");
        }

        List(declaration.FallbackTypes, path + ".fallbackTypes");
        bool fallback = (declaration.Kind == "projection" ? declaration.ProjectionType : declaration.MessageType) == "*";
        Require(fallback == (declaration.FallbackTypes.Count > 0), path + ".fallbackTypes", "undeclared-or-unbounded-fallback");
        // Every command needs its own AD-17 declaration and AD-25 join; v1 forbids grouped command policies.
        Require(!fallback || declaration.Kind != "command", path + ".fallbackTypes", "command-requires-exact-declaration");
        var types = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < declaration.FallbackTypes.Count; index++)
        {
            string type = declaration.FallbackTypes[index];
            string typePath = $"{path}.fallbackTypes[{index}]";
            Name(type, typePath);
            Require(types.Add(type), typePath, "duplicate-fallback-type");
        }

        if (declaration.Execution is { } execution)
        {
            Discriminator(execution.HandlerType, path + ".execution.handlerType");
            Require(execution.TargetKind is "domain-service" or "projection-actor", path + ".execution.targetKind", "unsupported-target-kind");
            Require(execution.TargetKind == "projection-actor" ? execution.ActorType == "ProjectionActor" : execution.ActorType is null, path + ".execution.actorType", "invalid-actor-type");
            Require(!execution.RequiresDelegatedContext || (declaration.Kind == "query" && execution.TargetKind == "domain-service"), path + ".execution.requiresDelegatedContext", "invalid-delegated-context");
            Require(execution.TargetKind != "projection-actor" || declaration.Kind == "query", path + ".execution.targetKind", "actor-requires-query");
            Require(execution.AliasPolicy == "exact" || (execution.AliasPolicy == "clr-short-name-v1" && declaration.Kind == "command"), path + ".execution.aliasPolicy", "unsupported-alias-policy");
            Require(!fallback, path + ".fallbackTypes", "execution-requires-exact-handler");
            List(execution.Aliases, path + ".execution.aliases", allowEmpty: false);
            Require(execution.Aliases.Distinct(StringComparer.Ordinal).Count() == execution.Aliases.Count, path + ".execution.aliases", "duplicate-handler-alias");
            foreach (string alias in execution.Aliases)
            {
                Discriminator(alias, path + ".execution.aliases");
                Require(execution.AliasPolicy != "clr-short-name-v1" || CommandDiscriminatorCompatibility.LookupKey(alias) == alias, path + ".execution.aliases", "alias-is-not-lookup-key");
            }
            Require(execution.Aliases.Contains(declaration.Kind == "projection" ? declaration.ProjectionType! : declaration.MessageType!, StringComparer.Ordinal), path + ".execution.aliases", "missing-primary-alias");
            Require(declaration.Kind == "query" ? execution.QueryKind is "projection" or "computed" : execution.QueryKind is null, path + ".execution.queryKind", "invalid-query-kind");
            Require(execution.TargetKind != "projection-actor" || execution.QueryKind == "projection", path + ".execution.queryKind", "actor-requires-projection");
            List(execution.ProjectionOutputs, path + ".execution.projectionOutputs", allowEmpty: declaration.Kind != "projection");
            Require(declaration.Kind == "projection" || execution.ProjectionOutputs.Count == 0, path + ".execution.projectionOutputs", "unexpected-projection-outputs");
            var outputs = new HashSet<string>(StringComparer.Ordinal);
            foreach (string output in execution.ProjectionOutputs)
            {
                Name(output, path + ".execution.projectionOutputs");
                Require(outputs.Add(output), path + ".execution.projectionOutputs", "duplicate-projection-output");
            }
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
        for (int index = 0; index < descriptor.Fields.Count; index++)
        {
            IdempotencyDescriptorField field = descriptor.Fields[index];
            string fieldPath = $"{path}.fields[{index}]";
            Name(field.Name, fieldPath + ".name");
            Require(names.Add(field.Name), fieldPath + ".name", "duplicate-descriptor-field");
            Require(field.Type is "string" or "int64" or "boolean" or "string-list", fieldPath + ".type", "unsupported-descriptor-type");
        }
    }

    private static bool IsDiscriminator(string? value) => value is { Length: > 0 and <= 512 }
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' or '+');

    private static void Discriminator(string? value, string path) => Require(IsDiscriminator(value), path, "missing-or-invalid-discriminator");

    private static void Name(string? value, string path) => Require(IsMatch(value, NamePattern()), path, "missing-or-invalid-name");

    private static void AppId(string? value, string path) => Require(IsMatch(value, DomainPattern()), path, "missing-or-invalid-name");

    private static void Domain(string? value, string path) => Require(value is { Length: > 0 and <= 64 } && DomainPattern().IsMatch(value), path, "missing-or-invalid-domain");

    private static void Token(string? value, string path) => Require(IsMatch(value, TokenPattern()), path, "missing-or-invalid-token");

    private static bool IsMatch(string? value, Regex pattern) => value is { Length: > 0 and <= 128 } && pattern.IsMatch(value);

    private static void List<T>(IReadOnlyList<T>? list, string path, bool allowEmpty = true)
    {
        Require(list is not null && list.Count <= 4096 && (allowEmpty || list.Count > 0), path, "missing-or-invalid-list");
        for (int index = 0; index < list.Count; index++)
        {
            Require(list[index] is not null, $"{path}[{index}]", "null-entry");
        }
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

    [GeneratedRegex("\\A[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\\z", RegexOptions.CultureInvariant)]
    private static partial Regex DomainPattern();

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
