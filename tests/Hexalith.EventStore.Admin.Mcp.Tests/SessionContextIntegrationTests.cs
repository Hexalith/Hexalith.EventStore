namespace Hexalith.EventStore.Admin.Mcp.Tests;

using System.Net;

using Hexalith.EventStore.Testing.Http;
using Hexalith.EventStore.Admin.Mcp.Tools;

public class SessionContextIntegrationTests
{
    [Fact]
    public async Task StreamList_UsesSessionTenantId_WhenParameterIsNull()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            """{"items":[],"totalCount":0,"continuationToken":null}""");
        var client = new AdminApiClient(httpClient);
        var session = new InvestigationSession();
        session.SetContext("acme-corp", null);

        _ = await StreamTools.ListStreams(client, session, tenantId: null, cancellationToken: ct);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldContain("tenantId=acme-corp");
    }

    [Fact]
    public async Task StreamList_UsesExplicitTenantId_WhenBothSessionAndParameterProvided()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            """{"items":[],"totalCount":0,"continuationToken":null}""");
        var client = new AdminApiClient(httpClient);
        var session = new InvestigationSession();
        session.SetContext("acme-corp", null);

        _ = await StreamTools.ListStreams(client, session, tenantId: "beta-corp", cancellationToken: ct);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldContain("tenantId=beta-corp");
        capturedUri.PathAndQuery.ShouldNotContain("acme-corp");
    }

    [Fact]
    public async Task StreamList_UsesSessionDomain_WhenParameterIsNull()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            """{"items":[],"totalCount":0,"continuationToken":null}""");
        var client = new AdminApiClient(httpClient);
        var session = new InvestigationSession();
        session.SetContext(null, "Orders");

        _ = await StreamTools.ListStreams(client, session, domain: null, cancellationToken: ct);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldContain("domain=Orders");
    }

    [Fact]
    public async Task StreamList_UsesSessionContext_WhenExplicitParametersAreWhitespace()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            """{"items":[],"totalCount":0,"continuationToken":null}""");
        var client = new AdminApiClient(httpClient);
        var session = new InvestigationSession();
        session.SetContext("acme-corp", "Orders");

        _ = await StreamTools.ListStreams(client, session, tenantId: "  ", domain: "\t", cancellationToken: ct);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldContain("tenantId=acme-corp");
        capturedUri.PathAndQuery.ShouldContain("domain=Orders");
    }

    [Fact]
    public async Task StreamList_TrimsExplicitTenantAndDomain()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            """{"items":[],"totalCount":0,"continuationToken":null}""");
        var client = new AdminApiClient(httpClient);
        var session = new InvestigationSession();

        _ = await StreamTools.ListStreams(client, session, tenantId: "  beta-corp  ", domain: "  Sales  ", cancellationToken: ct);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldContain("tenantId=beta-corp");
        capturedUri.PathAndQuery.ShouldContain("domain=Sales");
    }

    [Fact]
    public async Task ProjectionList_UsesSessionTenantId_WhenParameterIsNull()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            "[]");
        var client = new AdminApiClient(httpClient);
        var session = new InvestigationSession();
        session.SetContext("acme-corp", null);

        _ = await ProjectionTools.ListProjections(client, session, tenantId: null, cancellationToken: ct);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldContain("tenantId=acme-corp");
    }

    [Fact]
    public async Task ConsistencyList_UsesSessionTenantId_WhenParameterIsNull()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            "[]");
        var client = new AdminApiClient(httpClient);
        var session = new InvestigationSession();
        session.SetContext("acme-corp", null);

        _ = await ConsistencyTools.ListChecks(client, session, tenantId: null, cancellationToken: ct);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldContain("tenantId=acme-corp");
    }

    [Fact]
    public async Task StorageOverview_UsesSessionTenantId_WhenParameterIsNull()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            """{"totalEventCount":0,"totalSizeBytes":0,"tenantBreakdown":[],"totalStreamCount":0}""");
        var client = new AdminApiClient(httpClient);
        var session = new InvestigationSession();
        session.SetContext("acme-corp", null);

        _ = await StorageTools.GetStorageOverview(client, session, tenantId: null, cancellationToken: ct);

        capturedUri.ShouldNotBeNull();
        capturedUri.PathAndQuery.ShouldContain("tenantId=acme-corp");
    }

    [Fact]
    public async Task TypesList_UsesSessionDomain_WhenParameterIsNull()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        List<Uri?> capturedUris = [];
        var handler = new MockHttpMessageHandler((request, _) =>
        {
            capturedUris.Add(request.RequestUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json"),
            });
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost:5443") };
        var client = new AdminApiClient(httpClient);
        var session = new InvestigationSession();
        session.SetContext(null, "Orders");

        _ = await TypeCatalogTools.ListTypes(client, session, domain: null, cancellationToken: ct);

        capturedUris.Count.ShouldBe(3);
        capturedUris.ShouldAllBe(u => u!.PathAndQuery.Contains("domain=Orders"));
    }

    [Fact]
    public async Task AllTools_WorkCorrectly_WhenSessionHasNoContext()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Uri? capturedUri = null;
        using HttpClient httpClient = MockHttpMessageHandler.CreateCapturingClient(
            r => capturedUri = r.RequestUri,
            HttpStatusCode.OK,
            """{"items":[],"totalCount":0,"continuationToken":null}""");
        var client = new AdminApiClient(httpClient);
        var session = new InvestigationSession(); // No context set

        _ = await StreamTools.ListStreams(client, session, cancellationToken: ct);

        capturedUri.ShouldNotBeNull();
        // With no session context and no explicit params, no tenantId/domain should be in URL
        capturedUri.PathAndQuery.ShouldNotContain("tenantId=");
        capturedUri.PathAndQuery.ShouldNotContain("domain=");
    }
}
