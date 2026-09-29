using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Installs <see cref="ReminderCallbackTokenFilter"/> at the front of the pipeline whenever reminders are
/// registered, so the reminder actor routes are guarded even when the host maps the Dapr actor handlers itself.
/// </summary>
internal sealed class ReminderCallbackTokenStartupFilter : IStartupFilter
{
    /// <inheritdoc/>
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        return app =>
        {
            _ = app.UseMiddleware<ReminderCallbackTokenFilter>();
            next(app);
        };
    }
}
