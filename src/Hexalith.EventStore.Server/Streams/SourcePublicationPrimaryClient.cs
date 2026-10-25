using System.Text.Json;
using Dapr.Actors.Client;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.ServiceDefaults.Authentication;

namespace Hexalith.EventStore.Server.Streams;

/// <summary>Exact resource-bound primary actor transport. Missing enrolled scope or resource-capable issuer denies.</summary>
public sealed class SourcePublicationPrimaryClient(IActorProxyFactory proxies, IReadOnlyList<SourcePublicationAuthorityEnrollment> enrollments,
    SourcePublicationAuthorityInstallation installation, SourcePublicationAuthorityCredentials credentials)
{
    /// <summary>Invokes one exact enrolled primary operation over Dapr.</summary>
    public async Task<T?> InvokeAsync<T>(SourcePublicationScope scope, string method, object body, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope); ArgumentException.ThrowIfNullOrWhiteSpace(method); ArgumentNullException.ThrowIfNull(body);
        var enrollment = enrollments.SingleOrDefault(item => item.Scope == scope);
        if (enrollment is null) return default;
        var issuer = method == "install-namespace" ? credentials.Registrar : method == "register-source" || method.StartsWith("write-", StringComparison.Ordinal)
            || method.StartsWith("advance-", StringComparison.Ordinal) ? credentials.Producer : credentials.Reader;
        if (!issuer.CanBindResources) return default;
        var call = new SourcePublicationAuthorityCall(method, scope, enrollment.Epoch, JsonSerializer.SerializeToUtf8Bytes(body, body.GetType()), "");
        string? assertion = await issuer.IssueAsync(new(installation.PrimaryAudience, SourcePublicationWorkloadBinding.PrimaryOperation,
            SourcePublicationWorkloadBinding.Bind(call)), cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(assertion)) return default;
        call = call with { Assertion = assertion };
        byte[]? result = method.EndsWith("namespace", StringComparison.Ordinal) || method == "register-source"
            ? await proxies.CreateActorProxy<ISourcePublicationNamespaceActor>(new(scope.ActorId), SourcePublicationNamespaceActor.ActorTypeName)
                .InvokeAuthenticatedAsync(call).WaitAsync(cancellationToken).ConfigureAwait(false)
            : await proxies.CreateActorProxy<ISourcePublicationIndexActor>(new(scope.ActorId), SourcePublicationIndexActor.ActorTypeName)
                .InvokeAuthenticatedAsync(call).WaitAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return result is null ? default : JsonSerializer.Deserialize<T>(result);
    }
}
