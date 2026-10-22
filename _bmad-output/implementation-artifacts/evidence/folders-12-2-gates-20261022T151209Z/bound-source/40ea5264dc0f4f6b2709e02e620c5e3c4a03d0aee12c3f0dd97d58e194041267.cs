using System.Globalization;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Counts process-boundary requests without retaining request content or identifiers.</summary>
internal sealed class Oq8BoundaryCounterStartupFilter : IStartupFilter
{
    /// <summary>Names the environment variable containing the counter file.</summary>
    public const string CounterFileEnvironmentVariable = "HEXALITH_OQ8_BOUNDARY_COUNTER_FILE";

    private readonly string? _counterFile = Environment.GetEnvironmentVariable(CounterFileEnvironmentVariable);
    private readonly object _counterLock = new();
    private long _count;

    /// <inheritdoc/>
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        ArgumentNullException.ThrowIfNull(next);

        return app =>
        {
            if (!string.IsNullOrWhiteSpace(_counterFile))
            {
                app.Use(async (context, following) =>
                {
                    if (IsCountedBoundary(context.Request.Path))
                    {
                        lock (_counterLock)
                        {
                            long count = ++_count;
                            string temporary = _counterFile + ".tmp";
                            File.WriteAllText(temporary, count.ToString(CultureInfo.InvariantCulture));
                            File.Move(temporary, _counterFile, overwrite: true);
                        }
                    }

                    await following(context).ConfigureAwait(false);
                });
            }

            next(app);
        };
    }

    private static bool IsCountedBoundary(PathString path)
        => path.Equals("/process") || path.Equals("/api/v1/commands");
}
