using Hexalith.EventStore.Client.Reminders;

namespace Hexalith.EventStore.DomainService.Tests.Fixtures;

/// <summary>A synthetic delegation issuer that records every binding it was asked to sign.</summary>
internal sealed class FakeReminderDelegationTokenProvider : IReminderDelegationTokenProvider
{
    /// <summary>Gets every request, in order.</summary>
    public List<ReminderDelegationRequest> Requests { get; } = [];

    /// <summary>Gets or sets the token returned; <see langword="null"/> simulates an unavailable issuer.</summary>
    public string? Token { get; set; } = "synthetic-delegation";

    /// <summary>Gets or sets the failure the issuer throws, simulating an issuer outage.</summary>
    public Exception? Failure { get; set; }

    /// <inheritdoc/>
    public Task<string?> GetDelegationTokenAsync(ReminderDelegationRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return Failure is null ? Task.FromResult(Token) : throw Failure;
    }
}
