using System.Text.Json;
using System.Text.RegularExpressions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Checks generated credentials and inert controls at their original format boundaries.</summary>
public sealed partial class SecretsProtectionTests
{
    /// <summary>Sequence members retain their credential owner and original scalar line.</summary>
    [Fact]
    public void CredentialFormats_YamlSequencesInspectEveryScalar()
    {
        const string path = "config/values.yaml";
        string name = "pass" + "words";
        string source = name + ":\n  - ${RUNTIME_VALUE}\n  - " + RandomSecret() + "\n  - '[redacted]'\n  - " + RandomSecret()
            + "\nother:\n  - public-metadata\n";
        FindViolations(path, source).ShouldBe([path + ":3", path + ":5"]);
        FindViolations(path, name + ":\n  - ${RUNTIME_VALUE}\n  - '[redacted]'\nother: public\n").ShouldBeEmpty();
        FindViolations(path, name + ":\n- ${RUNTIME_VALUE}\n- " + RandomSecret() + "\nother:\n- public-metadata\n")
            .ShouldBe([path + ":3"]);
        FindViolations(path, "\"" + name + "\":\n- " + RandomSecret() + "\nother:\n- public-metadata\n")
            .ShouldBe([path + ":2"]);
        string references = "imagePull" + "Secrets:\n  - name: registry-credentials\n";
        FindViolations(path, references).ShouldBeEmpty();
        FindViolations(path, references + name + ":\n  - pass" + "word: " + RandomSecret() + "\n")
            .ShouldBe([path + ":4"]);
    }

    /// <summary>Plain and block YAML scalars keep all text beyond runtime placeholders.</summary>
    [Fact]
    public void CredentialFormats_YamlScalarsInspectCompleteValues()
    {
        const string path = "config/values.yml";
        string name = "client" + "Secret";
        FindViolations("docs/config.md", "```yaml\n" + name + ": ${{ secrets.RUNTIME_VALUE }} " + RandomSecret() + "\n```")
            .ShouldBe(["docs/config.md:2"]);
        foreach (string placeholder in new[] { "${RUNTIME_VALUE}", "${{ secrets.RUNTIME_VALUE }}", "{env:RUNTIME_VALUE}" })
        {
            FindViolations(path, name + ": " + placeholder + " # a comment\nother: public\n").ShouldBeEmpty();
            FindViolations(path, name + ": " + placeholder + " " + RandomSecret() + "\n").ShouldBe([path + ":1"]);
            FindViolations(path, name + ": " + placeholder + "\n  " + RandomSecret() + "\nother: public\n")
                .ShouldBe([path + ":1"]);
            FindViolations(path, name + ": >-\n  " + placeholder + "\nother: public\n").ShouldBeEmpty();
            FindViolations(path, name + ": |\n  " + placeholder + "\n  " + RandomSecret() + "\nother: public\n")
                .ShouldBe([path + ":2"]);
            FindViolations(path, name + ": |\n  " + placeholder + " # " + RandomSecret() + "\nother: public\n")
                .ShouldBe([path + ":2"]);
            FindViolations(path, name + ": |\n  - " + RandomSecret() + "\nother: public\n")
                .ShouldBe([path + ":2"]);
        }
    }

