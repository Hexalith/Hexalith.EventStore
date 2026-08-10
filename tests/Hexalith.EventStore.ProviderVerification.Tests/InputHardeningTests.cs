using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Shouldly;

namespace Hexalith.EventStore.ProviderVerification.Tests;

public sealed class InputHardeningTests
{
    [Fact]
    public void SafePath_TraversalInput_IsRejected()
    {
        SafePath.TryResolveExistingFile("../hostile.json", 1024, out _, out string code).ShouldBeFalse();

        code.ShouldBe("input.path.invalid");
    }

    [Fact]
    public void SafePath_SymlinkInput_IsRejected()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string target = Path.Combine(directory, "target.json");
            string link = Path.Combine(directory, "link.json");
            File.WriteAllText(target, "{}");
            _ = File.CreateSymbolicLink(link, target);

            SafePath.TryResolveExistingFile(link, 1024, out _, out string code).ShouldBeFalse();

            code.ShouldBe("input.path.invalid");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void JsonInput_MalformedJson_ReturnsStableCode()
    {
        string path = Path.Combine(CreateTemporaryDirectory(), "malformed.json");
        try
        {
            File.WriteAllText(path, "{");

            ProviderVerificationInputException exception = Should.Throw<ProviderVerificationInputException>(
                () => JsonInput.Read(path, 1024));

            exception.Code.ShouldBe("input.json.malformed");
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void JsonInput_OversizedJson_ReturnsStableCode()
    {
        string path = Path.Combine(CreateTemporaryDirectory(), "oversized.json");
        try
        {
            File.WriteAllText(path, new string('x', 1025));

            ProviderVerificationInputException exception = Should.Throw<ProviderVerificationInputException>(
                () => JsonInput.Read(path, 1024));

            exception.Code.ShouldBe("input.file.size-invalid");
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void RequireExactProperties_DuplicateOrExtraFields_FailsClosed()
    {
        using System.Text.Json.JsonDocument duplicate = System.Text.Json.JsonDocument.Parse("{\"value\":1,\"value\":2}");
        using System.Text.Json.JsonDocument extra = System.Text.Json.JsonDocument.Parse("{\"value\":1,\"extra\":2}");

        Should.Throw<ProviderVerificationInputException>(
            () => JsonInput.RequireExactProperties(duplicate.RootElement, "value"))
            .Code.ShouldBe("input.json.duplicate-field");
        Should.Throw<ProviderVerificationInputException>(
            () => JsonInput.RequireExactProperties(extra.RootElement, "value"))
            .Code.ShouldBe("input.json.extra-or-missing-field");
    }

    [Fact]
    public void LoadPact_UnexpectedConsumer_IsRejected()
    {
        byte[] bytes = Encoding.UTF8.GetBytes(PactJson("Other.Consumer", "\"method\":\"GET\",\"path\":\"/api/v1/query\""));

        ProviderVerificationInputException exception = Should.Throw<ProviderVerificationInputException>(
            () => VerificationInputLoader.LoadPact(bytes, "pact.json", new string('a', 64)));

        exception.Code.ShouldBe("input.pact.identity-invalid");
    }

    [Fact]
    public void LoadPact_DuplicateRequestField_IsRejectedWithStableCode()
    {
        byte[] bytes = Encoding.UTF8.GetBytes(PactJson(
            "Hexalith.FrontComposer.Shell",
            "\"method\":\"GET\",\"method\":\"POST\",\"path\":\"/api/v1/query\""));

        ProviderVerificationInputException exception = Should.Throw<ProviderVerificationInputException>(
            () => VerificationInputLoader.LoadPact(bytes, "pact.json", new string('a', 64)));

        exception.Code.ShouldBe("input.json.duplicate-field");
    }

    [Fact]
    public void LoadPact_WrongInteractionsKind_IsRejectedWithStableCode()
    {
        byte[] bytes = """
            {
              "consumer":{"name":"Hexalith.FrontComposer.Shell"},
              "provider":{"name":"Hexalith.EventStore"},
              "interactions":{},
              "metadata":{}
            }
            """u8.ToArray();

        ProviderVerificationInputException exception = Should.Throw<ProviderVerificationInputException>(
            () => VerificationInputLoader.LoadPact(bytes, "pact.json", new string('a', 64)));

        exception.Code.ShouldBe("input.json.value-invalid");
    }

    [Fact]
    public void CreateNormalizedPact_SourceChangesAfterLoad_IsRejectedBeforeNormalization()
    {
        string directory = CreateTemporaryDirectory();
        string pactPath = Path.Combine(directory, "pact.json");
        byte[] original = "{\"interactions\":[]}"u8.ToArray();
        File.WriteAllBytes(pactPath, original);
        var interaction = new InteractionDefinition(
            "description",
            "command-accepted",
            "POST",
            "/api/v1/commands",
            "pact.json",
            VerificationInputLoader.ComputeSha256(original));
        try
        {
            File.WriteAllText(pactPath, "{\"interactions\":[{}]}");

            ProviderVerificationInputException exception = Should.Throw<ProviderVerificationInputException>(
                () => PactInteractionVerifier.CreateNormalizedPact(interaction, directory));

            exception.Code.ShouldBe("input.pact.hash-changed");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void CreateNormalizedPact_RemovesOnlyPerInteractionMetadata()
    {
        string directory = CreateTemporaryDirectory();
        string pactPath = Path.Combine(directory, "pact.json");
        byte[] original = """
            {
              "interactions":[{
                "description":"known",
                "metadata":{"nonContract":"remove"},
                "request":{"body":{"metadata":{"contract":"retain"}}},
                "response":{"status":200}
              }],
              "metadata":{"pactSpecification":{"version":"4.0"}}
            }
            """u8.ToArray();
        File.WriteAllBytes(pactPath, original);
        var interaction = new InteractionDefinition(
            "description",
            "command-accepted",
            "POST",
            "/api/v1/commands",
            "pact.json",
            VerificationInputLoader.ComputeSha256(original));
        string? normalizedPath = null;
        try
        {
            normalizedPath = PactInteractionVerifier.CreateNormalizedPact(interaction, directory);
            JsonNode root = JsonNode.Parse(File.ReadAllBytes(normalizedPath))!;

            root["metadata"].ShouldNotBeNull();
            root["interactions"]![0]!["metadata"].ShouldBeNull();
            root["interactions"]![0]!["request"]!["body"]!["metadata"].ShouldNotBeNull();
        }
        finally
        {
            if (normalizedPath is not null)
            {
                File.Delete(normalizedPath);
            }

            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void LoadStateCatalog_UnsupportedStateAndChangedSeam_AreRejected()
    {
        string source = Path.Combine(
            FindRepositoryRoot(),
            "..",
            "..",
            "tests",
            "Hexalith.FrontComposer.Shell.Tests",
            "Pact",
            "provider-state-catalog.json");
        string directory = CreateTemporaryDirectory();
        try
        {
            JsonNode unsupported = JsonNode.Parse(File.ReadAllText(source))!;
            unsupported["states"]![0]!["name"] = "unsupported-state";
            string unsupportedPath = Path.Combine(directory, "unsupported.json");
            File.WriteAllText(unsupportedPath, unsupported.ToJsonString());
            Should.Throw<ProviderVerificationInputException>(
                () => VerificationInputLoader.LoadStateCatalog(unsupportedPath))
                .Code.ShouldBe("input.state.unsupported-catalog");

            JsonNode changedSeam = JsonNode.Parse(File.ReadAllText(source))!;
            changedSeam["states"]![0]!["testOnlySeam"] = "unapproved seam";
            string changedSeamPath = Path.Combine(directory, "changed-seam.json");
            File.WriteAllText(changedSeamPath, changedSeam.ToJsonString());
            Should.Throw<ProviderVerificationInputException>(
                () => VerificationInputLoader.LoadStateCatalog(changedSeamPath))
                .Code.ShouldBe("input.state.catalog-metadata-invalid");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string PactJson(string consumer, string requestProperties)
        => $$"""
            {
              "consumer":{"name":"{{consumer}}"},
              "provider":{"name":"Hexalith.EventStore"},
              "interactions":[{
                "type":"Synchronous/HTTP",
                "description":"known",
                "providerStates":[{"name":"command-accepted"}],
                "request":{ {{requestProperties}} },
                "response":{},
                "metadata":{}
              }],
              "metadata":{}
            }
            """;

    private static string FindRepositoryRoot()
    {
        foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            DirectoryInfo? directory = new(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Hexalith.EventStore.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("EventStore repository root was not found.");
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"eventstore-provider-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
