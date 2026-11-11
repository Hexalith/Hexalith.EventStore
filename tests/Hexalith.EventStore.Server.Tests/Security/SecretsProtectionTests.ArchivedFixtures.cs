using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Recognizes only unchanged, line-bound negative-test inputs in immutable qualification source snapshots.</summary>
public sealed partial class SecretsProtectionTests
{
    private static bool IsArchivedNegativeFixtureLine(string path, string content, int lineNumber)
    {
        string? expectedLineHash = (path, lineNumber) switch
        {
            ("deploy/dapr/qualification/public-identities/1.19.1-original-inputs/tools/tests/test_dapr_runtime_qualification.py", 660) => "109ae6f079728433ffac49a329c69d0dfce645afaf603319d943cab2e9177467",
            ("deploy/dapr/qualification/public-identities/1.19.1-original-inputs/tools/tests/test_dapr_runtime_qualification.py", 939) => "317e4d92303de1ae955031d826ed479f2284e4929584b51f33afd97a7267d63d",
            ("deploy/dapr/qualification/public-identities/1.19.1-original-inputs/tools/tests/test_dapr_runtime_qualification.py", 1373) => "9e19d087e73806ece93092b6cfa1d47fc04e8feb0c4044c385567d3edc80c7cc",
            ("deploy/dapr/qualification/public-identities/1.19.1-original-inputs/tools/tests/test_dapr_runtime_qualification.py", 1377) => "2dd04797619766f16531ff0adeb7ea93d158512632242e3158a7f60279cec539",
            ("deploy/dapr/qualification/public-identities/1.19.1-original-inputs/tools/tests/test_dapr_runtime_qualification.py.c94e6fff-original", 648) => "d8d7a8aece75239401f6002718cf5faabd51233c411b816aaf8be89a5d166e5e",
            ("deploy/dapr/qualification/public-identities/1.19.1-original-inputs/tools/tests/test_dapr_runtime_qualification.py.c94e6fff-original", 941) => "55fa5ae293fff162118c95201bb44ac5278b2361273e9fae5268a76178c416f5",
            ("deploy/dapr/qualification/public-identities/1.19.1-original-inputs/tools/tests/test_dapr_runtime_qualification.py.c94e6fff-original", 1378) => "bdb8e3b20d0fe9d0e98ee744cf62f536a694ea1583271705617316d14eb45fac",
            ("deploy/dapr/qualification/public-identities/1.19.1-original-inputs/tools/tests/test_dapr_runtime_qualification.py.c94e6fff-original", 1532) => "12fb85a37aac23f89dac7309b924cdfc8f84a98351921f4bd6d83ca8dced27f9",
            ("deploy/dapr/qualification/public-identities/1.19.1-original-inputs/tools/tests/test_qualify_internal_actor_admission.py", 182) => "c27457a6228762a710a37dd7f40d0943a9909604292bfaae7085ad7883558283",
            _ => null,
        };
        if (expectedLineHash is null)
        {
            return false;
        }

        // Binding the complete snapshot prevents copied or recontextualized fixture
        // lines from inheriting recognition. Every unregistered line is still scanned.
        string? expectedContentHash = path switch
        {
            "deploy/dapr/qualification/public-identities/1.19.1-original-inputs/tools/tests/test_dapr_runtime_qualification.py" => "f68536cdb00ae625a4baec9fa6075c7142b6f33900c0fec99d5d2a99d6d5b054",
            "deploy/dapr/qualification/public-identities/1.19.1-original-inputs/tools/tests/test_dapr_runtime_qualification.py.c94e6fff-original" => "5bed2c15f774cf0f6e5669174efe5510632042f65fb33f641aa5541fd70136d0",
            "deploy/dapr/qualification/public-identities/1.19.1-original-inputs/tools/tests/test_qualify_internal_actor_admission.py" => "08630bee9636119b6aaec111353e4341e0134ac4840d03a91f56a3853bafa93b",
            _ => null,
        };
        if (Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content))) != expectedContentHash)
        {
            return false;
        }

        int start = 0;
        for (int line = 1; line < lineNumber; line++)
        {
            int newline = content.IndexOf('\n', start);
            if (newline < 0)
            {
                return false;
            }
            start = newline + 1;
        }
        int end = content.IndexOf('\n', start);
        string sourceLine = content[start..(end < 0 ? content.Length : end)].TrimEnd('\r');
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sourceLine))) == expectedLineHash;
    }

    /// <summary>Only the unchanged snapshot, exact path and registered line admit a known negative-test input.</summary>
    /// <param name="path">The immutable archived fixture source.</param>
    /// <param name="lineNumber">The reviewed synthetic credential line.</param>
    [Theory]
    [InlineData("deploy/dapr/qualification/public-identities/1.19.1-original-inputs/tools/tests/test_dapr_runtime_qualification.py", 660)]
    [InlineData("deploy/dapr/qualification/public-identities/1.19.1-original-inputs/tools/tests/test_dapr_runtime_qualification.py", 939)]
    [InlineData("deploy/dapr/qualification/public-identities/1.19.1-original-inputs/tools/tests/test_dapr_runtime_qualification.py", 1373)]
    [InlineData("deploy/dapr/qualification/public-identities/1.19.1-original-inputs/tools/tests/test_dapr_runtime_qualification.py", 1377)]
    [InlineData("deploy/dapr/qualification/public-identities/1.19.1-original-inputs/tools/tests/test_dapr_runtime_qualification.py.c94e6fff-original", 648)]
    [InlineData("deploy/dapr/qualification/public-identities/1.19.1-original-inputs/tools/tests/test_dapr_runtime_qualification.py.c94e6fff-original", 941)]
    [InlineData("deploy/dapr/qualification/public-identities/1.19.1-original-inputs/tools/tests/test_dapr_runtime_qualification.py.c94e6fff-original", 1378)]
    [InlineData("deploy/dapr/qualification/public-identities/1.19.1-original-inputs/tools/tests/test_dapr_runtime_qualification.py.c94e6fff-original", 1532)]
    [InlineData("deploy/dapr/qualification/public-identities/1.19.1-original-inputs/tools/tests/test_qualify_internal_actor_admission.py", 182)]
    public void ArchivedNegativeFixtures_RequireExactBytesAndRetainAdjacentCredentialDetection(string path, int lineNumber)
    {
        ArgumentNullException.ThrowIfNull(path);
        string original = File.ReadAllText(Path.Combine(RepoRoot, path));
        FindViolations(path, original).ShouldBeEmpty();
        FindViolations("evidence/unregistered.txt", original).ShouldNotBeEmpty();
        FindViolations(path, original + " ").ShouldNotBeEmpty();

        string credential = Assignment("pass" + "word", JsonSerializer.Serialize(RandomSecret()));
        string[] changed = original.Split('\n');
        changed[lineNumber - 1] += "; " + credential;
        FindViolations(path, string.Join('\n', changed)).ShouldContain(path + ":" + lineNumber);

        var adjacent = original.Split('\n').ToList();
        adjacent.Insert(lineNumber, credential);
        FindViolations(path, string.Join('\n', adjacent)).ShouldContain(path + ":" + (lineNumber + 1));

        string[] moved = original.Split('\n');
        moved[lineNumber - 1] = string.Empty;
        moved[lineNumber] = original.Split('\n')[lineNumber - 1];
        FindViolations(path, string.Join('\n', moved)).ShouldContain(path + ":" + (lineNumber + 1));
    }
}