    /// <summary>Qualified constructors inspect positional or reordered named secret arguments.</summary>
    [Fact]
    public void CredentialFormats_ConstructorsUseSecretArgumentsAndOriginalLines()
    {
        const string path = "src/credentials.cs";
        foreach ((string type, string prefix, string argument) in new[]
        {
            ("System.Net.Network" + "Credential", "\"public-user\", ", "password"),
            ("global::System.Net.Network" + "Credential", "\"public-user\", ", "password"),
            ("Azure.Identity.ClientSecret" + "Credential", "\"public-tenant\", \"public-client\", ", "clientSecret"),
            ("global::Azure.Identity.ClientSecret" + "Credential", "\"public-tenant\", \"public-client\", ", "clientSecret"),
            ("Microsoft.IdentityModel.Tokens.SymmetricSecurity" + "Key", "", "key"),
        })
        {
            string literal = JsonSerializer.Serialize(RandomSecret());
            string runtime = "Environment.GetEnvironmentVariable(\"RUNTIME_VALUE\")";
            string open = "var x = new " + type + "(\n";
            FindViolations(path, open + prefix + literal + ");\n").ShouldBe([path + ":2"]);
            FindViolations(path, open + prefix + runtime + ");\n").ShouldBeEmpty();
            FindViolations(path, open + prefix + "global::System." + runtime + ");\n").ShouldBeEmpty();
            FindViolations(path, open + prefix + "OtherEnvironment.GetEnvironmentVariable(" + literal + "));")
                .ShouldBe([path + ":2"]);
            FindViolations(path, open + argument + ": " + literal + ");\n").ShouldBe([path + ":2"]);
            FindViolations(path, open + argument + ": " + runtime + ");\n").ShouldBeEmpty();
            FindViolations(path, open + prefix + "\"${RUNTIME_VALUE}\");\n").ShouldBeEmpty();
            FindViolations(path, open + prefix + runtime + " + " + literal + ");\n").ShouldBe([path + ":2"]);
            FindViolations(path, open + prefix + runtime + ");\n" + Assignment("pass" + "word", literal))
                .ShouldBe([path + ":3"]);
        }
        string named = "new Azure.Identity.ClientSecret" + "Credential(clientId: \"public\",\nclientSecret: "
            + JsonSerializer.Serialize(RandomSecret()) + ", tenantId: \"public\");";
        FindViolations(path, named).ShouldBe([path + ":2"]);
        string generated = "string example = \"new System.Net.Network" + "Credential(\" + quote + user + quote + \", \" + quote + supplied + quote + \");\";";
        FindViolations(path, generated).ShouldBeEmpty();
        string embedded = "string example = " + JsonSerializer.Serialize("new System.Net.Network" + "Credential(\"public-user\", "
            + JsonSerializer.Serialize(RandomSecret()) + ");") + ";";
        FindViolations(path, embedded).ShouldBe([path + ":1"]);
        string raw = "string example = \"\"\"new System.Net.Network" + "Credential(\"public-user\", \"+" + RandomSecret() + "\")\"\"\";";
        FindViolations(path, raw).ShouldBe([path + ":1"]);
    }

    /// <summary>CDATA reports its payload line and does not exempt adjacent XML secrets.</summary>
    [Fact]
    public void CredentialFormats_XmlCdataPreservesLiteralAndPlaceholderBoundaries()
    {
        const string path = "config/credentials.xml";
        string open = "<pass" + "word><![CDATA[\n";
        string close = "\n]]></pass" + "word>";
        FindViolations(path, open + RandomSecret() + close).ShouldBe([path + ":2"]);
        FindViolations(path, open + "${RUNTIME_VALUE}" + close).ShouldBeEmpty();
        FindViolations(path, open + "${RUNTIME_VALUE} " + RandomSecret() + close).ShouldBe([path + ":2"]);
        FindViolations(path, "<pass" + "word>\n<![CDATA[" + RandomSecret() + "]]>\n</pass" + "word>")
            .ShouldBe([path + ":2"]);
        string mixed = "<pass" + "word>\n<![CDATA[${RUNTIME_VALUE}]]>\n";
        FindViolations(path, mixed + "\n</pass" + "word>").ShouldBeEmpty();
        FindViolations(path, mixed + RandomSecret() + "\n</pass" + "word>").ShouldBe([path + ":2"]);
        FindViolations(path, "<pass" + "word>\n" + RandomSecret() + "<![CDATA[${RUNTIME_VALUE}]]></pass" + "word>")
            .ShouldBe([path + ":2"]);
        FindViolations(path, open + "[redacted]" + close + "\n<api" + "Key>" + RandomSecret() + "</api" + "Key>")
            .ShouldBe([path + ":4"]);
    }

