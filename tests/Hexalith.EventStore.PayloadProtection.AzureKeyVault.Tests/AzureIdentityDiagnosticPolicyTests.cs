using System.Text;

using Hexalith.EventStore.PayloadProtection.AzureKeyVault.Conformance;
using Hexalith.EventStore.Testing.Security;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests;

/// <summary>Proves the selected-identifier exception cannot exempt another payload, source, event or channel.</summary>
public sealed class AzureIdentityDiagnosticPolicyTests
{
    /// <summary>Only each exact vendor event tuple can carry the enrolled identifier.</summary>
    [Theory]
    [InlineData("LogMsalAlways", 24)]
    [InlineData("ManagedIdentityCredentialSelected", 26)]
    public void ExactVendorTuplePermitsOnlySelectedIdentifier(string name, int eventId)
    {
        string selected = Guid.NewGuid().ToString("D");
        var record = new AzureDiagnosticRecord("EventSource", "Azure-Identity", name, eventId, name == "LogMsalAlways" ? [selected] : ["ManagedIdentityCredential", "ClientId: " + selected], selected);
        AzureIdentityDiagnosticPolicy.Classify(record, [selected], []).ShouldBe("permitted-selected-identifier");
        string summary = AzureIdentityDiagnosticPolicy.AssertSafe([record], [selected], []);
        summary.Contains(selected, StringComparison.Ordinal).ShouldBeFalse();
        summary.ShouldContain(AzureIdentityDiagnosticPolicy.Digest);
    }

    /// <summary>Every part of the exception is exact, including execution-owned credential selection.</summary>
    [Theory]
    [InlineData("source")]
    [InlineData("name")]
    [InlineData("id")]
    [InlineData("swapped-tuple")]
    [InlineData("channel")]
    [InlineData("other-identifier")]
    [InlineData("missing-selection")]
    [InlineData("encoded-identifier")]
    [InlineData("extra-field")]
    [InlineData("extra-identifier")]
    public void ChangedTupleOrIdentityIsRefused(string mutation)
    {
        string selected = Guid.NewGuid().ToString("D");
        var record = new AzureDiagnosticRecord("EventSource", "Azure-Identity", "LogMsalAlways", 24, [selected], selected);
        record = mutation switch
        {
            "source" => record with { Source = "Azure-Identity-extra" },
            "name" => record with { Name = "Other" },
            "id" => record with { EventId = 25 },
            "swapped-tuple" => record with { EventId = 26 },
            "channel" => record with { Channel = "Activity" },
            "other-identifier" => record with { Name = "ManagedIdentityCredentialSelected", EventId = 26, Payload = ["ManagedIdentityCredential", Guid.NewGuid().ToString("D")] },
            "extra-field" => record with { Payload = [selected, "unexpected"] },
            "extra-identifier" => record with { Name = "ManagedIdentityCredentialSelected", EventId = 26, Payload = ["ManagedIdentityCredential", selected + " " + Guid.NewGuid().ToString("D")] },
            "missing-selection" => record with { SelectedClientId = null },
            _ => record with { Payload = [Convert.ToBase64String(Encoding.UTF8.GetBytes(selected))] },
        };
        string decision = AzureIdentityDiagnosticPolicy.Classify(record, [selected], []);
        (decision is "safe" or "permitted-selected-identifier").ShouldBeFalse();
        var failure = Should.Throw<InvalidOperationException>(() => AzureIdentityDiagnosticPolicy.AssertSafe([record], [selected], []));
        failure.Message.Contains(selected, StringComparison.Ordinal).ShouldBeFalse();
        record.ToString().ShouldBe(nameof(AzureDiagnosticRecord));
    }

