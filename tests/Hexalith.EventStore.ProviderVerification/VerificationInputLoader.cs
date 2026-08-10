using System.Security.Cryptography;
using System.Text.Json;

namespace Hexalith.EventStore.ProviderVerification;

internal static class VerificationInputLoader
{
    private const string ExpectedConsumer = "Hexalith.FrontComposer.Shell";
    private const string ExpectedProvider = "Hexalith.EventStore";
    private const string ExpectedDefaultIsolation = "state reset per interaction; tenant/user/aggregate/cache data scoped by verification run id";
    private const string ExpectedTestOnlySeam = "Provider-state HTTP endpoint or fixture command in the EventStore submodule";
    private const long MaxCatalogBytes = 256 * 1024;
    private const long MaxManifestBytes = 256 * 1024;
    private const long MaxPactBytes = 2 * 1024 * 1024;

    public static VerificationInputs Load(ProviderVerificationOptions options, string repositoryRoot)
    {
        if (!SafePath.TryResolveExistingDirectory(options.PactDirectory, out string pactDirectory, out string code)
            || !SafePath.TryResolveExistingFile(options.ManifestPath, MaxManifestBytes, out string manifestPath, out code)
            || !SafePath.TryResolveExistingFile(options.StateCatalogPath, MaxCatalogBytes, out string catalogPath, out code)
            || !SafePath.TryResolveExistingFile(options.IdentityRecordPath, 128 * 1024, out string identityPath, out code)
            || !SafePath.TryResolveExistingDirectory(options.IdentityEvidenceDirectory, out string evidenceDirectory, out code))
        {
            throw new ProviderVerificationInputException(code);
        }

        IReadOnlySet<string> states = LoadStateCatalog(catalogPath);
        IReadOnlyList<ManifestInteraction> manifestInteractions = LoadManifest(manifestPath, out string[] pactFiles);
        if (!states.SetEquals(manifestInteractions.Select(interaction => interaction.State)))
        {
            throw new ProviderVerificationInputException("input.state.reconciliation-failed");
        }

        var hashes = new List<InputHash>
        {
            Hash("interaction-manifest", Path.GetFileName(manifestPath), manifestPath),
            Hash("provider-state-catalog", Path.GetFileName(catalogPath), catalogPath),
        };

        var pactInteractions = new Dictionary<(string Description, string State), PactInteraction>();
        foreach (string pactFile in pactFiles)
        {
            if (!IsSafePactFileName(pactFile))
            {
                throw new ProviderVerificationInputException("input.pact.filename-invalid");
            }

            string pactPath = Path.Combine(pactDirectory, pactFile);
            if (!SafePath.TryResolveExistingFile(pactPath, MaxPactBytes, out pactPath, out code)
                || !string.Equals(Path.GetDirectoryName(pactPath), pactDirectory, StringComparison.Ordinal))
            {
                throw new ProviderVerificationInputException(code);
            }

            byte[] pactSnapshot = JsonInput.ReadSnapshot(pactPath, MaxPactBytes);
            string pactHash = ComputeSha256(pactSnapshot);
            hashes.Add(new InputHash("pact", pactFile, pactHash));
            foreach (PactInteraction interaction in LoadPact(pactSnapshot, pactFile, pactHash))
            {
                if (!pactInteractions.TryAdd((interaction.Description, interaction.State), interaction))
                {
                    throw new ProviderVerificationInputException("input.interaction.duplicate");
                }
            }
        }

        if (manifestInteractions.Count != pactInteractions.Count)
        {
            throw new ProviderVerificationInputException("input.interaction.count-mismatch");
        }

        var interactions = new List<InteractionDefinition>(manifestInteractions.Count);
        foreach (ManifestInteraction manifest in manifestInteractions)
        {
            if (!states.Contains(manifest.State)
                || !pactInteractions.Remove((manifest.Description, manifest.State), out PactInteraction? pact)
                || !string.Equals(manifest.Method, pact.Method, StringComparison.Ordinal)
                || !string.Equals(manifest.Path, pact.Path, StringComparison.Ordinal))
            {
                throw new ProviderVerificationInputException("input.interaction.reconciliation-failed");
            }

            interactions.Add(new InteractionDefinition(
                manifest.Description,
                manifest.State,
                manifest.Method,
                manifest.Path,
                pact.PactFile,
                pact.PactSha256));
        }

        if (pactInteractions.Count != 0)
        {
            throw new ProviderVerificationInputException("input.interaction.reconciliation-failed");
        }

        IdentityEvidence identity = RuntimeIdentityValidator.Validate(
            identityPath,
            evidenceDirectory,
            repositoryRoot,
            hashes);
        hashes.Add(Hash("identity-decision", Path.GetFileName(identityPath), identityPath));
        return new VerificationInputs(pactDirectory, interactions, states, hashes, identity);
    }