    /// <summary>CLI argument boundaries accept runtime sources and retain adjacent command checks.</summary>
    [Fact]
    public void CredentialFormats_ShellOptionsStopAtArgumentAndCommandBoundaries()
    {
        const string path = "scripts/credentials.sh";
        string option = "tool --client-" + "secret ";
        foreach (string quote in new[] { "", "\"", "'" })
        {
            FindViolations(path, "echo public\n" + option + quote + RandomSecret() + quote + "; echo public")
                .ShouldBe([path + ":2"]);
            FindViolations(path, option + quote + "${RUNTIME_VALUE}" + quote + "; echo public").ShouldBeEmpty();
            FindViolations(path, option + quote + "${RUNTIME_VALUE}" + quote + "\n" + option + quote + RandomSecret() + quote)
                .ShouldBe([path + ":2"]);
        }
        FindViolations(path, option + "$(openssl rand -base64 32); echo public").ShouldBeEmpty();
        string continued = option + "\\\n  ";
        FindViolations(path, continued + JsonSerializer.Serialize(RandomSecret()) + "; echo public").ShouldBe([path + ":2"]);
        FindViolations(path, continued + "\"${RUNTIME_VALUE}\"; echo public").ShouldBeEmpty();
        FindViolations(path, option + "\"$RUNTIME_VALUE\"" + RandomSecret()).ShouldBe([path + ":1"]);
        FindViolations(path, option + "$RUNTIME_VALUE && " + option + RandomSecret()).ShouldBe([path + ":1"]);
        FindViolations(path, option + "\\\"$RUNTIME_VALUE\\\"").ShouldBeEmpty();
        FindViolations(path, option + "\\\"" + RandomSecret() + "\\\"").ShouldBe([path + ":1"]);
        string mentions = "Use --to" + "ken to provide a JWT token.\n--url --to" + "ken --format --output\n";
        FindViolations(path, mentions).ShouldBeEmpty();
        FindViolations(path, mentions + option + RandomSecret()).ShouldBe([path + ":3"]);
    }

    /// <summary>Decoded nested octet JWKs report key material while public metadata remains inert.</summary>
    [Fact]
    public void CredentialFormats_PrivateOctetJwkUsesDecodedObjectContext()
    {
        const string path = "config/keys.json";
        string credential = RandomSecret();
        foreach (string keyType in new[] { "\"kty\": \"oct\"", "\"kt\\u0079\": \"o\\u0063t\"" })
        {
            string source = "{\"keys\":[{\n  \"\\u006b\": " + JsonSerializer.Serialize(credential) + ",\n  " + keyType + "\n}]}";
            FindViolations(path, source).ShouldBe([path + ":2"]);
            FindViolations(path, source.Replace(JsonSerializer.Serialize(credential), "\"${RUNTIME_VALUE}\"", StringComparison.Ordinal))
                .ShouldBeEmpty();
            FindViolations(path, source.Replace(JsonSerializer.Serialize(credential), JsonSerializer.Serialize("${RUNTIME_VALUE}" + credential), StringComparison.Ordinal))
                .ShouldBe([path + ":2"]);
            FindViolations(path, JsonSerializer.Serialize(new { capture = source })).ShouldBe([path + ":1"]);
        }
        FindViolations(path, "{\"kty\":\"oct\",\"kid\":\"public-key-id\",\"alg\":\"HS256\"}").ShouldBeEmpty();
        FindViolations(path, "{\"kty\":\"RSA\",\"n\":" + JsonSerializer.Serialize(credential) + ",\"e\":\"AQAB\"}").ShouldBeEmpty();
        string reordered = "{\"wrapper\":{\"kty\":\"oct\",\n\"k\":" + JsonSerializer.Serialize(credential) + "}}";
        FindViolations(path, reordered).ShouldBe([path + ":2"]);
    }

    /// <summary>Comment punctuation and generic commas do not change secret argument ordinals.</summary>
    [Fact]
    public void CredentialFormats_ConstructorTriviaAndGenericsRetainSecrets()
    {
        const string path = "src/trivia.cs";
        string runtime = "Environment.GetEnvironmentVariable(/* , ) */ \"RUNTIME_VALUE\")";
        foreach (string type in new[] { "Network" + "Credential", "System.Net.Network" + "Credential", "global::System.Net.Network" + "Credential" })
        {
            foreach (string user in new[] { "ResolveUser<Left, Right>()", "user /* , ) \"comment\" */", "user // , )\n" })
            {
                string prefix = "new " + type + "(" + user + ",\n";
                FindViolations(path, prefix + JsonSerializer.Serialize(RandomSecret()) + ");").ShouldBe([path + (user.Contains('\n') ? ":3" : ":2")]);
                FindViolations(path, prefix + runtime + ");").ShouldBeEmpty();
            }
        }
        string client = "new Azure.Identity.ClientSecret" + "Credential(Resolve<Left, Right>(), client /* , ) */,\n";
        FindViolations(path, client + JsonSerializer.Serialize(RandomSecret()) + ");").ShouldBe([path + ":2"]);
        FindViolations(path, client + runtime + ");").ShouldBeEmpty();
        string symmetric = "new SymmetricSecurity" + "Key(/* ) , */ Encoding.UTF8.GetBytes(\n";
        FindViolations(path, symmetric + JsonSerializer.Serialize(RandomSecret()) + "));").ShouldBe([path + ":2"]);
        FindViolations(path, symmetric + runtime + "));").ShouldBeEmpty();
    }

