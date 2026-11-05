using System.Diagnostics;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using OpenTelemetry;

namespace Hexalith.EventStore.ServiceDefaults;

/// <summary>Omits protected read diagnostics before the application's authentication and exception middleware.</summary>
/// <param name="loggers">The real host logger factory.</param>
internal sealed class ReadDiagnosticsPrivacyStartupFilter(ILoggerFactory loggers) : IStartupFilter
{
    /// <inheritdoc/>
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        return builder =>
        {
            builder.Use(async (context, following) =>
            {
                if (!ReadDiagnosticsPrivacy.IsProtectedExecution(context))
                {
                    await following(context).ConfigureAwait(false);
                    return;
                }

                ReadDiagnosticsPrivacy.MarkProtectedRequest(context);
                using IDisposable privacy = ReadDiagnosticsPrivacyScope.Begin();
                // Standard unsampled context also suppresses native Dapr child spans; no privacy wire field is introduced.
                if (Activity.Current is { } request)
                {
                    request.IsAllDataRequested = false;
                    request.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
                }
                if (ReadDiagnosticsPrivacy.IsPublicQuery(context))
                {
                    ReadQueryDenialTiming.Register(context);
                    context.Response.OnCompleted(() => ReadBoundaryDiagnostics.CompleteAsync(context, loggers));
                }

                using IDisposable suppression = SuppressInstrumentationScope.Begin();
                await following(context).ConfigureAwait(false);
            });
            next(builder);
        };
    }
}