    internal static string ComputeSha256(string path)
        => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    internal static string ComputeSha256(ReadOnlySpan<byte> bytes)
        => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static InputHash Hash(string kind, string name, string path)
        => new(kind, name, ComputeSha256(path));

    private static bool IsSafePactFileName(string value)
        => value.Length <= 128
            && value.EndsWith(".json", StringComparison.Ordinal)
            && string.Equals(Path.GetFileName(value), value, StringComparison.Ordinal)
            && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-');

    private static IReadOnlyList<ManifestInteraction> LoadManifest(string path, out string[] pactFiles)
    {
        using JsonDocument document = JsonInput.Read(path, MaxManifestBytes);
        JsonElement root = document.RootElement;
        JsonInput.RequireExactProperties(root, "story", "consumer", "provider", "pactFiles", "interactionCount", "interactions");
        _ = JsonInput.RequiredString(root, "story");
        if (JsonInput.RequiredString(root, "consumer") != ExpectedConsumer
            || JsonInput.RequiredString(root, "provider") != ExpectedProvider)
        {
            throw new ProviderVerificationInputException("input.manifest.identity-invalid");
        }

        JsonElement pactFileValues = JsonInput.RequiredArray(root, "pactFiles");
        pactFiles = pactFileValues.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : string.Empty)
            .ToArray();
        if (pactFiles.Length == 0
            || pactFiles.Length > 32
            || pactFiles.Any(string.IsNullOrWhiteSpace)
            || pactFiles.Distinct(StringComparer.Ordinal).Count() != pactFiles.Length)
        {
            throw new ProviderVerificationInputException("input.pact-list.invalid");
        }

        JsonElement interactions = JsonInput.RequiredArray(root, "interactions");
        if (JsonInput.RequiredInt32(root, "interactionCount") != interactions.GetArrayLength()
            || interactions.GetArrayLength() == 0
            || interactions.GetArrayLength() > 128)
        {
            throw new ProviderVerificationInputException("input.interaction.count-invalid");
        }

        var result = new List<ManifestInteraction>();
        var identities = new HashSet<(string Description, string State)>();
        foreach (JsonElement interaction in interactions.EnumerateArray())
        {
            JsonInput.RequireExactProperties(
                interaction,
                "description",
                "providerState",
                "method",
                "path",
                "generatedSource",
                "adapterPath",
                "owningAcceptanceCriteria",
                "classifierExpectation");
            var item = new ManifestInteraction(
                JsonInput.RequiredString(interaction, "description"),
                JsonInput.RequiredString(interaction, "providerState"),
                JsonInput.RequiredString(interaction, "method", 16),
                JsonInput.RequiredString(interaction, "path"));
            if (!identities.Add((item.Description, item.State)))
            {
                throw new ProviderVerificationInputException("input.interaction.duplicate");
            }

            result.Add(item);
        }

