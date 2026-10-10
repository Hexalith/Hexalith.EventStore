using Hexalith.EventStore.DomainService;
using P1R.Counter;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddEventStoreDomainService();
#if P1R_CANDIDATE
string writeAlias = builder.Configuration["P1R_EVOLUTION_WRITE_ALIAS"] == "old"
    ? FixtureEvolutionManifest.LegacyAlias
    : typeof(CounterIncremented).FullName!;
builder.Services.AddEventStoreBoundedV1DomainSerialization("counter", profile =>
    profile.Add<CounterIncremented>(writeAlias, "json", 2,
        (payload, destination, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            destination.Write("{}"u8);
            return Task.CompletedTask;
        }));
string fixtureFingerprint = FixtureEvolutionManifest.Register(builder.Services);
#endif
object evolutionRegistration = new
{
    manifest_registered = builder.Services.Any(descriptor =>
        descriptor.ServiceType.FullName == "Hexalith.EventStore.Client.Events.EventEvolutionManifestCandidate"),
#if P1R_CANDIDATE
    bounded_v1_serializer_registered = true,
    write_alias = writeAlias,
    fixture_manifest_fingerprint = fixtureFingerprint,
    fixture_manifest_test_only = true,
    gateway_authority = false,
#else
    bounded_v1_serializer_registered = false,
    write_alias = typeof(CounterIncremented).FullName,
#endif
};
WebApplication app = builder.Build();
app.UseEventStoreDomainService();
#if P1R_CANDIDATE || P1R_SOURCE
app.MapGet("/ready", () => Results.Ok(new { ready = true, identity = PublishedIdentity.Read(), evolution_registration = evolutionRegistration })).AllowAnonymous();
#else
app.MapGet("/ready", () => Results.Ok(new { ready = true, evolution_registration = evolutionRegistration })).AllowAnonymous();
app.MapGet("/identity", () => Results.Ok(PublishedIdentity.Read())).AllowAnonymous();
#endif
app.Run();
