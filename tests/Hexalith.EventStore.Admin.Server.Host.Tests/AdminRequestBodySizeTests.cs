using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Host.Middleware;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

using NSubstitute;

namespace Hexalith.EventStore.Admin.Server.Host.Tests;

public class AdminRequestBodySizeTests : IClassFixture<AdminRequestBodySizeTests.RequestSizeHostFactory> {
    private readonly RequestSizeHostFactory _factory;

    public AdminRequestBodySizeTests(RequestSizeHostFactory factory) => _factory = factory;

    [Theory]
    [InlineData(AdminRequestSizeLimits.OrdinaryJsonBody, false)]
    [InlineData(AdminRequestSizeLimits.OrdinaryJsonBody + 1, true)]
    public async Task Sandbox_EnforcesExactOrdinaryBoundary(long bodySize, bool rejected) {
        _factory.StreamService.ClearReceivedCalls();
        using HttpClient client = CreateAdminClient();
        string json = CreatePaddedJson(
            "{\"commandType\":\"Test.Command\",\"payloadJson\":\"{}\",\"padding\":\"",
            "\"}",
            bodySize);

        using HttpResponseMessage response = await client.PostAsync(
            "/api/v1/admin/streams/tenant-a/domain/aggregate/sandbox",
            CreateJsonContent(json));

        await AssertBoundaryResultAsync(response, rejected);
        if (rejected) {
            _ = await _factory.StreamService.DidNotReceiveWithAnyArgs()
                .SandboxCommandAsync(default!, default!, default!, default!, default);
        }
        else {
            _ = await _factory.StreamService.Received(1).SandboxCommandAsync(
                "tenant-a",
                "domain",
                "aggregate",
                Arg.Any<SandboxCommandRequest>(),
                Arg.Any<CancellationToken>());
        }
    }

    [Theory]
    [InlineData(AdminRequestSizeLimits.OrdinaryJsonBody, false)]
    [InlineData(AdminRequestSizeLimits.OrdinaryJsonBody + 1, true)]
    public async Task CreateTenant_EnforcesExactOrdinaryMutationBoundary(long bodySize, bool rejected) {
        _factory.TenantCommandService.ClearReceivedCalls();
        using HttpClient client = CreateAdminClient();
        string json = CreatePaddedJson(
            "{\"tenantId\":\"tenant-a\",\"name\":\"Tenant A\",\"description\":\"description\",\"padding\":\"",
            "\"}",
            bodySize);

        using HttpResponseMessage response = await client.PostAsync(
            "/api/v1/admin/tenants",
            CreateJsonContent(json));

        await AssertBoundaryResultAsync(response, rejected);
        if (rejected) {
            _ = await _factory.TenantCommandService.DidNotReceiveWithAnyArgs()
                .CreateTenantAsync(default!, default);
        }
        else {
            _ = await _factory.TenantCommandService.Received(1)
                .CreateTenantAsync(Arg.Any<CreateTenantRequest>(), Arg.Any<CancellationToken>());
        }
    }