    /// <summary>Verbatim source delimiters decode with their original physical locations.</summary>
    [Fact]
    public void CredentialFormats_VerbatimEmbeddedConstructorRetainsOffsets()
    {
        const string path = "src/verbatim.cs";
        string prefix = "string text = @\"new System.Net.Network" + "Credential(\"\"public)user\"\",\n";
        string close = ");\";";
        FindViolations(path, prefix + "\"\"" + RandomSecret() + "\"\"" + close).ShouldBe([path + ":2"]);
        FindViolations(path, prefix + "\"\"${RUNTIME_VALUE}\"\"" + close).ShouldBeEmpty();
        FindViolations(path, prefix + "Environment.GetEnvironmentVariable(\"\"RUNTIME_VALUE\"\")" + close).ShouldBeEmpty();
    }

    /// <summary>YAML owner comments, node properties and flow collections preserve scalar semantics.</summary>
    [Fact]
    public void CredentialFormats_YamlNodePropertiesAndCollectionsKeepLiteralChecks()
    {
        const string path = "config/nodes.yaml";
        string name = "pass" + "words";
        string owner = name + ": # owner note\n  # another note\n";
        FindViolations(path, owner + "  - ${RUNTIME_VALUE}\n").ShouldBeEmpty();
        FindViolations(path, owner + "  - " + RandomSecret() + "\n").ShouldBe([path + ":3"]);
        foreach (string properties in new[] { "", "&runtime ", "!!str ", "!<tag:yaml.org,2002:str> " })
        {
            FindViolations(path, "pass" + "word: " + properties + "${RUNTIME_VALUE}\n").ShouldBeEmpty();
            FindViolations(path, "pass" + "word: " + properties + RandomSecret() + "\n").ShouldBe([path + ":1"]);
        }
        FindViolations(path, name + ": [${RUNTIME_VALUE}, '[redacted]', \"${OTHER_VALUE}\"]\n").ShouldBeEmpty();
        FindViolations(path, name + ": [${RUNTIME_VALUE}, \"" + RandomSecret() + "\"]\n").ShouldBe([path + ":1"]);
        foreach (string quote in new[] { "\"", "'" })
        {
            string prefix = name + ":\n  - " + quote + "\n    ";
            FindViolations(path, prefix + "${RUNTIME_VALUE}\n    " + quote + "\n").ShouldBeEmpty();
            FindViolations(path, prefix + RandomSecret() + "\n    " + quote + "\n").ShouldBe([path + ":2"]);
        }
    }

    /// <summary>Only actual bare YAML boolean/null nodes inherit non-string exemptions.</summary>
    [Fact]
    public void CredentialFormats_YamlStringTypesRejectBooleanLookingCredentials()
    {
        const string path = "config/types.yml";
        string name = "pass" + "word";
        foreach (string word in new[] { new string(['t', 'r', 'u', 'e']), new string(['f', 'a', 'l', 's', 'e']), new string(['n', 'u', 'l', 'l']) })
        {
            FindViolations(path, name + ": " + word + "\n").ShouldBeEmpty();
            FindViolations(path, name + ": \"" + word + "\"\n").ShouldBe([path + ":1"]);
            FindViolations(path, name + ": !!str " + word + "\n").ShouldBe([path + ":1"]);
            FindViolations(path, name + ": |\n  " + word + "\n").ShouldBe([path + ":2"]);
            FindViolations(path, name + "s:\n  - \"" + word + "\"\n").ShouldBe([path + ":2"]);
            FindViolations(path, name + "s: [\"" + word + "\"]\n").ShouldBe([path + ":1"]);
        }
    }

