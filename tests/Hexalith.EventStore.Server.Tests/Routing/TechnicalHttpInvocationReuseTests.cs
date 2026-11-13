using System.Diagnostics;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Checks exact-text parsing reuse while preserving each configuration read and normal grant derivation.</summary>
public sealed class TechnicalHttpInvocationReuseTests(ITestOutputHelper output)
{
    private const string Key = "EventStore:RouteCatalog:DevelopmentBootstrap:DeclaredInvocations";

    /// <summary>Only the last exact successfully parsed text is reused; null, changed and malformed values remain current.</summary>
    [Fact]
    public void ExactTextReuseIsBoundedToOneEntryAndOneObservation()
    {
        using var services = Services();
        var pass = new TechnicalHttpObservationPass(services);
        string firstText = JsonSerializer.Serialize(new[] { new RouteCatalogDeclaredInvocation("caller", "owned/one", ["GET"]) });
        string otherText = JsonSerializer.Serialize(new[] { new RouteCatalogDeclaredInvocation("caller", "owned/two", ["POST"]) });
        var first = pass.DeclaredInvocations(firstText);
        pass.DeclaredInvocations(new string(firstText.ToCharArray())).ShouldBeSameAs(first);
        var other = pass.DeclaredInvocations(otherText);
        other[0].Method.ShouldBe("owned/two");
        other.ShouldNotBeSameAs(first);
        var restored = pass.DeclaredInvocations(firstText);
        restored.ShouldNotBeSameAs(first);
        JsonSerializer.Serialize(restored).ShouldBe(JsonSerializer.Serialize(first));
        Should.Throw<JsonException>(() => pass.DeclaredInvocations("[malformed"));
        pass.DeclaredInvocations(firstText).ShouldBeSameAs(restored);
        pass.DeclaredInvocations(null).ShouldBeEmpty();
        pass.DeclaredInvocations(firstText).ShouldNotBeSameAs(restored);
        new TechnicalHttpObservationPass(services).DeclaredInvocations(firstText).ShouldNotBeSameAs(first);
        for (int index = 0; index < 1000; index++)
            pass.DeclaredInvocations(otherText.Replace("owned/two", "owned/" + index, StringComparison.Ordinal));
        // Returning to old text reparses it: no dictionary of prior variants is retained.
        pass.DeclaredInvocations(firstText).ShouldNotBeSameAs(first);
    }

    /// <summary>A real installed receiver reads configuration for every technical action, including drift within one Observe call.</summary>
    [Fact]
    public async Task RealObservationUsesEachFreshConfigurationTextForItsOwnGrants()
    {
        await using var fixture = new TechnicalHttpCatalogFixture();
        Func<string?>? read = null;
        IConfiguration original = null!;
        await fixture.StartAsync(TestContext.Current.CancellationToken, services: services =>
        {
            var configuration = Substitute.For<IConfiguration>();
            configuration[Arg.Any<string>()].Returns(call => call.Arg<string>() == Key ? read?.Invoke() ?? original[Key] : original[call.Arg<string>()]);
            configuration.GetSection(Arg.Any<string>()).Returns(call => original.GetSection(call.Arg<string>()));
            configuration.GetChildren().Returns(_ => original.GetChildren());
            configuration.GetReloadToken().Returns(_ => original.GetReloadToken());
            services.AddSingleton(configuration);
        }, observeConfiguration: configuration => original = configuration);
        var receiver = fixture.App.Services.GetRequiredService<TechnicalHttpRouteCatalogReceiver>();
        var initial = receiver.Observe();
        string populated = JsonSerializer.Serialize(initial.TechnicalHttp!
            .Where(endpoint => !endpoint.Pattern.Contains('{', StringComparison.Ordinal))
            .GroupBy(endpoint => endpoint.Pattern)
            .Select(group => new RouteCatalogDeclaredInvocation("caller", group.Key.Trim('/'), group.SelectMany(endpoint => endpoint.Methods).Distinct().ToArray())).ToArray());
        var installed = ((IEndpointRouteBuilder)fixture.App).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()
            .Select(endpoint => TechnicalHttpActionObservation.Pattern(endpoint) + "|" + string.Join(',',
                (endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? []).Order(StringComparer.Ordinal)))
            .Where(key => initial.TechnicalHttp.Any(endpoint => TechnicalHttpCatalogBinding.Key(endpoint) == key)).ToArray();
        var texts = new List<string>();
        read = () =>
        {
            string current = texts.Count % 2 == 0 ? populated : "[]";
            texts.Add(current);
            return current;
        };

        var observed = receiver.Observe();

        var technical = observed.Endpoints.Where(endpoint => endpoint.TechnicalGrants is not null).ToArray();
        texts.Count.ShouldBe(technical.Length);
        texts.Count.ShouldBe(initial.TechnicalHttp.Count);
        for (int index = 0; index < technical.Length; index++)
        {
            string key = technical[index].Pattern + "|" + string.Join(',', technical[index].Methods);
            int readIndex = Array.IndexOf(installed, key);
            readIndex.ShouldBeGreaterThanOrEqualTo(0);
            bool expectedGrant = texts[readIndex] == populated && !technical[index].Pattern.Contains('{', StringComparison.Ordinal);
            technical[index].TechnicalGrants!.Any().ShouldBe(expectedGrant);
        }
        read = () => "[malformed";
        Should.Throw<JsonException>(() => receiver.Observe());
    }

    /// <summary>Records parsing cost with identical realistic text; each measurement includes the exact-text comparison.</summary>
    [Fact]
    public void MeasurePerObservationParsingBenefit()
    {
        using var services = Services();
        string json = JsonSerializer.Serialize(Enumerable.Range(0, 128)
            .Select(index => new RouteCatalogDeclaredInvocation("caller", "api/owned/" + index, ["GET", "POST"])).ToArray());
        var pass = new TechnicalHttpObservationPass(services);
        for (int warmup = 0; warmup < 20; warmup++) pass.DeclaredInvocations(json);
        (double elapsed, long bytes) Measure(Action action)
        {
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            long started = Stopwatch.GetTimestamp();
            for (int iteration = 0; iteration < 500; iteration++) action();
            return (Stopwatch.GetElapsedTime(started).TotalMilliseconds / 500,
                (GC.GetAllocatedBytesForCurrentThread() - allocated) / 500);
        }
        var plain = Measure(() => JsonSerializer.Deserialize<RouteCatalogDeclaredInvocation[]>(json));
        var reused = Measure(() => pass.DeclaredInvocations(json));
        output.WriteLine(JsonSerializer.Serialize(new { configurationCharacters = json.Length, baselineMs = plain.elapsed,
            exactTextReuseMs = reused.elapsed, baselineAllocatedBytes = plain.bytes, exactTextReuseAllocatedBytes = reused.bytes }));
    }

    private static ServiceProvider Services() => new ServiceCollection()
        .AddSingleton<IOptions<JsonOptions>>(Options.Create(new JsonOptions())).BuildServiceProvider();
}
