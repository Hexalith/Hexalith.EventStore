using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

public class Oq8BoundaryCounterStartupFilterTests
{
    [Fact]
    public async Task ReceiverReplacementRetainsPersistedCountAndIgnoresNonBoundaryRequests()
    {
        string scratch = Path.Combine(Path.GetTempPath(), "oq8-counter-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        string file = Path.Combine(scratch, "count.txt");
        try
        {
            await File.WriteAllTextAsync(file, "41");
            using var services = new ServiceCollection().BuildServiceProvider();
            for (int replacement = 0; replacement < 2; replacement++)
            {
                var app = new ApplicationBuilder(services);
                new Oq8BoundaryCounterStartupFilter(file).Configure(builder => builder.Run(_ => Task.CompletedTask))(app);
                RequestDelegate pipeline = app.Build();
                var health = new DefaultHttpContext();
                health.Request.Path = "/alive";
                await pipeline(health);
                (await File.ReadAllTextAsync(file)).ShouldBe((41 + replacement).ToString(System.Globalization.CultureInfo.InvariantCulture));
                var process = new DefaultHttpContext();
                process.Request.Path = "/process";
                await pipeline(process);
                (await File.ReadAllTextAsync(file)).ShouldBe((42 + replacement).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }
        finally { Directory.Delete(scratch, recursive: true); }
    }
}
