using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

public class LiveAdmissionRouteCatalogTests
{
    /// <summary>Preserves large nonseekable bodies, exact read positions and immutable catalog selection.</summary>
    [Theory]
    [InlineData("/actors/AggregateActor/tenant-b:counter:id/method/ProcessCommandAsync", false, "tenant-b", false)]
    [InlineData("/actors/AggregateActor/tenant-b:counter:id/method/ProcessCommandAsync", false, "tenant-b", true)]
    [InlineData("/project/rebuild/stage/v1", true, "tenant-a", false)]
    [InlineData("/project/rebuild/stage/v1", true, "tenant-a", true)]
    public async Task StartupBufferingPreservesLargeNonseekableRequestsAndCatalogScope(string path, bool json, string tenant, bool partial)
    {
        byte[] body = json
            ? Encoding.UTF8.GetBytes("{\"tenant\":\"tenant-a\",\"padding\":\"" + new string('x', 2_097_152) + "\"}")
            : Enumerable.Repeat((byte)255, 2_097_216).ToArray();
        using var input = new MemoryStream(body);
        using var observed = new LiveByteCountingStream(input);
        observed.CanSeek.ShouldBeFalse();
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Body = observed;
        var http = new HttpContextAccessor { HttpContext = context };
        using var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);
        new LiveAdmissionCatalogStartupFilter().Configure(builder => builder.Run(async requestContext =>
        {
            requestContext.Request.Body.CanSeek.ShouldBe(json);
            using var consumed = new MemoryStream();
            int consumedBytes = partial ? 257 : body.Length;
            if (partial)
            {
                byte[] prefix = new byte[consumedBytes];
                await requestContext.Request.Body.ReadExactlyAsync(prefix);
                await consumed.WriteAsync(prefix);
            }
            else await requestContext.Request.Body.CopyToAsync(consumed);
            consumed.Length.ShouldBe((long)consumedBytes);
            SHA256.HashData(consumed.ToArray()).ShouldBe(SHA256.HashData(body.AsSpan(0, consumedBytes)));
            long position = json ? requestContext.Request.Body.Position : observed.Bytes;
            position.ShouldBe((long)consumedBytes);
            var (authority, _, _, _) = Create(http);
            await using var lease = await authority.AcquireAsync();
            lease.Snapshot.Resolve("command", tenant, "counter", "IncrementCounter").Target.ResolverTenantPattern.ShouldBe(tenant);
            if (json) requestContext.Request.Body.Position.ShouldBe(position);
            else observed.Bytes.ShouldBe(position);
            using var remaining = new MemoryStream();
            await requestContext.Request.Body.CopyToAsync(remaining);
            remaining.Length.ShouldBe(body.LongLength - consumedBytes);
            SHA256.HashData(remaining.ToArray()).ShouldBe(SHA256.HashData(body.AsSpan(consumedBytes)));
            using var replayed = new MemoryStream();
            if (json)
            {
                requestContext.Request.Body.Position = 0;
                await requestContext.Request.Body.CopyToAsync(replayed);
            }
            else
            {
                consumed.Position = 0;
                await consumed.CopyToAsync(replayed);
                remaining.Position = 0;
                await remaining.CopyToAsync(replayed);
            }
            replayed.Length.ShouldBe(body.LongLength);
            SHA256.HashData(replayed.ToArray()).ShouldBe(SHA256.HashData(body));
            observed.Bytes.ShouldBe(body.LongLength);
        }))(app);
        try { await app.Build()(context); }
        finally
        {
            http.HttpContext = null;
            await context.Request.Body.DisposeAsync();
        }
    }

    [Theory]
    [InlineData("/actors/AggregateActor/tenant-b:counter:id/method/ProcessCommandAsync", false, "tenant-b")]
    [InlineData("/api/v1/commands", true, "tenant-a")]
    public async Task RealActorPartitionPrecedesBinaryArgumentsAndHttpJsonIsRewound(string path, bool json, string tenant)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Body = new MemoryStream(json ? "{\"tenant\":\"tenant-a\"}"u8.ToArray() : [255, 0, 1]);
        context.Request.Body.Position = context.Request.Body.Length;
        var http = new HttpContextAccessor { HttpContext = context };
        try
        {
            var (authority, _, _, _) = Create(http);
            await using var lease = await authority.AcquireAsync();
            lease.Snapshot.Resolve("command", tenant, "counter", "IncrementCounter").Target.ResolverTenantPattern.ShouldBe(tenant);
            context.Request.Body.Position.ShouldBe(context.Request.Body.Length);
        }
        finally { http.HttpContext = null; }
    }

    [Theory]
    [InlineData("/actors/AggregateActor/outside:counter:id/method/ProcessCommandAsync", "not-json")]
    [InlineData("/not-an-actor/tenant-a:counter:id", "not-json")]
    [InlineData("/api/v1/commands", "{\"tenant\":\"tenant-a\",\"other\":{\"tenantId\":\"tenant-b\"}}")]
    public async Task UnboundOrAmbiguousRequestCannotSelectACommittedTenant(string path, string body)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(body));
        var http = new HttpContextAccessor { HttpContext = context };
        try
        {
            var (authority, _, _, _) = Create(http);
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authority.AcquireAsync());
        }
        finally { http.HttpContext = null; }
    }

    [Fact]
    public async Task ImmutableAuthoritySelectsOnlyExactTenantAndDeclaredRoutesAndClosesRealLease()
    {
        var (authority, _, _, _) = Create();
        string firstRoot;
        using (var scope = new LiveAdmissionCatalogScope("tenant-a"))
        {
            var lease = await authority.AcquireAsync();
            firstRoot = lease.Snapshot.RootDigest;
            lease.Snapshot.Resolve("command", "tenant-a", "counter", "IncrementCounter").Target.ResolverTenantPattern.ShouldBe("tenant-a");
            Should.Throw<RouteCatalogUnavailableException>(() => lease.Snapshot.Resolve("command", "tenant-b", "counter", "IncrementCounter"));
            Should.Throw<RouteCatalogUnavailableException>(() => lease.Snapshot.Resolve("command", "tenant-a", "counter", "ResetCounter"));
            await lease.DisposeAsync();
            bool invoked = false;
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => lease.InvokeRemoteAsync(() => { invoked = true; return Task.FromResult(true); }));
            invoked.ShouldBeFalse();
        }
        using (var scope = new LiveAdmissionCatalogScope("tenant-b"))
        {
            await using var lease = await authority.AcquireAsync();
            lease.Snapshot.RootDigest.ShouldNotBe(firstRoot);
            lease.Snapshot.Resolve("command", "tenant-b", "counter", "IncrementCounter").Target.ResolverTenantPattern.ShouldBe("tenant-b");
            Should.Throw<RouteCatalogUnavailableException>(() => lease.Snapshot.Resolve("command", "tenant-a", "counter", "IncrementCounter"));
        }
        using (var scope = new LiveAdmissionCatalogScope("outside"))
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authority.AcquireAsync());
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authority.AcquireAsync());
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("revision")]
    [InlineData("profile")]
    [InlineData("environment")]
    [InlineData("development")]
    [InlineData("active-generation")]
    [InlineData("readers")]
    public async Task IndependentHostProfileAndPolicyMismatchRefusesAcquisition(string change)
    {
        var (authority, options, admission, environment) = Create();
        using var scope = new LiveAdmissionCatalogScope("tenant-a");
        switch (change)
        {
            case "identity": options.Identity = options.Identity! with { AppId = "another-owner" }; break;
            case "revision": options.Identity = options.Identity! with { TopologyDigest = new string('a', 64) }; break;
            case "profile": options.Profile = "Development"; break;
            case "environment": environment.EnvironmentName.Returns("Production"); break;
            case "development": options.DevelopmentFixture = true; break;
            case "active-generation": admission.Value.Returns(new IdempotencyAdmissionOptions { Enabled = true, ActiveDigestKeyVersion = "other-v1" }); break;
            case "readers": admission.Value.Returns(new IdempotencyAdmissionOptions { Enabled = true, ActiveDigestKeyVersion = "unit-v1", ReaderDigestKeyVersions = ["undeclared-v0"] }); break;
        }
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authority.AcquireAsync());
    }

    [Fact]
    public async Task LifecycleMutationAndUnknownIncomingRootAreRefusedWithoutChangingInventory()
    {
        var (authority, _, _, _) = Create();
        using var scope = new LiveAdmissionCatalogScope("tenant-a");
        string root = (await authority.ReadbackAsync()).RootDigest;
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authority.PrepareAsync(null!));
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authority.ReadyAsync("unowned-attempt"));
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authority.CommitAsync("unowned-attempt"));
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authority.RollbackAsync(null!));
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authority.ReconcileAsync(null!));
        (await authority.ReadbackAsync()).RootDigest.ShouldBe(root);
        var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        http.HttpContext.Request.Headers[RouteCatalogInvocationHandler.RootHeader] = new string('b', 64);
        var (requestAuthority, _, _, _) = Create(http);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => requestAuthority.AcquireAsync());
        http.HttpContext = null;
    }

    private static (LiveAdmissionRouteCatalog Authority, RouteCatalogHostOptions Options,
        IOptions<IdempotencyAdmissionOptions> Admission, IWebHostEnvironment Environment) Create(IHttpContextAccessor? http = null)
    {
        var binding = new LiveAdmissionCatalogBinding("unit-run", "gateway-1", "eventstore", "eventstore", "sample",
            ["tenant-a", "tenant-b"], "unit-v1", []);
        var options = new RouteCatalogHostOptions { Identity = binding.Identity, Profile = binding.Profile };
        var admission = Substitute.For<IOptions<IdempotencyAdmissionOptions>>();
        admission.Value.Returns(new IdempotencyAdmissionOptions { Enabled = true, ActiveDigestKeyVersion = "unit-v1", ReaderDigestKeyVersions = [] });
        var environment = Substitute.For<IWebHostEnvironment>();
        environment.EnvironmentName.Returns("Testing");
        return (new(binding, Options.Create(options), admission, environment, http ?? new HttpContextAccessor()), options, admission, environment);
    }
}
