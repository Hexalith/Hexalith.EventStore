using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Preserves the complete assignment grammar while bounding repeated journal work.</summary>
public sealed partial class SecretsProtectionTests
{
    /// <summary>Every supplied path remains scanned, ordered, and sensitive to changed bytes.</summary>
    [Fact]
    public void DynamicallyPartitionedScan_PreservesCompleteCoverageAndChangedCredentials()
        => WithEvidenceRepository(root =>
        {
            string[] paths = Enumerable.Range(0, 129).Select(index => $"evidence/{index:D3}.txt").ToArray();
            foreach (string path in paths)
            {
                WriteEvidenceFile(root, path, Encoding.UTF8.GetBytes(Assignment("pass" + "word", RandomSecret())));
            }
            ScanTrackedFiles(paths.Reverse().ToArray(), root).ShouldBe(paths.Select(path => path + ":1"));
            WriteEvidenceFile(root, paths[64], Encoding.UTF8.GetBytes("ordinary text"));
            ScanTrackedFiles(paths, root).ShouldBe(paths.Where((_, index) => index != 64).Select(path => path + ":1"));
        });

    private static string[] ScanTrackedFiles(string[] paths, string? repositoryRoot = null)
        => Partitioner.Create(paths, loadBalance: true)
            .AsParallel()
            .WithDegreeOfParallelism(Math.Min(Environment.ProcessorCount, 4))
            .SelectMany(path => ScanTrackedFile(path, repositoryRoot))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();

    /// <summary>Compares the optimized discovery against the original overlapping matcher.</summary>
    [Fact]
    public void CredentialAssignmentDiscovery_PreservesOriginalMatches()
    {
        string credential = RandomSecret();
        string[] fragments =
        [
            "description=\"an ordinary value\"",
            Assignment("password", credential),
            "[\"password\"] = \"" + credential + "\"",
            "\"password\":\n\"" + credential + "\"",
            "outer=\"password=" + credential + "\"",
            "token='prefix' + runtimeToken",
            "token=`${runtimeToken}`",
            "password=\"unterminated",
            "password=",
            "password=>ignored",
            "password:-ignored",
            "some.password = '" + credential + "'",
            "\u00e9\npassword='" + credential + "'",
            "password:leaked=",
            "password:ordinaryField=",
            "plain:password=",
            "plain:password:ordinaryField=",
        ];
        foreach (string content in fragments.Concat(fragments.Select(fragment =>
            "prefix='unclosed\n" + fragment + "\n" + string.Join("\n", fragments))))
        {
            static string Identity(Match match) => string.Join("|",
                new[] { "name", "value", "bare", "double", "single", "template" }.Select(name =>
                    $"{name}:{match.Groups[name].Index}:{match.Groups[name].Length}:{match.Groups[name].Value}"));
            string[] original = SecretAssignmentPattern().Matches(content)
                .Where(match => IsCredentialName(match.Groups["name"].Value)).Select(Identity).ToArray();
            FindCredentialAssignments(content).Select(Identity).ShouldBe(original);
        }

        foreach (string prefix in new[] { "plain", "password", "apiToken", "account_key" })
        {
            foreach (string suffix in new[] { "plain", "password", "token", "key", "" })
            {
                foreach (string separator in new[] { ":", "=", ":=", ":plain:" })
                {
                    string content = prefix + separator + suffix + "=";
                    int[] original = SecretAssignmentPattern().Matches(content)
                        .Where(match => IsCredentialName(match.Groups["name"].Value)).Select(match => match.Index).ToArray();
                    FindCredentialAssignments(content).Select(match => match.Index).ShouldBe(original);
                }
            }
        }
        FindViolations("evidence/backtracking.txt", "password:leaked=").ShouldBe(["evidence/backtracking.txt:1"]);
        FindViolations("evidence/backtracking.txt", "plain:password=").ShouldBeEmpty();
    }

    /// <summary>Long quoted noncredential fields cannot prevent scanning credentials anywhere in the file.</summary>
    [Fact]
    public void LargeQuotedJournal_ScansEveryCredentialWithoutRepeatedValueParsing()
    {
        string assignment = Assignment("pass" + "word", RandomSecret());
        string field = "description=\"" + new string(' ', 512) + "\"\n";
        string padding = string.Concat(Enumerable.Repeat(field, 4096));
        string content = assignment + "\n" + padding + assignment + "\n" + padding + assignment;
        var elapsed = Stopwatch.StartNew();
        FindViolations("evidence/journal.txt", content)
            .ShouldBe(["evidence/journal.txt:1", "evidence/journal.txt:4098", "evidence/journal.txt:8195"]);
        elapsed.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(15),
            "A four MiB journal must not reparse unrelated quoted values for each field.");
    }

    private static void WithEvidenceRepository(Action<string> exercise)
    {
        string root = Directory.CreateTempSubdirectory("secrets-scan-").FullName;
        try
        {
            exercise(root);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void WriteEvidenceFile(string root, string path, byte[] bytes)
    {
        string fullPath = Path.Combine(root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, bytes);
    }

    private static byte[] EncodeEvidenceText(string content, string encoding)
    {
        Encoding codec = encoding switch
        {
            "utf8-bom" => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            "utf16-le" => new UnicodeEncoding(bigEndian: false, byteOrderMark: true),
            "utf16-be" => new UnicodeEncoding(bigEndian: true, byteOrderMark: true),
            "utf16-le-no-bom" => new UnicodeEncoding(bigEndian: false, byteOrderMark: false),
            "utf16-be-no-bom" => new UnicodeEncoding(bigEndian: true, byteOrderMark: false),
            _ => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        };
        byte[] bytes = [.. codec.GetPreamble(), .. codec.GetBytes(content)];
        return encoding switch
        {
            "malformed-utf8" => [0xff, .. bytes],
            "control-prefix" => [0x00, 0x01, 0x02, .. bytes],
            _ => bytes,
        };
    }

    private static IEnumerable<Match> FindCredentialAssignments(string content)
    {
        foreach (Match candidate in SecretAssignmentStartPattern().Matches(content))
        {
            string candidateName = candidate.Groups["name"].Value;
            // A colon can belong to the name OR be the separator. The full grammar may
            // backtrack to an earlier colon when a later equals sign has no usable value.
            if (!IsCredentialName(candidateName) && !candidateName.Contains(':'))
            {
                continue;
            }

            // Retain the original value grammar, anchored to this exact candidate.
            // In particular, failed candidates cannot skip ahead or hide overlapping assignments.
            Match match = AnchoredSecretAssignmentPattern().Match(content, candidate.Index);
            if (match.Success && IsCredentialName(match.Groups["name"].Value))
            {
                yield return match;
            }
        }
    }
}