    /// <summary>All protected canaries and encodings are scanned even after the permitted identifier field.</summary>
    [Theory]
    [InlineData("plain")]
    [InlineData("uri")]
    [InlineData("base64")]
    [InlineData("hex")]
    [InlineData("json")]
    public void PermittedEventStillScansEveryPayloadFieldAndProtectedValue(string encoding)
    {
        string selected = Guid.NewGuid().ToString("D");
        string[] prohibited = [..ProtectedDataLeakSentinel.All(), Guid.NewGuid().ToString("N") + "+\""];
        foreach (string value in prohibited)
        {
            string encoded = encoding switch
            {
                "uri" => Uri.EscapeDataString(value),
                "base64" => Convert.ToBase64String(Encoding.UTF8.GetBytes(value)),
                "hex" => Convert.ToHexStringLower(Encoding.UTF8.GetBytes(value)),
                "json" => AzureDiagnosticRecord.Capture(new { PrivateValue = value }),
                _ => value,
            };
            var record = new AzureDiagnosticRecord("EventSource", "Azure-Identity", "ManagedIdentityCredentialSelected", 26,
                [selected, "unrelated field", encoded], selected);
            AzureIdentityDiagnosticPolicy.Classify(record, [selected], prohibited).ShouldBe("prohibited-payload");
        }
    }

    /// <summary>Other observer channels refuse selected identifiers, including with the approved event metadata.</summary>
    [Theory]
    [InlineData("DiagnosticListener")]
    [InlineData("Exception")]
    [InlineData("Http")]
    [InlineData("Metric")]
    [InlineData("ApplicationLog")]
    public void OtherChannelsNeverInheritVendorPermission(string channel)
    {
        string selected = Guid.NewGuid().ToString("D");
        var record = new AzureDiagnosticRecord(channel, "Azure-Identity", "LogMsalAlways", 24, [selected], selected);
        AzureIdentityDiagnosticPolicy.Classify(record, [selected], []).ShouldBe("identifier-channel");
    }

    /// <summary>Asynchronous credential selections remain isolated and nested selection is restored.</summary>
    [Fact]
    public async Task IdentityScopesFollowOnlyTheirOwnExecution()
    {
        string first = Guid.NewGuid().ToString("D"), second = Guid.NewGuid().ToString("D");
        async Task<bool> ObserveAsync(string expected)
        {
            using var scope = new AzureIdentityDiagnosticScope(expected);
            await Task.Yield();
            return AzureIdentityDiagnosticScope.Current == expected;
        }
        (await Task.WhenAll(ObserveAsync(first), ObserveAsync(second))).ShouldAllBe(value => value);
        AzureIdentityDiagnosticScope.Current.ShouldBeNull();
        using (var outer = new AzureIdentityDiagnosticScope(first))
        {
            using (var inner = new AzureIdentityDiagnosticScope(second))
                (AzureIdentityDiagnosticScope.Current == second).ShouldBeTrue();
            (AzureIdentityDiagnosticScope.Current == first).ShouldBeTrue();
        }
        AzureIdentityDiagnosticScope.Current.ShouldBeNull();
    }
    /// <summary>Unsupported structured payloads remain an explicit refusal rather than disappearing from observations.</summary>
    [Fact]
    public void UnreadablePayloadCannotBecomePermitted()
    {
        string selected = Guid.NewGuid().ToString("D");
        Action unsupported = () => { };
        string captured = AzureDiagnosticRecord.Capture(unsupported);
        captured.ShouldBe(AzureDiagnosticRecord.UnreadablePayload);
        var record = new AzureDiagnosticRecord("EventSource", "Azure-Identity", "LogMsalAlways", 24,
            [selected, captured], selected);
        AzureIdentityDiagnosticPolicy.Classify(record, [selected], []).ShouldBe("unreadable-payload");
        Should.Throw<InvalidOperationException>(() => AzureIdentityDiagnosticPolicy.AssertSafe([record], [selected], []));
    }

    /// <summary>Every reviewed payload field rejects an additional or unselected UUID.</summary>
    [Theory]
    [InlineData("message-extra")]
    [InlineData("message-other-only")]
    [InlineData("type-extra")]
    public void CompleteSchemasRejectUnselectedIdentifiers(string mutation)
    {
        string selected = Guid.NewGuid().ToString("D"), other = Guid.NewGuid().ToString("D");
        var record = new AzureDiagnosticRecord("EventSource", "Azure-Identity", "LogMsalAlways", 24,
            [mutation == "message-other-only" ? other : selected + " " + other], selected);
        if (mutation == "type-extra") record = record with { Name = "ManagedIdentityCredentialSelected", EventId = 26, Payload = [other, selected] };
        AzureIdentityDiagnosticPolicy.Classify(record, [selected], []).ShouldBe("unapproved-identifier");
    }

