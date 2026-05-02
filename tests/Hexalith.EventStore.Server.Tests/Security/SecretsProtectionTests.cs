
using System.Text.RegularExpressions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;
/// <summary>
/// Story 5.4, Task 9: Secrets protection tests (AC #4).
/// Static analysis tests that verify no hardcoded secrets appear in configuration files or source code.
/// </summary>
public class SecretsProtectionTests {
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    // Patterns that indicate hardcoded secrets
    private static readonly Regex ConnectionStringWithPasswordPattern = new(
        @"(Password|Pwd)\s*=\s*[^;""'\s]{3,}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex JwtSigningKeyPattern = new(
        @"(SigningKey|SecretKey|JwtKey)\s*[""':=]\s*[A-Za-z0-9+/=]{16,}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ApiKeyPattern = new(
        @"(ApiKey|api_key|api-key)\s*[""':=]\s*[A-Za-z0-9]{16,}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // --- Task 9.2: No hardcoded secrets in config files ---

    [Fact]
    public void SourceCode_NoHardcodedSecrets_InConfigFiles() {
        string[] configFiles = Directory.GetFiles(RepoRoot, "appsettings*.json", SearchOption.AllDirectories);
        configFiles.ShouldNotBeEmpty("Should find at least one appsettings file");

        foreach (string configFile in configFiles) {
            string content = File.ReadAllText(configFile);
            string relativePath = Path.GetRelativePath(RepoRoot, configFile);

            ConnectionStringWithPasswordPattern.IsMatch(content).ShouldBeFalse(
                $"Config file '{relativePath}' contains a connection string with hardcoded password");

            // Allow the development signing key placeholder (test-only)
            string contentWithoutDevKey = Regex.Replace(content, @"""SigningKey"":\s*""[^""]*test[^""]*""", "", RegexOptions.IgnoreCase);
            if (!relativePath.Contains("Development", StringComparison.OrdinalIgnoreCase)) {
                JwtSigningKeyPattern.IsMatch(contentWithoutDevKey).ShouldBeFalse(
                    $"Config file '{relativePath}' contains a hardcoded JWT signing key");
            }
        }
    }

    // --- Task 9.3: No hardcoded secrets in DAPR YAML ---

    [Fact]
    public void SourceCode_NoHardcodedSecrets_InDaprYaml() {
        string[] yamlSearchPaths =
        [
            Path.Combine(RepoRoot, "src", "Hexalith.EventStore.AppHost", "DaprComponents"),
            Path.Combine(RepoRoot, "deploy", "dapr"),
        ];

        bool foundAnyYaml = false;

        foreach (string searchPath in yamlSearchPaths) {
            if (!Directory.Exists(searchPath)) {
                continue;
            }

            string[] yamlFiles = Directory.GetFiles(searchPath, "*.yaml", SearchOption.AllDirectories);
            foreach (string yamlFile in yamlFiles) {
                foundAnyYaml = true;
                string content = File.ReadAllText(yamlFile);
                string relativePath = Path.GetRelativePath(RepoRoot, yamlFile);

                // Strip comments before checking for hardcoded secrets (comments may contain format examples)
                string[] lines = content.Split('\n');
                string contentWithoutComments = string.Join('\n', lines.Select(line => {
                    int commentIndex = line.IndexOf('#');
                    return commentIndex >= 0 ? line[..commentIndex] : line;
                }));

                // DAPR YAML should use environment variable substitution, not hardcoded passwords
                ConnectionStringWithPasswordPattern.IsMatch(contentWithoutComments).ShouldBeFalse(
                    $"DAPR YAML '{relativePath}' contains a hardcoded password in connection string");

                // Check for common secret patterns (not in comments)
                foreach (string line in lines) {
                    string trimmed = line.TrimStart();
                    if (trimmed.StartsWith('#')) {
                        continue;
                    }

                    // Password values should reference environment variables or secret stores
                    if (Regex.IsMatch(trimmed, @"password\s*:", RegexOptions.IgnoreCase)) {
                        // Allow environment variable patterns like ${SECRET_NAME} or secretKeyRef
                        bool isEnvVar = trimmed.Contains("${", StringComparison.Ordinal) ||
                                        trimmed.Contains("secretKeyRef", StringComparison.OrdinalIgnoreCase) ||
                                        trimmed.Contains("\"\"", StringComparison.Ordinal) ||
                                        trimmed.TrimEnd().EndsWith("\"\"", StringComparison.Ordinal) ||
                                        trimmed.TrimEnd().EndsWith("''", StringComparison.Ordinal);

                        // If it has a non-empty value that's not an env var, flag it
                        Match valueMatch = Regex.Match(trimmed, @"password\s*:\s*(.+)", RegexOptions.IgnoreCase);
                        if (valueMatch.Success) {
                            string value = valueMatch.Groups[1].Value.Trim().Trim('"', '\'');
                            if (!string.IsNullOrEmpty(value) && !isEnvVar && value.Length > 3) {
                                true.ShouldBeFalse(
                                    $"DAPR YAML '{relativePath}' may contain a hardcoded password: {trimmed.Trim()}");
                            }
                        }
                    }
                }
            }
        }

        foundAnyYaml.ShouldBeTrue("Should find at least one DAPR YAML file");
    }

    // --- Task 9.4: No hardcoded secrets in C# files ---

    [Fact]
    public void SourceCode_NoHardcodedSecrets_InCSharpFiles() {
        string srcPath = Path.Combine(RepoRoot, "src");
        Directory.Exists(srcPath).ShouldBeTrue("src directory should exist");

        string[] csFiles = Directory.GetFiles(srcPath, "*.cs", SearchOption.AllDirectories);
        csFiles.ShouldNotBeEmpty("Should find C# source files");

        // Patterns for hardcoded secrets in C# string literals
        Regex hardcodedSecretPattern = new(
            @"""[^""\r\n]*(?:password|secret|connectionstring|apikey|signing.?key)\s*[=:]\s*[^""\r\n]{8,}""",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        foreach (string csFile in csFiles) {
            string content = File.ReadAllText(csFile);
            string relativePath = Path.GetRelativePath(RepoRoot, csFile);

            // Skip test files (they may have fake secrets for testing)
            if (relativePath.Contains("Tests", StringComparison.OrdinalIgnoreCase) ||
                relativePath.Contains("Testing", StringComparison.OrdinalIgnoreCase) ||
                relativePath.Contains("Fakes", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            // Skip configuration option classes (they define property names, not values)
            if (relativePath.Contains("Options.cs", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            // Skip Aspire host extensions that use AddParameter/AddRedis with parameter names only (no literal secrets)
            if (relativePath.EndsWith("HexalithEventStoreExtensions.cs", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            // Check for hardcoded secret patterns in string literals
            MatchCollection matches = hardcodedSecretPattern.Matches(content);
            foreach (Match match in matches) {
                // Allow common false positives: configuration key names, log messages, XML doc comments,
                // Aspire AddParameter (parameter name, not secret value), C# named arg secret: true, XML doc
                string matchValue = match.Value;
                if (matchValue.Contains("configuration", StringComparison.OrdinalIgnoreCase) ||
                    matchValue.Contains("placeholder", StringComparison.OrdinalIgnoreCase) ||
                    matchValue.Contains("must be", StringComparison.OrdinalIgnoreCase) ||
                    matchValue.Contains("cannot be", StringComparison.OrdinalIgnoreCase) ||
                    matchValue.Contains("required", StringComparison.OrdinalIgnoreCase) ||
                    matchValue.Contains("AddParameter", StringComparison.OrdinalIgnoreCase) ||
                    matchValue.Contains("secret: true", StringComparison.OrdinalIgnoreCase) ||
                    matchValue.Contains("AddRedis", StringComparison.OrdinalIgnoreCase) ||
                    matchValue.Contains("returns>", StringComparison.Ordinal)) {
                    continue;
                }

                true.ShouldBeFalse(
                    $"C# file '{relativePath}' may contain a hardcoded secret: {matchValue}");
            }
        }
    }
}
