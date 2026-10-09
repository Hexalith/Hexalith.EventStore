using Hexalith.EventStore.DomainService;
using P1R.Counter;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddEventStoreDomainService();
#if P1R_CANDIDATE
builder.Services.AddEventStoreBoundedV1DomainSerialization("counter", profile =>
    profile.Add<CounterIncremented>(typeof(CounterIncremented).FullName!, "json", 2,
        (payload, destination, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            destination.Write("{}"u8);
            return Task.CompletedTask;
        }));
#endif
WebApplication app = builder.Build();
app.UseEventStoreDomainService();
app.MapGet("/ready", () => Results.Ok(new { ready = true })).AllowAnonymous();
app.MapGet("/identity", () => Results.Ok(PublishedIdentity.Read())).AllowAnonymous();
app.Run();
