using System.Security.Cryptography;
using System.Text;

using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.DomainService.DurableTasks;
using Hexalith.EventStore.DurableTaskLookupHost;
using Hexalith.EventStore.ServiceDefaults.Authentication;

WebApplicationBuilder builder = WebApplication.CreateBuilder();
if (args is ["authority"])
{
    builder.Services.AddEventStoreWorkloadBindingAuthority(builder.Configuration);
    WebApplication authority = builder.Build();
    authority.MapGet("/alive", () => Results.Ok());
    authority.MapEventStoreWorkloadBindingAuthority();
    await authority.RunAsync();
    return;
}

const string Store = "g1-task-state";
const string Owner = "sample-owner";
const string Domain = "sample-owner-domain";
string sidecar = builder.Configuration["HEXALITH_G1_PRIMARY_GRPC"]
    ?? throw new InvalidOperationException("The G-1 lookup fixture requires its Dapr gRPC endpoint.");
ArgumentException.ThrowIfNullOrWhiteSpace(builder.Configuration[DaprAppChannelToken.ConfigurationKey]);
builder.Services.AddSingleton(_ => new DaprClientBuilder().UseGrpcEndpoint(sidecar).Build());
builder.Services.AddSingleton<IReadModelStore, DaprReadModelStore>();
builder.Services.AddSingleton<IDurableTaskAdmissionAuthorizer, LookupOnlyAdmissionAuthorizer>();
builder.Services.AddSingleton(services => new DurableTaskAdmission(
    services.GetRequiredService<IReadModelStore>(), Store, services.GetRequiredService<IDurableTaskAdmissionAuthorizer>(), TimeProvider.System));
builder.Services.AddEventStoreHttpHostSecurity().AddDurableTaskStepLookup();

WebApplication host = builder.Build();
host.UseEventStoreHttpHostSecurity();
host.MapGet("/alive", () => Results.Ok()).AllowAnonymous();
host.MapDurableTaskStepLookup(
    new(Owner, "sample.provision@1", 2, Domain, DeriveTarget),
    new(Owner, "sample.other@1", 2, Domain, DeriveTarget),
    new(Owner, "sample.named@1", 1, Domain, DeriveTarget));
await host.RunAsync();

static string DeriveTarget(CommittedDurableTaskAdmission task, string stepKey)
    => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{task.Scope.TenantId}:{task.Scope.ActorId}:{stepKey}")));