    /// <summary>Comments and processing instructions do not split XML credential text.</summary>
    [Fact]
    public void CredentialFormats_XmlNonValueNodesDoNotHideText()
    {
        const string path = "config/nodes.xml";
        foreach (string node in new[] { "<!-- note -->", "<?source runtime?>" })
        {
            string prefix = "<pass" + "word>\n${RUNTIME_VALUE}" + node;
            string close = "</pass" + "word>";
            FindViolations(path, prefix + close).ShouldBeEmpty();
            FindViolations(path, prefix + RandomSecret() + close).ShouldBe([path + ":2"]);
            FindViolations(path, "<pass" + "word>" + node + "\n" + RandomSecret() + close).ShouldBe([path + ":2"]);
        }
    }

    /// <summary>Runtime command recognition covers the complete substitution, not its prefix.</summary>
    [Fact]
    public void CredentialFormats_ShellRuntimeCommandsRejectLiteralOutputTails()
    {
        const string path = "scripts/runtime.sh";
        string option = "tool --client-" + "secret ";
        foreach (string command in new[] { "openssl rand -hex 32", "head -c 32 /dev/urandom", "dd if=/dev/urandom bs=32 count=1", "uuidgen" })
        {
            FindViolations(path, option + "$(" + command + ")").ShouldBeEmpty();
            foreach (string separator in new[] { "; ", " && ", "\n" })
            {
                string value = "$(" + command + separator + "printf '%s' '" + RandomSecret() + "')";
                FindViolations(path, option + value).ShouldBe([path + ":1"]);
                FindViolations(path, Assignment("pass" + "word", value)).ShouldBe([path + ":1"]);
            }
        }
        FindViolations(path, option + "${RUNTIME_VALUE}").ShouldBeEmpty();
    }

    /// <summary>The deployment examples remain runtime sources without admitting literal-producing variants.</summary>
    [Fact]
    public void CredentialFormats_DocumentedTokenAcquisitionsRetainRuntimeBoundaries()
    {
        int examples = 0;
        foreach (string document in new[] { "deployment-azure-container-apps.md", "deployment-docker-compose.md", "deployment-kubernetes.md" })
        {
            string content = File.ReadAllText(Path.Combine(RepoRoot, "docs/guides", document));
            foreach (Match example in Regex.Matches(content, @"\bTOKEN=(?<value>\$\(curl[\s\S]*?\)\r?$)", RegexOptions.Multiline | RegexOptions.CultureInvariant))
            {
                examples++;
                const string path = "examples/runtime.sh";
                string expression = example.Groups["value"].Value;
                FindViolations(path, "TOKEN=" + expression).ShouldBeEmpty();
                string commandTail = expression[..^1] + "; printf '%s' '" + RandomSecret() + "')";
                FindViolations(path, "TOKEN=" + commandTail).ShouldBe([path + ":1"]);
                foreach (string filter in new[] { JsonSerializer.Serialize(RandomSecret()), ".access_token // " + JsonSerializer.Serialize(RandomSecret()), ".access_token + " + JsonSerializer.Serialize(RandomSecret()) })
                {
                    string changed = Regex.Replace(expression, @"(?<prefix>\|\s*jq\s+-(?:r|er)\s+)'[^']*'(?=\))",
                        match => match.Groups["prefix"].Value + "'" + filter + "'", RegexOptions.CultureInvariant);
                    changed.ShouldNotBe(expression);
                    FindViolations(path, "TOKEN=" + changed).ShouldBe([path + ":1"]);
                }
            }
            foreach (Match example in Regex.Matches(content, @"(?im)^>\s*\$token\s*=\s*(?<value>\(Invoke-RestMethod[^\r\n]+)", RegexOptions.CultureInvariant))
            {
                examples++;
                const string path = "examples/runtime.ps1";
                string expression = example.Groups["value"].Value;
                FindViolations(path, "$token = " + expression).ShouldBeEmpty();
                int close = expression.LastIndexOf(')');
                string commandTail = expression[..close] + "; Write-Output '" + RandomSecret() + "'" + expression[close..];
                FindViolations(path, "$token = " + commandTail).ShouldBe([path + ":1"]);
                string literalTail = expression + " + '" + RandomSecret() + "'";
                FindViolations(path, "$token = " + literalTail).ShouldBe([path + ":1"]);
                IsShellRuntimeExpression(expression + "; Write-Output '" + RandomSecret() + "'").ShouldBeFalse();
            }
        }
        examples.ShouldBe(7);
    }
}