        return result;
    }

    internal static IReadOnlySet<string> LoadStateCatalog(string path)
    {
        using JsonDocument document = JsonInput.Read(path, MaxCatalogBytes);
        JsonElement root = document.RootElement;
        JsonInput.RequireExactProperties(root, "provider", "defaultIsolation", "forbiddenDependencies", "startupGuards", "states");
        if (JsonInput.RequiredString(root, "provider") != ExpectedProvider)
        {
            throw new ProviderVerificationInputException("input.provider.invalid");
        }

        if (JsonInput.RequiredString(root, "defaultIsolation", 512) != ExpectedDefaultIsolation)
        {
            throw new ProviderVerificationInputException("input.state.catalog-metadata-invalid");
        }

        ValidateExactStringArray(
            root,
            "forbiddenDependencies",
            ["DAPR", "Aspire", "Keycloak", "external network", "persisted shared state"]);
        ValidateExactStringArray(
            root,
            "startupGuards",
            ["unique loopback port", "health probe", "bounded startup timeout", "stale process detection", "process cleanup on failure"]);
        JsonElement states = JsonInput.RequiredArray(root, "states");
        if (states.GetArrayLength() == 0 || states.GetArrayLength() > 128)
        {
            throw new ProviderVerificationInputException("input.state.count-invalid");
        }

        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement state in states.EnumerateArray())
        {
            JsonInput.RequireExactProperties(
                state,
                "name",
                "setup",
                "teardown",
                "seededTenant",
                "seededUser",
                "seededAggregateId",
                "expectedResult",
                "isolatedPerInteraction",
                "owningRepository",
                "testOnlySeam");
            string name = JsonInput.RequiredString(state, "name");
            _ = JsonInput.RequiredString(state, "setup", 1024);
            _ = JsonInput.RequiredString(state, "teardown", 1024);
            _ = JsonInput.RequiredString(state, "seededTenant");
            _ = JsonInput.RequiredString(state, "seededUser");
            _ = JsonInput.RequiredString(state, "seededAggregateId");
            _ = JsonInput.RequiredString(state, "expectedResult", 1024);
            if (!result.Add(name))
            {
                throw new ProviderVerificationInputException("input.state.invalid");
            }

            if (!SupportedProviderStates.All.Contains(name))
            {
                throw new ProviderVerificationInputException("input.state.unsupported-catalog");
            }

            if (!state.TryGetProperty("isolatedPerInteraction", out JsonElement isolated)
                || isolated.ValueKind != JsonValueKind.True
                || JsonInput.RequiredString(state, "owningRepository") != "Hexalith.EventStore"
                || JsonInput.RequiredString(state, "testOnlySeam", 512) != ExpectedTestOnlySeam)
            {
                throw new ProviderVerificationInputException("input.state.catalog-metadata-invalid");
            }
        }

        return result;
    }

    internal static IReadOnlyList<PactInteraction> LoadPact(
        ReadOnlyMemory<byte> snapshot,
        string pactFile,
        string pactHash)
    {
        using JsonDocument document = JsonInput.Parse(snapshot);
        JsonElement root = document.RootElement;
        JsonInput.RequireExactProperties(root, "consumer", "provider", "interactions", "metadata");
        JsonInput.RequireExactProperties(root.GetProperty("consumer"), "name");
        JsonInput.RequireExactProperties(root.GetProperty("provider"), "name");
        if (JsonInput.RequiredString(root.GetProperty("consumer"), "name") != ExpectedConsumer
            || JsonInput.RequiredString(root.GetProperty("provider"), "name") != ExpectedProvider)
        {
            throw new ProviderVerificationInputException("input.pact.identity-invalid");
        }

        var result = new List<PactInteraction>();
        JsonElement pactInteractions = JsonInput.RequiredArray(root, "interactions");
        if (pactInteractions.GetArrayLength() == 0 || pactInteractions.GetArrayLength() > 128)
        {
            throw new ProviderVerificationInputException("input.interaction.count-invalid");
        }

        foreach (JsonElement interaction in pactInteractions.EnumerateArray())
        {
            JsonInput.RequireExactProperties(interaction, "type", "description", "providerStates", "request", "response", "metadata");
            if (JsonInput.RequiredString(interaction, "type", 32) != "Synchronous/HTTP")
            {
                throw new ProviderVerificationInputException("input.pact.type-invalid");
            }

            JsonElement providerStates = JsonInput.RequiredArray(interaction, "providerStates");
            if (providerStates.GetArrayLength() != 1)
            {
                throw new ProviderVerificationInputException("input.pact.state-invalid");
            }

            JsonElement state = providerStates[0];
            JsonInput.RequireExactProperties(state, "name");
            JsonElement request = JsonInput.RequiredObject(interaction, "request");
            JsonInput.RequireAllowedProperties(
                request,
                new HashSet<string>(["method", "path", "headers", "body", "matchingRules", "generators"], StringComparer.Ordinal),
                "method",
                "path");
            _ = JsonInput.RequiredObject(interaction, "response");
            _ = JsonInput.RequiredObject(interaction, "metadata");

            result.Add(new PactInteraction(
                JsonInput.RequiredString(interaction, "description"),
                JsonInput.RequiredString(state, "name"),
                JsonInput.RequiredString(request, "method", 16),
                JsonInput.RequiredString(request, "path"),
                pactFile,
                pactHash));
        }

        return result;
    }

    private static void ValidateExactStringArray(
        JsonElement root,
        string propertyName,
        IReadOnlyList<string> expected)
    {
        JsonElement values = JsonInput.RequiredArray(root, propertyName);
        string[] observed = values.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : string.Empty)
            .ToArray();
        if (!observed.SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw new ProviderVerificationInputException("input.state.catalog-metadata-invalid");
        }
    }
}
