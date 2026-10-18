using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;

public sealed partial class SecretsProtectionTests
{
    /// <summary>Existing approved fixtures, as escaped by the xUnit XML reporter.</summary>
    public static TheoryData<string> ExistingXunitProbeNames
    {
        get
        {
            var cases = new TheoryData<string>();
            var hashes = new HashSet<string>(StringComparer.Ordinal);
            foreach (string project in new[] { "contracts", "server" })
            {
                string path = "_bmad-output/implementation-artifacts/evidence/story-6-4/qualification-20261017-"
                    + project + "-debug-full.xml";
                foreach (XElement test in XDocument.Load(Path.Combine(RepoRoot, path)).Descendants("test"))
                {
                    string name = test.Attribute("name")!.Value;
                    string hash = XunitProbeNameHash(name);
                    if (IsKnownSyntheticTestNameHash(hash) && hashes.Add(hash))
                    {
                        cases.Add(name);
                    }
                }
            }

            hashes.Count.ShouldBe(9, "Only the existing nine approved fixture identities are used.");
            return cases;
        }
    }

    /// <summary>Report recognition preserves every other attribute, output, namespace and malformed byte.</summary>
    [Theory]
    [MemberData(nameof(ExistingXunitProbeNames))]
    public void XunitExistingProbeMetadata_RequiresExactIdentityAndStructure(string name)
        => WithEvidenceRepository(root =>
        {
            const string path = "evidence/renamed.xml";
            foreach (string mutation in new[] { "valid", "changed-name", "attribute", "element", "container",
                "namespace", "malformed", "adjacent", "output", "dtd" })
            {
                string value = mutation == "changed-name" ? name + RandomSecret() : name;
                var test = new XElement(mutation == "element" ? "message" : "test",
                    new XAttribute(mutation == "attribute" ? "description" : "name", value));
                var report = new XElement("assemblies", new XElement("assembly",
                    mutation == "container" ? test : new XElement("collection", test)));
                if (mutation == "namespace")
                {
                    foreach (XElement element in report.DescendantsAndSelf())
                    {
                        element.Name = XName.Get(element.Name.LocalName, "urn:other");
                    }
                }
                if (mutation == "adjacent")
                {
                    test.Add(new XAttribute("pass" + "word", RandomSecret()));
                }
                if (mutation == "output")
                {
                    test.Add(new XElement("output", Assignment("pass" + "word", RandomSecret())));
                }
                string text = report.ToString();
                if (mutation == "malformed") text += "<";
                if (mutation == "dtd") text = "<!DOCTYPE assemblies [<!ENTITY probe 'value'>]>" + text;
                foreach (string encoding in new[] { "utf8", "utf8-bom", "utf16-le", "utf16-be",
                    "utf16-le-no-bom", "utf16-be-no-bom" })
                {
                    foreach (string ending in new[] { "\n", "\r\n", "\r" })
                    {
                        WriteEvidenceFile(root, path, EncodeEvidenceText(text.Replace("\n", ending, StringComparison.Ordinal), encoding));
                        ScanTrackedFile(path, root).Any().ShouldBe(mutation != "valid");
                    }
                }
            }
        });

    /// <summary>Unapproved URI display names remain findings in the original immutable reports.</summary>
    [Theory]
    [InlineData("debug")]
    [InlineData("release")]
    public void XunitUnapprovedAuthorityDisplay_RemainsBlocking(string configuration)
    {
        string path = "_bmad-output/implementation-artifacts/evidence/story-6-4/qualification-20261017-server-"
            + configuration + "-full.xml";
        ScanTrackedFile(path).ShouldNotBeEmpty();
    }

    /// <summary>Historical literal probes have no new approval and must retain their existing findings.</summary>
    [Theory]
    [InlineData("pre-review-sources", 1)]
    [InlineData("reviewed-sources", 2)]
    [InlineData("reviewed-final-sources", 2)]
    public void HistoricalAuthorityLiteralProbes_RemainBlocking(string directory, int expected)
    {
        string path = "docs/evidence/story-3-6/20261017t144520z-production-authority-review/" + directory
            + "/tests/Hexalith.EventStore.Server.Tests/Authentication/WorkloadBindingAuthorityTests.cs";
        ScanTrackedFile(path).Count().ShouldBe(expected);
    }

