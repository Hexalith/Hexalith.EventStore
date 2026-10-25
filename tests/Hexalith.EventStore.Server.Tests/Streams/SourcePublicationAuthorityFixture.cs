using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Streams;
using Hexalith.EventStore.Server.Tests.TestUtilities;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.Testing.Fakes;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;

using static Hexalith.EventStore.ServiceDefaults.Authentication.ConfigureWorkloadJwtBearerOptions;

namespace Hexalith.EventStore.Server.Tests.Streams;

/// <summary>Real serialized primary/journal owners with independent signed role credentials and controllable authenticated observation ports.</summary>
internal sealed class SourcePublicationAuthorityFixture
{
    internal SourcePublicationScope Scope { get; } = new("tenant-a", "conversation", "deletion", "installation-1");
    internal SourcePublicationAuthorityEnrollment Enrollment { get; set; }
    internal SourcePublicationAuthorityInstallation Installation { get; } = new("independent-authority", "eventstore", EventStoreWorkloadAuthenticationDefaults.WorkloadScheme, "registrar", "independent-journal-store");
    internal InMemoryStateManager JournalBackend { get; } = new();
    internal InMemoryStateManager NamespaceBackend { get; } = new();
    internal InMemoryStateManager IndexBackend { get; } = new();
    internal ISourcePublicationAuthorityInstallationEvidence Evidence { get; } = Substitute.For<ISourcePublicationAuthorityInstallationEvidence>();
    internal IAuthoritativeEventStreamReader Streams { get; } = Substitute.For<IAuthoritativeEventStreamReader>();
    internal ISourcePublicationProjector Projector { get; } = Substitute.For<ISourcePublicationProjector>();
    internal ISourcePublicationAcknowledgementObserver Acknowledgements { get; } = Substitute.For<ISourcePublicationAcknowledgementObserver>();
    internal Dictionary<string, AuthoritativeEventStream> Sources { get; } = new(StringComparer.Ordinal);
    internal IReadOnlyList<SourcePublicationDescriptor> Publications { get; set; } = [];
    internal Dictionary<long, SourcePublicationAcknowledgement> SourceReceipts { get; } = [];
    internal Dictionary<long, SourcePublicationAcknowledgement> ReceiverReceipts { get; } = [];
    internal SourcePublicationAuthorityClient Journal { get; private set; } = null!;
    internal SourcePublicationPrimaryClient Primary { get; private set; } = null!;
    internal SourcePublicationAuthority Authority { get; private set; } = null!;
    internal SourcePublicationNamespaceActor Namespace { get; private set; } = null!;
    internal SourcePublicationIndexActor Index { get; private set; } = null!;
    internal SourcePublicationAuthorityActor JournalActor { get; private set; } = null!;
    internal IActorProxyFactory Proxies { get; } = Substitute.For<IActorProxyFactory>();
    internal bool Current { get; set; } = true;
    internal TimeProvider Clock { get; }
    internal IHttpContextAccessor HttpContexts { get; } = new HttpContextAccessor();
    internal string? LoseJournalResponse { get; set; }
    private readonly WorkloadAssertionValidator _validator;
    private readonly SourcePublicationAuthorityCredentials _credentials;
    private static readonly string _signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private readonly SemaphoreSlim _journalTurns = new(1);
    private readonly SemaphoreSlim _namespaceTurns = new(1);
    private readonly SemaphoreSlim _indexTurns = new(1);

