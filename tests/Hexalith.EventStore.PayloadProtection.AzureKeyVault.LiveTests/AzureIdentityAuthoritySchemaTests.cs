using System.Text.Json;
using System.Text;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

using Hexalith.EventStore.PayloadProtection.AzureKeyVault.Conformance;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault.LiveTests;

/// <summary>Offline closed-schema checks; no fixture initialization, provider call or environment authority is created.</summary>
public sealed class AzureIdentityAuthoritySchemaTests
{
    /// <summary>The exact diagnostic policy is required in separately supplied live authority.</summary>
    [Theory]
    [InlineData("valid")]
    [InlineData("missing")]
    [InlineData("wrong")]
    [InlineData("case")]
    [InlineData("unknown-field")]
    [InlineData("duplicate-wrong-first")]
    [InlineData("duplicate-same")]
    [InlineData("duplicate-owner")]
    [InlineData("duplicate-escaped")]
    public void LiveDocumentRequiresExactDiagnosticPolicy(string mutation)
    {
        var fields = new Dictionary<string, object?>();
        foreach (string field in new[] { "Owner", "SourceSha", "ResourceIdSha256", "IdentityIdSha256", "NetworkIdSha256", "Region", "VaultUri", "KeyName", "ManagedIdentityClientId", "DeniedManagedIdentityClientId", "InitialVersion", "RotationVersion", "DisabledVersion", "ExpiredVersion", "MissingVersion", "ExceptionDigest", "CorrectionDigest", "CustodyReceiptSha256" })
            fields[field] = "schema-only";
        fields["ExpiresAtUtc"] = DateTimeOffset.UtcNow;
        fields["Operations"] = Array.Empty<string>();
        fields["Faults"] = Array.Empty<string>();
        fields["FaultWindows"] = Array.Empty<object>();
        if (mutation != "missing") fields["DiagnosticPolicyDigest"] = mutation switch
        {
            "wrong" => new string('0', 64),
            "case" => AzureIdentityDiagnosticPolicy.Digest.ToUpperInvariant(),
            _ => AzureIdentityDiagnosticPolicy.Digest,
        };
        if (mutation == "unknown-field") fields["UnexpectedPermission"] = true;
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(fields);
        if (mutation.StartsWith("duplicate-", StringComparison.Ordinal))
        {
            string name = mutation == "duplicate-owner" ? "Owner" : "DiagnosticPolicyDigest";
            string value = mutation == "duplicate-wrong-first" ? new string('0', 64) : mutation == "duplicate-owner" ? "schema-only" : AzureIdentityDiagnosticPolicy.Digest;
            string duplicate = JsonSerializer.Serialize(name) + ":" + JsonSerializer.Serialize(value) + ",";
            if (mutation == "duplicate-escaped") duplicate = duplicate.Replace("DiagnosticPolicyDigest", "DiagnosticPolicyDi" + "\\u0067" + "est", StringComparison.Ordinal);
            bytes = Encoding.UTF8.GetBytes("{" + duplicate + Encoding.UTF8.GetString(bytes)[1..]);
        }
        if (mutation == "valid")
            AzureLiveAuthority.ParsePolicyBoundDocument(bytes).DiagnosticPolicyDigest.ShouldBe(AzureIdentityDiagnosticPolicy.Digest);
        else
            Should.Throw<InvalidOperationException>(() => AzureLiveAuthority.ParsePolicyBoundDocument(bytes));
    }

    /// <summary>Real offline emissions, rather than synthetic channel labels, reach the live observer.</summary>
    [Theory]
    [InlineData("Activity")]
    [InlineData("Metric")]
    [InlineData("ApplicationLog")]
    [InlineData("Exception")]
    public void LiveObserverCapturesActualOwnedChannels(string channel)
    {
        using var capture = new AzureLiveDiagnosticObserver();
        string canary = Guid.NewGuid().ToString("N");
        if (channel == "Activity")
        {
            using var source = new ActivitySource("Azure.Offline.IdentityPolicy");
            using var activity = source.StartActivity("observation");
            activity!.SetTag("private", canary);
        }
        else if (channel == "Metric")
        {
            using var meter = new Meter("Azure.Offline.IdentityPolicy");
            meter.CreateCounter<long>("observation").Add(1, new KeyValuePair<string, object?>("private", canary));
        }
        else if (channel == "ApplicationLog") capture.Logger.LogInformation("{Private}", canary);
        else
        {
            try { throw new InvalidOperationException(canary); }
            catch (InvalidOperationException) { }
        }
        capture.Records.Any(record => record.Channel == channel && record.Content.Contains(canary, StringComparison.Ordinal)).ShouldBeTrue();
        Should.Throw<InvalidOperationException>(() => capture.AssertSafe([], [canary]));
    }

    /// <summary>Every actual fault plaintext is enrolled before use, including its decoded scalar.</summary>
    [Fact]
    public void ActualFaultPlaintextRegistrationRefusesScalarOnlyLeaks()
    {
        using var capture = new AzureLiveDiagnosticObserver();
        byte[] plain = "{\"secret\":\"second-live-canary\"}"u8.ToArray();
        byte[] before = plain.ToArray();
        capture.RegisterPlaintext(plain);
        plain.ShouldBe(before);
        capture.Logger.LogInformation("{Private}", "second-live-canary");
        Should.Throw<InvalidOperationException>(() => capture.AssertSafe([], []));
        System.Security.Cryptography.CryptographicOperations.ZeroMemory(plain);
    }

    /// <summary>Owned listeners stop observing after fixture cleanup.</summary>
    [Fact]
    public void OwnedChannelsStopAfterDispose()
    {
        var capture = new AzureLiveDiagnosticObserver();
        capture.Dispose();
        int count = capture.Records.Length;
        using (var source = new ActivitySource("Azure.Offline.IdentityPolicy"))
        using (var activity = source.StartActivity("after-dispose")) activity?.SetTag("value", "ignored");
        using (var meter = new Meter("Azure.Offline.IdentityPolicy")) meter.CreateCounter<long>("after-dispose").Add(1);
        capture.Logger.LogInformation("After disposal");
        try { throw new InvalidOperationException("after-dispose"); } catch (InvalidOperationException) { }
        capture.Records.Length.ShouldBe(count);
    }

}
