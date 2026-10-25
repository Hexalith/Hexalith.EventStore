using System.Text.Json;
using Dapr.Actors.Client;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.ServiceDefaults.Authentication;

namespace Hexalith.EventStore.Server.Streams;

/// <summary>Resource-bound Dapr transport to the independent authority owner; enrollment and exact recovery remain explicit operations.</summary>
public sealed class SourcePublicationAuthorityClient(IActorProxyFactory proxies, SourcePublicationAuthorityInstallation installation,
    SourcePublicationAuthorityCredentials credentials)
{
    /// <summary>Installs one independently approved enrollment. Changed enrollment requires a separately accepted installation/epoch.</summary>
    public Task<bool> EnrollAsync(SourcePublicationAuthorityEnrollment enrollment, CancellationToken cancellationToken = default)
    { ArgumentNullException.ThrowIfNull(enrollment); return CallAsync<bool>(enrollment, "enroll", enrollment, cancellationToken); }
    /// <summary>Conditionally changes the current delivery binding or epoch while retaining every original identity, anchor and journal.</summary>
    public Task<bool> RenewEnrollmentAsync(SourcePublicationAuthorityEnrollmentChange change, CancellationToken cancellationToken = default)
    { ArgumentNullException.ThrowIfNull(change); return CallAsync<bool>(change.Successor, "renew-enrollment", change, cancellationToken); }
    /// <summary>Reads only currently authenticated independent journal state.</summary>
    public Task<SourcePublicationAuthorityState?> ReadAsync(SourcePublicationAuthorityEnrollment enrollment, CancellationToken cancellationToken = default)
    { ArgumentNullException.ThrowIfNull(enrollment); return CallAsync<SourcePublicationAuthorityState?>(enrollment, "read", enrollment.Scope, cancellationToken); }
    /// <summary>Looks up the exact original independently of primary namespace/index state or the original caller.</summary>
    public Task<T?> LookupAsync<T>(SourcePublicationAuthorityEnrollment enrollment, string key, CancellationToken cancellationToken = default)
    { ArgumentNullException.ThrowIfNull(enrollment); ArgumentException.ThrowIfNullOrWhiteSpace(key); return CallAsync<T?>(enrollment, "lookup", key, cancellationToken); }
    internal async Task<T?> CallAsync<T>(SourcePublicationAuthorityEnrollment enrollment, string method, object body, CancellationToken cancellationToken = default)
    {
        var issuer = method switch
        {
            "enroll" or "renew-enrollment" => credentials.Registrar,
            "read" => credentials.Reader,
            "lookup" or "transition-recover" => credentials.Recovery,
            "reconciliation" or "dispatch" => credentials.Observer,
            _ => credentials.Producer,
        };
        if (!issuer.CanBindResources) return default;
        var call = new SourcePublicationAuthorityCall(method, enrollment.Scope, enrollment.Epoch, JsonSerializer.SerializeToUtf8Bytes(body, body.GetType()), "");
        string? assertion = await issuer.IssueAsync(new(installation.Audience, SourcePublicationWorkloadBinding.AuthorityOperation,
            SourcePublicationWorkloadBinding.Bind(call)), cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(assertion)) return default;
        byte[]? result = await proxies.CreateActorProxy<ISourcePublicationAuthorityActor>(new(enrollment.Scope.ActorId), SourcePublicationAuthorityActor.ActorTypeName)
            .CallAsync(call with { Assertion = assertion }).WaitAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return result is null ? default : JsonSerializer.Deserialize<T>(result);
    }
}
