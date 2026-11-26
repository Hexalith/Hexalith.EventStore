using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.DomainService.DurableTasks;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests.DurableTasks;

/// <summary>Runs the opted-in lookup endpoint with the shared signed assertion and channel verifier.</summary>
internal sealed class DurableTaskStepLookupFixture(WebApplication? app, HttpClient? client, DurableTaskStepLookupRuntimeClient? runtime = null) : IAsyncDisposable
{
    /// <summary>The exact owner workload allowed to look up the numbered step.</summary>
    internal const string OwnerWorkload = "sample-owner";

    /// <summary>A signed workload admitted by JWT verification but lacking a lookup grant.</summary>
    internal const string UnregisteredWorkload = "other-owner";

    /// <summary>The domain of the fixture's downstream owner.</summary>
    internal const string TargetDomain = "sample-owner-domain";

    /// <summary>An operation with a grant for a different kind of admission.</summary>
    internal const string OtherOperation = "sample.other@1";

    /// <summary>The operation used by the fixture to verify legacy named checkpoints cannot bind.</summary>
    internal const string NamedOperation = "sample.named@1";

    private const string Audience = "g1-task-host";
    private const string ChannelToken = "durable-task-fixture-channel";
    private const string Issuer = "https://durable-task-fixture.invalid";
    private static readonly string SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    private const string LookupOperation = EventStoreWorkloadOperations.DurableTaskStepLookup;

    /// <summary>Gets the transport used for this lookup receipt.</summary>
    internal string Transport => runtime is null ? "testserver" : "dapr-service-invocation";

    /// <summary>Starts an opted-in host over the supplied persisted admission service.</summary>
    internal static async Task<DurableTaskStepLookupFixture> StartAsync(DurableTaskAdmission admission, string operation)
    {
        if (DurableTaskStepLookupRuntimeClient.IsConfigured)
        {
            return new(null, null, new DurableTaskStepLookupRuntimeClient());
        }

        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        const string workload = DurableTaskStepLookupExtensions.WorkloadSection;
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [DaprAppChannelToken.ConfigurationKey] = ChannelToken,
            [$"{workload}:Audience"] = Audience,
            [$"{workload}:AllowedCallers:0"] = OwnerWorkload,
            [$"{workload}:AllowedCallers:1"] = UnregisteredWorkload,
            ["Authentication:WorkloadJwtBearer:Issuer"] = Issuer,
            ["Authentication:WorkloadJwtBearer:Audience"] = Audience,
            ["Authentication:WorkloadJwtBearer:SigningKey"] = SigningKey,
            ["Authentication:WorkloadJwtBearer:AllowedAlgorithms:0"] = SecurityAlgorithms.HmacSha256,
        });
        builder.Services.AddSingleton(admission).AddEventStoreHttpHostSecurity().AddDurableTaskStepLookup();

        WebApplication application = builder.Build();
        application.UseEventStoreHttpHostSecurity();
        application.MapDurableTaskStepLookup(
            new(OwnerWorkload, operation, 2, TargetDomain, DeriveTarget),
            new(OwnerWorkload, OtherOperation, 2, TargetDomain, DeriveTarget),
            new(OwnerWorkload, NamedOperation, 1, TargetDomain, DeriveTarget));
        try
        {
            await application.StartAsync();
            return new(application, application.GetTestClient());
        }
        catch
        {
            await application.DisposeAsync();
            throw;
        }
    }

    /// <summary>Builds a scoped request for a canonical step of the admitted task.</summary>
    internal static DurableTaskStepLookupRequest RequestFor(DurableTaskLease lease, int number = 2)
    {
        CommittedDurableTaskAdmission task = lease.Task;
        string stepKey = DurableTaskEffectStep.KeyFor(task.TaskId, number);
        return new(task.TaskId, stepKey, task.Scope.Operation, task.RequestSha256, TargetDomain, DeriveTarget(task, stepKey), lease.Fence);
    }

    /// <summary>Looks up the binding and verifies the response preserves the expected original admission.</summary>
    internal async Task<DurableTaskStepLookupResult> ExpectBindingAsync(DurableTaskLease lease, DurableTaskStatus status, string? receipt,
        bool acceptsExecution)
    {
        DurableTaskStepLookupRequest request = RequestFor(lease);
        using HttpResponseMessage response = await SendAsync(request, lease.Task.Scope.TenantId);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl.ShouldNotBeNull().NoStore.ShouldBeTrue();
        DurableTaskStepLookupResult binding = (await response.Content.ReadFromJsonAsync<DurableTaskStepLookupResult>()).ShouldNotBeNull();
        binding.ShouldBe(new DurableTaskStepLookupResult(lease.Task.Scope.TenantId, lease.Task.Scope.ActorId, lease.Task.Scope.Operation,
            lease.Task.RequestSha256, lease.Task.TaskId, request.StepKey, status, receipt, acceptsExecution));
        return binding;
    }

    /// <summary>Verifies that a refused request carries zero response bytes.</summary>
    internal async Task<HttpStatusCode> ExpectDenialAsync(DurableTaskStepLookupRequest request, string? tenant,
        HttpStatusCode expected = HttpStatusCode.NotFound, string workload = OwnerWorkload, string? signedDomain = TargetDomain,
        string operation = LookupOperation, bool channel = true, bool assertion = true)
    {
        using HttpResponseMessage response = await SendAsync(request, tenant, workload, signedDomain, operation, channel, assertion);
        response.StatusCode.ShouldBe(expected);
        (await response.Content.ReadAsStringAsync()).ShouldBeEmpty();
        return response.StatusCode;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        runtime?.Dispose();
        client?.Dispose();
        if (app is not null)
        {
            await app.DisposeAsync();
        }
    }

    private async Task<HttpResponseMessage> SendAsync(DurableTaskStepLookupRequest request, string? tenant,
        string workload = OwnerWorkload, string? signedDomain = TargetDomain, string operation = LookupOperation,
        bool channel = true, bool assertion = true)
    {
        if (runtime is not null)
        {
            return await runtime.SendAsync(request, tenant, workload, signedDomain, operation, channel, assertion);
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, DurableTaskStepLookupExtensions.StepLookupPath)
        {
            Content = JsonContent.Create(request),
        };
        if (channel)
        {
            message.Headers.Add(DaprAppChannelToken.HeaderName, ChannelToken);
        }

        if (assertion)
        {
            message.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, SignAssertion(workload, tenant, signedDomain, operation));
        }

        return await client!.SendAsync(message);
    }

    private static string DeriveTarget(CommittedDurableTaskAdmission task, string canonicalKey)
        => DurableTaskAdmission.Hash(Encoding.UTF8.GetBytes($"{task.Scope.TenantId}:{task.Scope.ActorId}:{canonicalKey}"));

    private static string SignAssertion(string workload, string? tenant, string? domain, string operation)
    {
        var claims = new Dictionary<string, object>
        {
            [EventStoreWorkloadAuthenticationDefaults.CallerClaimType] = workload,
            [EventStoreWorkloadAuthenticationDefaults.OperationClaimType] = operation,
            [EventStoreWorkloadAuthenticationDefaults.ActorBindingClaimType] = "asserted-actor-does-not-replace-admission",
        };
        if (tenant is not null)
        {
            claims.Add(EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType, tenant);
        }

        if (domain is not null)
        {
            claims.Add(EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType, domain);
        }

        DateTime now = DateTime.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(2),
            Claims = claims,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256),
        });
    }
}
