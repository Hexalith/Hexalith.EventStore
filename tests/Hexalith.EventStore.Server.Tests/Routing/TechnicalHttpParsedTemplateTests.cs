using System.Reflection;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Syntax reuse never substitutes for current route values, constraints, configuration or final observation.</summary>
public sealed class TechnicalHttpParsedTemplateTests
{
    private static IServiceProvider Framework => (IServiceProvider)typeof(TechnicalHttpRouteTemplate)
        .GetField("FrameworkServices", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;

    [Theory]
    [InlineData("/owned/{id:int}", "owned/7", "GET")]
    [InlineData("/owned/{id:int}", "owned/bad", "POST")]
    [InlineData("/owned/{id:int?}", "owned", "GET")]
    [InlineData("/owned/{id:int=7}", "owned", "POST")]
    [InlineData("/owned/{id:int:min(3)}", "owned/7", "GET")]
    [InlineData("/owned/{id:int:min(3)}", "owned/1", "GET")]
    [InlineData("/owned/{id:guid}", "owned/00000000-0000-0000-0000-000000000001", "PUT")]
    [InlineData("/owned/{name:regex(^[a-z]+$)}", "owned/order", "GET")]
    [InlineData("/owned/{name:regex([)}", "owned/order", "GET")]
    [InlineData("/owned/{name:unknown}", "owned/order", "GET")]
    [InlineData("/owned/{*path}", "owned/child", "GET")]
    [InlineData("/owned/{**path}", "owned/child", "GET")]
    [InlineData("/owned/{id", "owned/7", "GET")]
    [InlineData("/owned/{id}/{id}", "owned/7/8", "GET")]
    [InlineData("/owned/{id?}/tail", "owned/tail", "GET")]
    [InlineData("//owned/{id}", "owned/7", "POST")]
    [InlineData("/owned/{name}.{ext?}", "owned/report.txt", "GET")]
    [InlineData("/owned/{name}.{ext?}", "owned/report", "GET")]
    [InlineData("/café/{name}", "café/naïve", "GET")]
    [InlineData("/owned/{id}", "owned/%2F", "GET")]
    [InlineData("/owned/{id}", "owned/7/extra", "GET")]
    [InlineData("/owned/{id}", "owned//7", "GET")]
    [InlineData(null, "owned", "GET")]
    [InlineData("", "owned", "GET")]
    [InlineData("/owned", null, "GET")]
    [InlineData("/owned", "", "GET")]
    public void ColdAndRepeatedSyntaxHaveExactOrdinaryResultsAndErrors(string? template, string? method, string verb)
    {
        string expected = Outcome(() => TechnicalHttpRouteTemplate.Matches(template!, method!, verb));
        var parsed = new TechnicalHttpParsedTemplate();
        for (int iteration = 0; iteration < 3; iteration++)
            Outcome(() => parsed.Matches(template!, method!, verb, Framework)).ShouldBe(expected);
    }

    [Fact]
    public void EveryWarmMatchResolvesAndExecutesCurrentConstraintsWithFreshValuesAndContext()
    {
        var events = new List<string>(); var contexts = new List<HttpContext>(); var dictionaries = new List<RouteValueDictionary>();
        bool allowed = true;
        var actual = Framework.GetRequiredService<IInlineConstraintResolver>();
        var resolver = Substitute.For<IInlineConstraintResolver>();
        resolver.ResolveConstraint(Arg.Any<string>()).Returns(call =>
        {
            string content = call.Arg<string>(); events.Add("resolve:" + content);
            var current = actual.ResolveConstraint(content).ShouldNotBeNull();
            var constraint = Substitute.For<IRouteConstraint>();
            constraint.Match(Arg.Any<HttpContext>(), Arg.Any<IRouter>(), Arg.Any<string>(), Arg.Any<RouteValueDictionary>(), Arg.Any<RouteDirection>()).Returns(match =>
            {
                var context = match.ArgAt<HttpContext>(0); var values = match.ArgAt<RouteValueDictionary>(3);
                contexts.ShouldNotContain(context); dictionaries.ShouldNotContain(values);
                context.Items.ShouldBeEmpty(); values.ContainsKey("poison").ShouldBeFalse();
                contexts.Add(context); dictionaries.Add(values);
                events.Add("match:" + context.Request.Method + ":" + values["id"]);
                bool result = current.Match(context, null, match.ArgAt<string>(2), values, match.ArgAt<RouteDirection>(4)) && allowed;
                context.Items["poison"] = true; values["poison"] = true; values["id"] = "changed-after-match";
                return result;
            });
            return constraint;
        });
        var services = Substitute.For<IServiceProvider>();
        services.GetService(typeof(IInlineConstraintResolver)).Returns(_ => { events.Add("service"); return resolver; });
        var parsed = new TechnicalHttpParsedTemplate();
        parsed.Matches("/owned/{id:int=7}", "owned", "GET", services).ShouldBeTrue();
        allowed = false;
        parsed.Matches("/owned/{id:int=7}", "owned", "GET", services).ShouldBeFalse();
        allowed = true;
        parsed.Matches("/owned/{id:int=7}", "owned/8", "POST", services).ShouldBeTrue();
        parsed.Matches("/owned/{id:int=7}", "owned", "POST", services).ShouldBeTrue();
        events.ShouldBe(["service", "resolve:int", "match:GET:7", "service", "resolve:int", "match:GET:7",
            "service", "resolve:int", "match:POST:8", "service", "resolve:int", "match:POST:7"]);
        parsed.Count.ShouldBe(1);
    }

    [Fact]
    public void BoundsCatchallAndPathFailureKeepOriginalResolverCheckpoints()
    {
        var services = Substitute.For<IServiceProvider>(); var parsed = new TechnicalHttpParsedTemplate();
        parsed.Matches(new string('a', 513), "owned", "GET", services).ShouldBeFalse();
        parsed.Matches("/owned", new string('a', 8193), "GET", services).ShouldBeFalse();
        parsed.Matches("/owned/{*path}", "owned/child", "GET", services).ShouldBeFalse();
        parsed.Matches("/owned/{id:int}", "different/7", "GET", services).ShouldBeFalse();
        Should.Throw<RoutePatternException>(() => parsed.Matches("/owned/{id", "owned/7", "GET", services));
        services.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public void MalformedPatternsNeverBecomeCachedErrorsOrReplaceSuccessfulSyntax()
    {
        var parsed = new TechnicalHttpParsedTemplate();
        parsed.Matches("/owned/{id:int}", "owned/7", "GET", Framework).ShouldBeTrue();
        var first = Should.Throw<RoutePatternException>(() => parsed.Matches("/owned/{id", "owned/7", "GET", Framework));
        var second = Should.Throw<RoutePatternException>(() => parsed.Matches("/owned/{id", "owned/7", "GET", Framework));
        first.ShouldNotBeSameAs(second);
        first.Message.ShouldBe(second.Message);
        Outcome(() => parsed.Matches("/owned/{id", "owned/7", "GET", Framework))
            .ShouldBe(Outcome(() => TechnicalHttpRouteTemplate.Matches("/owned/{id", "owned/7", "GET")));
        parsed.Matches("/owned/{id:int}", "owned/8", "GET", Framework).ShouldBeTrue();
        parsed.Count.ShouldBe(1);
    }

    [Fact]
    public void ManyVariantsAndForcedBudgetExhaustionPreserveBoundedFreshMatching()
    {
        var parsed = new TechnicalHttpParsedTemplate();
        for (int index = 0; index < 1000; index++)
        {
            string pattern = "/owned" + index + "/{id:int}";
            parsed.Matches(pattern, "owned" + index + "/7", "GET", Framework).ShouldBeTrue();
            parsed.Count.ShouldBe(1); parsed.RetainedBytes.ShouldBeLessThanOrEqualTo(1024 * 1024);
        }
        parsed.Matches("/owned0/{id:int}", "owned0/bad", "GET", Framework).ShouldBeFalse();
        string dense = "/owned/{id" + string.Concat(Enumerable.Repeat(":int", 100)) + "}";
        dense.Length.ShouldBeLessThanOrEqualTo(512);
        parsed.Matches(dense, "owned/7", "GET", Framework).ShouldBeTrue();
        parsed.RetainedBytes.ShouldBeLessThanOrEqualTo(1024 * 1024);
        string deep = "/" + string.Join('/', Enumerable.Repeat("a", 28));
        parsed.Matches(deep, deep.TrimStart('/'), "GET", Framework)
            .ShouldBe(TechnicalHttpRouteTemplate.Matches(deep, deep.TrimStart('/'), "GET"));
        // Fault-inject a full first-stage accounting reservation so the successful second
        // parser graph cannot fit. No production tuning seam or input limit is introduced.
        const string budget = "/budget/{id:int}";
        typeof(TechnicalHttpParsedTemplate).GetMethod("Pattern", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(parsed, [budget]);
        typeof(TechnicalHttpParsedTemplate).GetField("<RetainedBytes>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(parsed, 1024L * 1024);
        int resolutions = 0, executions = 0;
        var resolver = Substitute.For<IInlineConstraintResolver>();
        resolver.ResolveConstraint("int").Returns(_ =>
        {
            resolutions++;
            var current = Framework.GetRequiredService<IInlineConstraintResolver>().ResolveConstraint("int").ShouldNotBeNull();
            var constraint = Substitute.For<IRouteConstraint>();
            constraint.Match(Arg.Any<HttpContext>(), Arg.Any<IRouter>(), Arg.Any<string>(), Arg.Any<RouteValueDictionary>(), Arg.Any<RouteDirection>()).Returns(call =>
            {
                executions++;
                return current.Match(call.ArgAt<HttpContext>(0), null, call.ArgAt<string>(2), call.ArgAt<RouteValueDictionary>(3), call.ArgAt<RouteDirection>(4));
            });
            return constraint;
        });
        var services = Substitute.For<IServiceProvider>();
        services.GetService(typeof(IInlineConstraintResolver)).Returns(resolver);
        parsed.Matches(budget, "budget/7", "GET", services)
            .ShouldBe(TechnicalHttpRouteTemplate.Matches(budget, "budget/7", "GET"));
        parsed.Count.ShouldBe(0); parsed.RetainedBytes.ShouldBe(0);
        parsed.Matches(budget, "budget/bad", "GET", services)
            .ShouldBe(TechnicalHttpRouteTemplate.Matches(budget, "budget/bad", "GET"));
        resolutions.ShouldBe(2); executions.ShouldBe(2);
        parsed.Matches("/restored/{id:int}", "restored/7", "GET", Framework).ShouldBeTrue();
        parsed.Count.ShouldBe(1);
    }

    [Fact]
    public void CompleteStillRunsCurrentCallbacksAfterWarmSyntaxAndIndependentPassesStayIndependent()
    {
        using var services = Services();
        var first = new TechnicalHttpObservationPass(services); var second = new TechnicalHttpObservationPass(services);
        int calls = 0; string current = "original";
        first.RequireUnchanged(() => { calls++; return current; }, "original");
        for (int iteration = 0; iteration < 3; iteration++) first.MatchesTemplate("/owned/{id:int}", "owned/7", "GET").ShouldBeTrue();
        second.MatchesTemplate("/owned/{id:int}", "owned/bad", "POST").ShouldBeFalse();
        first.Complete(); calls.ShouldBe(1);
        current = "changed";
        first.MatchesTemplate("/owned/{id:int}", "owned/8", "POST").ShouldBeTrue();
        Should.Throw<RouteCatalogUnavailableException>(first.Complete).Message.ShouldBe("Native SDK runtime inventory changed during observation.");
        calls.ShouldBe(2); second.Complete();
    }

    [Fact]
    public void ActualConcreteGrantsReadsEachCurrentConfigurationAndDerivesCurrentVerbs()
    {
        const string key = "EventStore:RouteCatalog:DevelopmentBootstrap:DeclaredInvocations";
        string Json(string method, string verb) => JsonSerializer.Serialize(new[] { new RouteCatalogDeclaredInvocation("caller", method, [verb]) });
        string[] texts = [Json("owned/7", "GET"), Json("owned/bad", "GET"), Json("owned/8", "POST")];
        var configuration = Substitute.For<IConfiguration>(); int reads = 0;
        configuration[key].Returns(_ => texts[Math.Min(reads++, 2)]);
        using var services = Services(configuration);
        var receiver = new TechnicalHttpRouteCatalogReceiver();
        typeof(TechnicalHttpRouteCatalogReceiver).GetField("_services", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(receiver, services);
        var grants = typeof(TechnicalHttpRouteCatalogReceiver).GetMethod("ConcreteGrants", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Func<RouteCatalogTechnicalHttpEndpoint, TechnicalHttpObservationPass, IReadOnlyList<RouteCatalogHttpGrant>>>(receiver);
        var pass = new TechnicalHttpObservationPass(services); var endpoint = Endpoint("/owned/{id:int}");
        grants(endpoint, pass).ShouldBe([new RouteCatalogHttpGrant("owned/7", "GET")]);
        grants(endpoint, pass).ShouldBeEmpty();
        grants(endpoint, pass).ShouldBe([new RouteCatalogHttpGrant("owned/8", "POST")]);
        reads.ShouldBe(3); pass.Complete();
        // Empty JSON never eagerly parses an unused malformed template; malformed JSON still fails first.
        configuration[key].Returns("[]");
        grants(Endpoint("/owned/{invalid"), new(services)).ShouldBeEmpty();
        configuration[key].Returns("[malformed");
        Should.Throw<JsonException>(() => grants(Endpoint("/owned/{invalid"), new(services)));
        configuration[key].Returns(Json("owned/7", "GET"));
        Should.Throw<RoutePatternException>(() => grants(Endpoint("/owned/{invalid"), new(services)));
    }

    private static RouteCatalogTechnicalHttpEndpoint Endpoint(string pattern) => new(pattern, ["GET", "POST"], "", "", "", [], "", "");
    private static ServiceProvider Services(IConfiguration? configuration = null)
    {
        var services = new ServiceCollection().AddSingleton<IOptions<JsonOptions>>(Options.Create(new JsonOptions()));
        if (configuration is not null) services.AddSingleton(configuration);
        return services.BuildServiceProvider();
    }
    private static string Outcome(Func<bool> action)
    {
        try { return "result:" + action(); }
        catch (Exception failure)
        {
            return failure.GetType().FullName + "|" + failure.Message + "|" + (failure as ArgumentException)?.ParamName
                + "|inner:" + failure.InnerException?.GetType().FullName + ":" + failure.InnerException?.Message;
        }
    }
}