    /// <summary>Equivalent identity spellings cannot evade a prohibited channel.</summary>
    [Theory]
    [InlineData("D")]
    [InlineData("N")]
    [InlineData("B")]
    [InlineData("P")]
    [InlineData("X")]
    public void EquivalentIdentitySpellingsRemainProhibited(string format)
    {
        Guid identity = Guid.Parse("abcdef12-3456-789a-bcde-f123456789ab");
        string selected = identity.ToString("D");
        var record = new AzureDiagnosticRecord("Activity", "Azure-Identity", "LogMsalAlways", 24,
            [identity.ToString(format).ToUpperInvariant()], selected);
        AzureIdentityDiagnosticPolicy.Classify(record, [selected], []).ShouldBe("identifier-channel");
    }

    /// <summary>Hex casing and equivalent percent-escape syntax retain the protected value's meaning.</summary>
    [Theory]
    [InlineData("upper-hex")]
    [InlineData("lower-hex")]
    [InlineData("percent")]
    public void EquivalentProtectedEncodingsAreRefused(string encoding)
    {
        string prohibited = "Ab+z=";
        string encoded = encoding switch
        {
            "upper-hex" => Convert.ToHexString(Encoding.UTF8.GetBytes(prohibited)),
            "lower-hex" => Convert.ToHexStringLower(Encoding.UTF8.GetBytes(prohibited)),
            _ => "%41b%2bz%3d",
        };
        var record = new AzureDiagnosticRecord("Other", "observer", "Record", null, [encoded], null);
        AzureIdentityDiagnosticPolicy.Classify(record, [], [prohibited]).ShouldBe("prohibited-payload");
    }

    /// <summary>The native SDK positive control refuses empty or dropped vendor observations.</summary>
    [Theory]
    [InlineData("empty")]
    [InlineData("missing24")]
    [InlineData("missing26")]
    public void NativeEvidenceCannotPassWithoutBothSelectedEventKinds(string missing)
    {
        string selected = Guid.NewGuid().ToString("D");
        var records = new List<AzureDiagnosticRecord>();
        if (missing == "missing24") records.Add(new("EventSource", "Azure-Identity", "ManagedIdentityCredentialSelected", 26, ["ManagedIdentityCredential", selected], selected));
        if (missing == "missing26") records.Add(new("EventSource", "Azure-Identity", "LogMsalAlways", 24, [selected], selected));
        Should.Throw<InvalidOperationException>(() => AzureIdentityDiagnosticPolicy.AssertNativeEvidence(records, [selected], []));
    }


    /// <summary>Only the pinned nonprotected correlation field is distinct from the message's client identities.</summary>
    [Theory]
    [InlineData("valid")]
    [InlineData("extra-body")]
    [InlineData("other-body-only")]
    [InlineData("wrong-version")]
    [InlineData("bad-time")]
    [InlineData("pii-enabled")]
    [InlineData("client-in-correlation")]
    [InlineData("prohibited-correlation")]
    public void PinnedMsalEnvelopeNeverExemptsBodyIdentities(string mutation)
    {
        string selected = Guid.NewGuid().ToString("D"), correlation = Guid.NewGuid().ToString("D"), other = Guid.NewGuid().ToString("D");
        string body = mutation == "extra-body" ? selected + " " + other : mutation == "other-body-only" ? other : selected;
        if (mutation == "client-in-correlation") correlation = selected;
        string message = "False MSAL 4.92.0.0 MSAL.NetCore .NET10 Linux [2026-11-03 13:00:00Z - " + correlation + "] " + body;
        if (mutation == "wrong-version") message = message.Replace("4.92.0.0", "4.93.0.0", StringComparison.Ordinal);
        if (mutation == "bad-time") message = message.Replace("13:00:00Z", "99:00:00Z", StringComparison.Ordinal);
        if (mutation == "pii-enabled") message = "True" + message[5..];
        var record = new AzureDiagnosticRecord("EventSource", "Azure-Identity", "LogMsalAlways", 24, [message], selected);
        string decision = AzureIdentityDiagnosticPolicy.Classify(record, [selected], mutation == "prohibited-correlation" ? [correlation] : []);
        if (mutation == "valid") decision.ShouldBe("permitted-selected-identifier");
        else (decision is "safe" or "permitted-selected-identifier").ShouldBeFalse();
    }

}