    /// <summary>XML line coordinates and numeric entities must never redirect masking onto another attribute.</summary>
    [Fact]
    public void XunitNumericEntitiesAndMixedNewlines_PreserveAdjacentCredential()
    {
        string name = XDocument.Load(Path.Combine(RepoRoot,
                "_bmad-output/implementation-artifacts/evidence/story-6-4/qualification-20261017-contracts-debug-full.xml"))
            .Descendants("test").Select(test => test.Attribute("name")!.Value)
            .First(value => IsKnownSyntheticTestNameHash(XunitProbeNameHash(value)));
        string encoded = string.Concat(name.Select(character => "&#" + (int)character + ";"));
        string content = "<assemblies>\r<assembly><collection><test name=\"" + encoded
            + "\" /></collection></assembly>\n" + new string(' ', 40)
            + "<evil pass" + "word=\"" + RandomSecret() + "\" /></assemblies>";
        WithEvidenceRepository(root =>
        {
            const string path = "evidence/mixed-lines.xml";
            foreach (string encoding in new[] { "utf8", "utf8-bom", "utf16-le", "utf16-be",
                "utf16-le-no-bom", "utf16-be-no-bom" })
            {
                WriteEvidenceFile(root, path, EncodeEvidenceText(content, encoding));
                ScanTrackedFile(path, root).ShouldBe([path + ":2"]);
            }
        });
    }

    /// <summary>A runtime JSON read has no literal credential; affixes, fallbacks and literal operands still fail.</summary>
    [Theory]
    [InlineData("accessToken.GetString()")]
    [InlineData("accessToken.GetString()!")]
    [InlineData("document.AccessToken.GetString()!")]
    public void CSharpJsonStringRead_RequiresRuntimeSourceWithoutLiteralOperands(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        const string path = "tests/runtime-read.cs";
        FindViolations(path, Assignment("to" + "ken", expression)).ShouldBeEmpty();
        FindViolations("capture.log", Assignment("to" + "ken", expression)).ShouldNotBeEmpty();
        foreach (string changed in new[] { expression + " + \"" + RandomSecret() + "\"",
            expression + " ?? \"" + RandomSecret() + "\"", expression.Replace("()", "(\"" + RandomSecret() + "\")", StringComparison.Ordinal) })
        {
            FindViolations(path, Assignment("to" + "ken", changed)).ShouldNotBeEmpty();
        }
        FindViolations(path, Assignment("to" + "ken", expression) + "\n" + Assignment("pass" + "word", RandomSecret()))
            .ShouldBe([path + ":2"]);
    }

    private static string XunitProbeNameHash(string name)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            name.Replace("\\\"", "\"", StringComparison.Ordinal)))).ToLowerInvariant();

    private static string MaskKnownSyntheticXunitTestNames(string content)
    {
        if (!content.Contains("<assemblies", StringComparison.Ordinal)) return content;
        var ranges = new List<(int Start, int Length)>();
        var lineStarts = new List<int> { 0 };
        for (int index = 0; index < content.Length; index++)
        {
            if (content[index] == '\r')
            {
                if (index + 1 < content.Length && content[index + 1] == '\n') index++;
                lineStarts.Add(index + 1);
            }
            else if (content[index] == '\n') lineStarts.Add(index + 1);
        }

        try
        {
            using var input = new StringReader(content);
            using XmlReader reader = XmlReader.Create(input, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
            });
            var ancestry = new List<string>();
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element) continue;
                if (ancestry.Count > reader.Depth) ancestry.RemoveRange(reader.Depth, ancestry.Count - reader.Depth);
                ancestry.Add(reader.NamespaceURI.Length == 0 ? reader.LocalName : string.Empty);
                if (ancestry.Count != 4 || !ancestry.SequenceEqual(new[] { "assemblies", "assembly", "collection", "test" })
                    || !reader.MoveToAttribute("name")) continue;
                if (IsKnownSyntheticTestNameHash(XunitProbeNameHash(reader.Value)))
                {
                    var location = (IXmlLineInfo)reader;
                    if (location.LineNumber < 1 || location.LineNumber > lineStarts.Count) return content;
                    int start = lineStarts[location.LineNumber - 1] + location.LinePosition - 1;
                    if (start < 0 || start + 4 > content.Length
                        || !content.AsSpan(start, 4).SequenceEqual("name")) return content;
                    int equals = start + 4;
                    while (equals < content.Length && char.IsWhiteSpace(content[equals])) equals++;
                    if (equals >= content.Length || content[equals] != '=') return content;
                    int quote = equals + 1;
                    while (quote < content.Length && char.IsWhiteSpace(content[quote])) quote++;
                    if (quote >= content.Length || content[quote] is not ('\"' or '\'')) return content;
                    int end = content.IndexOf(content[quote], quote + 1);
                    if (end < 0) return content;
                    string decoded = XElement.Parse("<probe name=" + content[quote..(end + 1)] + " />")
                        .Attribute("name")!.Value;
                    if (!string.Equals(decoded, reader.Value, StringComparison.Ordinal)) return content;
                    ranges.Add((quote + 1, end - quote - 1));
                }
                reader.MoveToElement();
            }
        }
        catch (XmlException)
        {
            return content;
        }

        char[] masked = content.ToCharArray();
        foreach ((int start, int length) in ranges)
        {
            for (int index = start; index < start + length; index++)
            {
                if (masked[index] is not ('\r' or '\n')) masked[index] = ' ';
            }
        }
        return new string(masked);
    }
}
