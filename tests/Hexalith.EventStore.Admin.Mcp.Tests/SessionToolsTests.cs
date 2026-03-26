namespace Hexalith.EventStore.Admin.Mcp.Tests;

using System.Text.Json;

using Hexalith.EventStore.Admin.Mcp.Tools;

public class SessionToolsTests
{
    [Fact]
    public async Task SetContext_ReturnsConfirmation_WithTenantIdAndDomain()
    {
        var session = new InvestigationSession();

        string result = await SessionTools.SetContext(session, tenantId: "acme-corp", domain: "Orders");

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("contextSet").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("tenantId").GetString().ShouldBe("acme-corp");
        doc.RootElement.GetProperty("domain").GetString().ShouldBe("Orders");
    }

    [Fact]
    public async Task SetContext_WithOnlyTenantId_SetsTenantLeaveDomainNull()
    {
        var session = new InvestigationSession();

        string result = await SessionTools.SetContext(session, tenantId: "acme-corp");

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("tenantId").GetString().ShouldBe("acme-corp");
        doc.RootElement.GetProperty("domain").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task SetContext_WithOnlyDomain_SetsDomainLeaveTenantNull()
    {
        var session = new InvestigationSession();

        string result = await SessionTools.SetContext(session, domain: "Orders");

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("tenantId").ValueKind.ShouldBe(JsonValueKind.Null);
        doc.RootElement.GetProperty("domain").GetString().ShouldBe("Orders");
    }

    [Fact]
    public async Task SetContext_AllNullAndFalse_ReturnsInvalidInputError()
    {
        var session = new InvestigationSession();

        string result = await SessionTools.SetContext(session);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("invalid-input");
    }

    [Fact]
    public async Task SetContext_WhitespaceTenantAndDomain_ReturnsInvalidInputError()
    {
        var session = new InvestigationSession();

        string result = await SessionTools.SetContext(session, tenantId: "  ", domain: "\t");

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("invalid-input");
    }

    [Fact]
    public async Task SetContext_PartialUpdate_PreservesExistingValues()
    {
        var session = new InvestigationSession();
        session.SetContext("acme-corp", "Orders");

        string result = await SessionTools.SetContext(session, domain: "Shipping");

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("tenantId").GetString().ShouldBe("acme-corp");
        doc.RootElement.GetProperty("domain").GetString().ShouldBe("Shipping");
    }

    [Fact]
    public async Task SetContext_ClearTenantId_ClearsTenantPreservesDomain()
    {
        var session = new InvestigationSession();
        session.SetContext("acme-corp", "Orders");

        string result = await SessionTools.SetContext(session, clearTenantId: true);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("tenantId").ValueKind.ShouldBe(JsonValueKind.Null);
        doc.RootElement.GetProperty("domain").GetString().ShouldBe("Orders");
    }

    [Fact]
    public async Task SetContext_ClearDomain_ClearsDomainPreservesTenant()
    {
        var session = new InvestigationSession();
        session.SetContext("acme-corp", "Orders");

        string result = await SessionTools.SetContext(session, clearDomain: true);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("tenantId").GetString().ShouldBe("acme-corp");
        doc.RootElement.GetProperty("domain").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task SetContext_TenantIdAndClearTenantIdSimultaneously_ReturnsError()
    {
        var session = new InvestigationSession();

        string result = await SessionTools.SetContext(session, tenantId: "acme-corp", clearTenantId: true);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("invalid-input");
    }

    [Fact]
    public async Task SetContext_DomainAndClearDomainSimultaneously_ReturnsError()
    {
        var session = new InvestigationSession();

        string result = await SessionTools.SetContext(session, domain: "Orders", clearDomain: true);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("error").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("adminApiStatus").GetString().ShouldBe("invalid-input");
    }

    [Fact]
    public async Task SetContext_SwitchingTenant_IncludesCorrectPreviousTenantId()
    {
        var session = new InvestigationSession();
        session.SetContext("acme-corp", null);

        string result = await SessionTools.SetContext(session, tenantId: "beta-corp");

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("previousTenantId").GetString().ShouldBe("acme-corp");
        doc.RootElement.GetProperty("tenantId").GetString().ShouldBe("beta-corp");
    }

    [Fact]
    public async Task SetContext_TrimsTenantAndDomainValues()
    {
        var session = new InvestigationSession();

        string result = await SessionTools.SetContext(session, tenantId: "  acme-corp  ", domain: "  Orders  ");

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("tenantId").GetString().ShouldBe("acme-corp");
        doc.RootElement.GetProperty("domain").GetString().ShouldBe("Orders");
    }

    [Fact]
    public async Task GetContext_ReturnsHasContextFalse_Initially()
    {
        var session = new InvestigationSession();

        string result = await SessionTools.GetContext(session);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("hasContext").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task GetContext_ReturnsHasContextTrue_AfterSettingContext()
    {
        var session = new InvestigationSession();
        session.SetContext("acme-corp", null);

        string result = await SessionTools.GetContext(session);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("hasContext").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task GetContext_ReturnsCorrectTenantIdAndDomain()
    {
        var session = new InvestigationSession();
        session.SetContext("acme-corp", "Orders");

        string result = await SessionTools.GetContext(session);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("tenantId").GetString().ShouldBe("acme-corp");
        doc.RootElement.GetProperty("domain").GetString().ShouldBe("Orders");
    }

    [Fact]
    public async Task ClearContext_ReturnsConfirmationWithAllNullsAndNote()
    {
        var session = new InvestigationSession();
        session.SetContext("acme-corp", "Orders");

        string result = await SessionTools.ClearContext(session);

        using JsonDocument doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("contextCleared").GetBoolean().ShouldBeTrue();
        doc.RootElement.GetProperty("tenantId").ValueKind.ShouldBe(JsonValueKind.Null);
        doc.RootElement.GetProperty("domain").ValueKind.ShouldBe(JsonValueKind.Null);
        doc.RootElement.GetProperty("hasContext").GetBoolean().ShouldBeFalse();
        doc.RootElement.GetProperty("note").GetString()!.ShouldContain("conversation history");
    }

    [Fact]
    public async Task ClearContext_ActuallyClearsState()
    {
        var session = new InvestigationSession();
        session.SetContext("acme-corp", "Orders");

        _ = await SessionTools.ClearContext(session);
        string getResult = await SessionTools.GetContext(session);

        using JsonDocument doc = JsonDocument.Parse(getResult);
        doc.RootElement.GetProperty("hasContext").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task AllSessionTools_ReturnParseableJson()
    {
        var session = new InvestigationSession();

        string setResult = await SessionTools.SetContext(session, tenantId: "acme-corp");
        string getResult = await SessionTools.GetContext(session);
        string clearResult = await SessionTools.ClearContext(session);

        Should.NotThrow(() => JsonDocument.Parse(setResult));
        Should.NotThrow(() => JsonDocument.Parse(getResult));
        Should.NotThrow(() => JsonDocument.Parse(clearResult));
    }
}