    [Theory]
    [InlineData(AdminRequestSizeLimits.BackupImportJsonBody, false)]
    [InlineData(AdminRequestSizeLimits.BackupImportJsonBody + 1, true)]
    public async Task BackupImport_EnforcesExactBoundaryIncludingJsonQuotes(long bodySize, bool rejected) {
        _factory.BackupCommandService.ClearReceivedCalls();
        using HttpClient client = CreateAdminClient();
        string json = $"\"{new string('a', checked((int)bodySize - 2))}\"";

        using HttpResponseMessage response = await client.PostAsync(
            "/api/v1/admin/backups/import-stream?tenantId=tenant-a",
            CreateJsonContent(json));

        await AssertBoundaryResultAsync(response, rejected);
        if (rejected) {
            _ = await _factory.BackupCommandService.DidNotReceiveWithAnyArgs()
                .ImportStreamAsync(default!, default!, default);
        }
        else {
            _ = await _factory.BackupCommandService.Received(1)
                .ImportStreamAsync("tenant-a", Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
    }

    [Theory]
    [InlineData(AdminRequestSizeLimits.OrdinaryJsonBody, false)]
    [InlineData(AdminRequestSizeLimits.OrdinaryJsonBody + 1, true)]
    public async Task UnknownLengthSandboxBody_UsesSameBoundaryAndNoWorkContract(long bodySize, bool rejected) {
        _factory.StreamService.ClearReceivedCalls();
        using HttpClient client = CreateAdminClient();
        string json = CreatePaddedJson(
            "{\"commandType\":\"Test.Command\",\"payloadJson\":\"{}\",\"padding\":\"",
            "\"}",
            bodySize);
        using var content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(json)));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        content.Headers.ContentLength = null;

        using HttpResponseMessage response = await client.PostAsync(
            "/api/v1/admin/streams/tenant-a/domain/aggregate/sandbox",
            content);

        await AssertBoundaryResultAsync(response, rejected);
        if (rejected) {
            _ = await _factory.StreamService.DidNotReceiveWithAnyArgs()
                .SandboxCommandAsync(default!, default!, default!, default!, default);
        }
        else {
            _ = await _factory.StreamService.Received(1).SandboxCommandAsync(
                "tenant-a",
                "domain",
                "aggregate",
                Arg.Any<SandboxCommandRequest>(),
                Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task UnknownLengthBody_UnrelatedIOExceptionPropagates() {
        Stream requestBody = Substitute.For<Stream>();
        _ = requestBody
            .ReadAsync(Arg.Any<Memory<byte>>(), Arg.Any<CancellationToken>())
            .Returns<ValueTask<int>>(_ => throw new IOException("transport failure"));
        HttpContext context = CreateMiddlewareContext(requestBody);
        var middleware = new AdminRequestBodySizeMiddleware(_ => Task.CompletedTask);

        IOException exception = await Should.ThrowAsync<IOException>(() => middleware.InvokeAsync(context));

        exception.Message.ShouldBe("transport failure");
        context.Response.StatusCode.ShouldNotBe(StatusCodes.Status413PayloadTooLarge);
    }

    [Fact]
    public async Task UnknownLengthBody_CancellationPropagates() {
        Stream requestBody = Substitute.For<Stream>();
        _ = requestBody
            .ReadAsync(Arg.Any<Memory<byte>>(), Arg.Any<CancellationToken>())
            .Returns<ValueTask<int>>(_ => throw new OperationCanceledException("request cancelled"));
        HttpContext context = CreateMiddlewareContext(requestBody);
        var middleware = new AdminRequestBodySizeMiddleware(_ => Task.CompletedTask);

        _ = await Should.ThrowAsync<OperationCanceledException>(() => middleware.InvokeAsync(context));

        context.Response.StatusCode.ShouldNotBe(StatusCodes.Status413PayloadTooLarge);
    }

    private HttpClient CreateAdminClient() {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken(new Claim(AdminClaimTypes.AdminRole, "Admin")));
        return client;
    }

    private static StringContent CreateJsonContent(string json)
        => new(json, Encoding.UTF8, "application/json");

    private static HttpContext CreateMiddlewareContext(Stream requestBody) {
        var context = new DefaultHttpContext();
        context.Request.Body = requestBody;
        context.Response.Body = new MemoryStream();
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new RequestSizeLimitAttribute(AdminRequestSizeLimits.OrdinaryJsonBody)),
            "body limit test"));
        return context;
    }

    private static string CreatePaddedJson(string prefix, string suffix, long byteLength) {
        int paddingLength = checked((int)(byteLength - Encoding.UTF8.GetByteCount(prefix) - Encoding.UTF8.GetByteCount(suffix)));
        paddingLength.ShouldBeGreaterThanOrEqualTo(0);
        string body = prefix + new string('a', paddingLength) + suffix;
        ((long)Encoding.UTF8.GetByteCount(body)).ShouldBe(byteLength);
        return body;
    }

    private static async Task AssertBoundaryResultAsync(HttpResponseMessage response, bool rejected) {
        if (!rejected) {
            response.StatusCode.ShouldNotBe(HttpStatusCode.RequestEntityTooLarge);
            return;
        }

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        response.Content.Headers.ContentType.ShouldNotBeNull();
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        string body = await response.Content.ReadAsStringAsync();
        body.Length.ShouldBeLessThan(512);
        body.ShouldContain("Payload Too Large");
        body.ShouldNotContain(new string('a', 32));
    }

    private static string CreateToken(params Claim[] claims) {
        var descriptor = new SecurityTokenDescriptor {
            Issuer = "hexalith-dev",
            Audience = "hexalith-eventstore",
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(30),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes("DevOnlySigningKey-AtLeast32Chars!")),
                SecurityAlgorithms.HmacSha256Signature),
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    public class RequestSizeHostFactory : WebApplicationFactory<Program> {
        public IStreamQueryService StreamService { get; } = Substitute.For<IStreamQueryService>();

        public ITenantCommandService TenantCommandService { get; } = Substitute.For<ITenantCommandService>();

        public IBackupCommandService BackupCommandService { get; } = Substitute.For<IBackupCommandService>();

        protected override void ConfigureWebHost(IWebHostBuilder builder) {
            ArgumentNullException.ThrowIfNull(builder);
            _ = StreamService.SandboxCommandAsync(
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<SandboxCommandRequest>(),
                    Arg.Any<CancellationToken>())
                .Returns((SandboxResult?)null);
            _ = TenantCommandService.CreateTenantAsync(Arg.Any<CreateTenantRequest>(), Arg.Any<CancellationToken>())
                .Returns(new AdminOperationResult(true, "operation", "accepted", null));
            _ = BackupCommandService.ImportStreamAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new AdminOperationResult(true, "operation", "accepted", null));

            _ = builder.ConfigureServices(services => {
                _ = services.AddSingleton(Substitute.For<DaprClient>());
                _ = services.AddSingleton(StreamService);
                _ = services.AddSingleton(TenantCommandService);
                _ = services.AddSingleton(BackupCommandService);
            });
        }
    }
}