    internal SourcePublicationAuthorityFixture(TimeProvider? clock = null)
    {
        Clock = clock ?? TimeProvider.System;
        Enrollment = new(Scope, "epoch-1", Installation.StorageIdentity, Clock.GetUtcNow().AddHours(1), [], [],
            "qualified-inventory", "qualified-all-writer-enforcement", "producer", "observer", "registrar", "reader", "recovery", "receiver-v1", "delivery-1");
        Evidence.IsCurrent(Arg.Any<SourcePublicationAuthorityEnrollment>()).Returns(call => Current
            && RecoverableAnchoredState.Digest(call.Arg<SourcePublicationAuthorityEnrollment>()) == RecoverableAnchoredState.Digest(Enrollment));
        var bearer = new JwtBearerOptions { TokenValidationParameters = new()
        {
            ValidIssuer = WorkloadAssertionTestTokens.Issuer, ValidAudience = Installation.Audience, ValidateIssuer = true,
            ValidateAudience = true, ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_signingKey)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ValidateLifetime = true, RequireExpirationTime = true,
            RequireSignedTokens = true, ClockSkew = TimeSpan.Zero,
            PropertyBag = new Dictionary<string, object> { [ConfiguredMarkerKey] = true },
        } };
        var bearerOptions = Substitute.For<IOptionsMonitor<JwtBearerOptions>>(); bearerOptions.Get(Arg.Any<string>()).Returns(bearer);
        var workload = Substitute.For<IOptionsMonitor<WorkloadAuthenticationOptions>>();
        workload.Get(Arg.Any<string>()).Returns(new WorkloadAuthenticationOptions { Audience = Installation.Audience, AllowedCallers = ["registrar", "reader", "observer", "producer", "recovery"] });
        _validator = new(bearerOptions, workload, WorkloadSecurityClock.System);
        _credentials = new(Issuer("producer"), Issuer("observer"), Issuer("registrar"), Issuer("reader"), Issuer("recovery"));
        Streams.ReadAsync(Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(), Arg.Any<CancellationToken>()).Returns(call =>
            Sources.TryGetValue(call.Arg<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>().ActorId, out var source)
                ? new AuthoritativeStreamReadResult(Clone(source), null) : new(null, "source-unavailable"));
        Projector.Project(Arg.Any<AuthoritativeEventStream>(), Arg.Any<CancellationToken>()).Returns(call => Publications
            .Where(item => item.Identity == call.Arg<AuthoritativeEventStream>().Identity && item.SourceRevision <= call.Arg<AuthoritativeEventStream>().Head).ToArray());
        Acknowledgements.ReadSourceAsync(Arg.Any<SourcePublicationIndexEntry>(), Arg.Any<CancellationToken>()).Returns(call => SourceReceipts.GetValueOrDefault(call.Arg<SourcePublicationIndexEntry>().Offset));
        Acknowledgements.ReadReceiverAsync(Arg.Any<SourcePublicationIndexEntry>(), Arg.Any<CancellationToken>()).Returns(call => ReceiverReceipts.GetValueOrDefault(call.Arg<SourcePublicationIndexEntry>().Offset));
        var journalProxy = Substitute.For<ISourcePublicationAuthorityActor>();
        journalProxy.CallAsync(Arg.Any<SourcePublicationAuthorityCall>()).Returns(async call =>
        {
            await _journalTurns.WaitAsync();
            try
            {
            var request = Clone(call.Arg<SourcePublicationAuthorityCall>());
            byte[]? result = await JournalActor.CallAsync(request);
            if (LoseJournalResponse == request.Method) { LoseJournalResponse = null; throw new HttpRequestException("Controlled lost journal response."); }
            return result;
            }
            finally { _journalTurns.Release(); }
        });
        Proxies.CreateActorProxy<ISourcePublicationAuthorityActor>(Arg.Any<ActorId>(), SourcePublicationAuthorityActor.ActorTypeName).Returns(journalProxy);
        var namespaceProxy = Substitute.For<ISourcePublicationNamespaceActor>();
        namespaceProxy.InvokeAuthenticatedAsync(Arg.Any<SourcePublicationAuthorityCall>()).Returns(async call =>
        {
            await _namespaceTurns.WaitAsync();
            try { return await Namespace.InvokeAuthenticatedAsync(Clone(call.Arg<SourcePublicationAuthorityCall>())); }
            finally { _namespaceTurns.Release(); }
        });
        Proxies.CreateActorProxy<ISourcePublicationNamespaceActor>(Arg.Any<ActorId>(), SourcePublicationNamespaceActor.ActorTypeName).Returns(namespaceProxy);
        var indexProxy = Substitute.For<ISourcePublicationIndexActor>();
        indexProxy.InvokeAuthenticatedAsync(Arg.Any<SourcePublicationAuthorityCall>()).Returns(async call =>
        {
            await _indexTurns.WaitAsync();
            try { return await Index.InvokeAuthenticatedAsync(Clone(call.Arg<SourcePublicationAuthorityCall>())); }
            finally { _indexTurns.Release(); }
        });
        Proxies.CreateActorProxy<ISourcePublicationIndexActor>(Arg.Any<ActorId>(), SourcePublicationIndexActor.ActorTypeName).Returns(indexProxy);
        Recreate();
    }
    internal void Recreate()
    {
        JournalActor = new(ActorHost.CreateForTest<SourcePublicationAuthorityActor>(new ActorTestOptions { ActorId = new(Scope.ActorId) }), Installation, Evidence, _validator, Clock);
        ActorStateManagerTestHelper.SetStateManager(JournalActor, JournalBackend);
        Journal = new(Proxies, Installation, _credentials); Primary = new(Proxies, [Clone(Enrollment)], Installation, _credentials);
        Authority = new(Journal, [Clone(Enrollment)], Installation, Evidence, _validator, Streams, Projector, Acknowledgements, Clock, HttpContexts);
        Namespace = new(ActorHost.CreateForTest<SourcePublicationNamespaceActor>(new ActorTestOptions { ActorId = new(Scope.ActorId) }), Authority);
        ActorStateManagerTestHelper.SetStateManager(Namespace, NamespaceBackend);
        Index = new(ActorHost.CreateForTest<SourcePublicationIndexActor>(new ActorTestOptions { ActorId = new(Scope.ActorId) }), Authority);
        ActorStateManagerTestHelper.SetStateManager(Index, IndexBackend);
    }
    internal Task<bool> InstallAsync() => Primary.InvokeAsync<bool>(Scope, "install-namespace", new SourcePublicationNamespaceState(Scope, 1,
        Enrollment.Epoch, Enrollment.InventoryEvidenceId, Enrollment.WriterEnforcementEvidenceId, Enrollment.InitialSources, Enrollment.InitialSources));
    internal Task<SourcePublicationNamespaceState?> ReadNamespaceAsync() => Primary.InvokeAsync<SourcePublicationNamespaceState>(Scope, "read-namespace", Scope);
    internal Task<SourcePublicationIndexState?> ReadIndexAsync() => Primary.InvokeAsync<SourcePublicationIndexState>(Scope, "read-index", Scope);
    internal SourcePublicationAuthorityCall SignedCall(string method, object body, string caller = "reader")
    {
        var call = new SourcePublicationAuthorityCall(method, Scope, Enrollment.Epoch, JsonSerializer.SerializeToUtf8Bytes(body, body.GetType()), "");
        return call with { Assertion = WorkloadAssertionTestTokens.Create(Installation.PrimaryAudience, caller,
            [SourcePublicationWorkloadBinding.PrimaryOperation], bindings: SourcePublicationWorkloadBinding.Bind(call), signingKey: _signingKey) };
    }
    internal static T Clone<T>(T item) => JsonSerializer.Deserialize<T>(JsonSerializer.SerializeToUtf8Bytes(item))!;
    private static IWorkloadAssertionIssuer Issuer(string caller)
    {
        var issuer = Substitute.For<IWorkloadAssertionIssuer>(); issuer.CanBindResources.Returns(true);
        issuer.IssueAsync(Arg.Any<WorkloadAssertionRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var request = call.Arg<WorkloadAssertionRequest>();
            return ValueTask.FromResult<string?>(WorkloadAssertionTestTokens.Create(request.Audience, caller, [request.Operation], bindings: request.Bindings, signingKey: _signingKey));
        });
        return issuer;
    }
}
