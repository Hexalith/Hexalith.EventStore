using System.Globalization;
using System.Text.RegularExpressions;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Safe effective fields independently parsed from the fixed owner-generated component format.</summary>
internal sealed record QualificationComponentBinding(string RedisAddress, string Name, string Type, string Version, bool ActorStateStore, string KeyPrefix)
{
    internal static QualificationComponentBinding Capture(string text)
    {
        // This finite experiment emits exactly this template. Refuse other formats
        // rather than retaining unknown metadata or interpreting secret values.
        var match = Regex.Match(text, "\\AapiVersion: dapr.io/v1alpha1\\nkind: Component\\nmetadata:\\n  name: statestore\\nspec:\\n  type: state.redis\\n  version: v1\\n  metadata:\\n  - name: redisHost\\n    value: (?<endpoint>127\\.0\\.0\\.1:(?<port>[0-9]+))\\n  - name: actorStateStore\\n    value: \"true\"\\n  - name: keyPrefix\\n    value: appid\\n\\z", RegexOptions.CultureInvariant);
        if (!match.Success || !int.TryParse(match.Groups["port"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int port) || port is < 1 or > 65535)
            throw new InvalidOperationException("The prepared component does not have the finite owned Redis/actor/prefix semantics.");
        return new(match.Groups["endpoint"].Value, "statestore", "state.redis", "v1", true, "appid");
    }
}
