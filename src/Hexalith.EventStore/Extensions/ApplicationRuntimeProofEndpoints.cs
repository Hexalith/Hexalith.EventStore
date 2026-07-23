using System.Security.Cryptography;
using System.Text;

namespace Hexalith.EventStore;

/// <summary>
/// Development-only process controls used by isolated runtime-proof harnesses.
/// </summary>
public static class ApplicationRuntimeProofEndpoints {
    private const string ConfigurationKey = "EventStore:RuntimeProof:ShutdownToken";
    private const string ShutdownTokenHeader = "X-Hexalith-Runtime-Proof-Token";
    private const int MinimumTokenLength = 32;

    /// <summary>
    /// Maps the authenticated shutdown control only when Development explicitly supplies
    /// a sufficiently strong, ephemeral proof token.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application.</returns>
    public static WebApplication MapApplicationRuntimeProofEndpoints(this WebApplication app) {
        ArgumentNullException.ThrowIfNull(app);

        string? expectedToken = app.Configuration[ConfigurationKey];
        if (!app.Environment.IsDevelopment()
            || string.IsNullOrWhiteSpace(expectedToken)
            || expectedToken.Length < MinimumTokenLength) {
            return app;
        }

        _ = app.MapPost(
            "/_test/runtime-proof/shutdown",
            (HttpContext context, IHostApplicationLifetime applicationLifetime) => {
                string suppliedToken = context.Request.Headers[ShutdownTokenHeader].ToString();
                if (!TokensMatch(expectedToken, suppliedToken)) {
                    return Results.NotFound();
                }

                context.Response.OnCompleted(() => {
                    applicationLifetime.StopApplication();
                    return Task.CompletedTask;
                });
                return Results.Accepted();
            });

        return app;
    }

    private static bool TokensMatch(string expectedToken, string suppliedToken) {
        if (expectedToken.Length != suppliedToken.Length) {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedToken),
            Encoding.UTF8.GetBytes(suppliedToken));
    }
}
