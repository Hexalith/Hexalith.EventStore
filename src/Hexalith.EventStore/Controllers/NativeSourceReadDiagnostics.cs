using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Controllers;

/// <summary>Observes the original native inspection task only in the explicitly selected private source proof.</summary>
internal static class NativeSourceReadDiagnostics
{
    /// <summary>Attaches metadata-only diagnostics without replacing, repeating or accepting the observed task.</summary>
    /// <param name="inspection">The one original actor inspection.</param>
    /// <param name="identity">The workload-validated source identity.</param>
    /// <param name="recheck">Whether this is the existing second inspection.</param>
    /// <param name="configuration">The actual host configuration.</param>
    /// <param name="options">The effective SDK host options, including configured Development selection.</param>
    /// <param name="environment">The actual hosting environment.</param>
    /// <param name="logger">The actual host logger.</param>
    /// <param name="cancellationToken">The original request cancellation.</param>
    /// <param name="protectedRead">The protection decision captured from the actual request before context suppression.</param>
    /// <returns>The unchanged original task.</returns>
    internal static Task<SourceProofInspection> Observe(Task<SourceProofInspection> inspection, AggregateIdentity identity,
        bool recheck, IConfiguration? configuration, IOptions<RouteCatalogHostOptions>? options,
        IHostEnvironment? environment, ILogger logger, CancellationToken cancellationToken, bool protectedRead = false)
    {
        // Capture this decision in the request before any detached completion can lose its ExecutionContext.
        bool omitDiagnostics = protectedRead;
        try
        {
            if (omitDiagnostics || !Enabled(configuration, options?.Value, environment)) return inspection;
            string tenant = Hash(identity.TenantId), domain = Hash(identity.Domain), aggregate = Hash(identity.AggregateId);
            string phase = recheck ? "recheck" : "initial";
            long started = Stopwatch.GetTimestamp();
            logger.LogInformation("Private native inspection started: TenantHash={TenantHash}, DomainHash={DomainHash}, AggregateHash={AggregateHash}, Phase={Phase}, ScopedDecodeReuseConfigured={ScopedDecodeReuseConfigured}, SerializeLocalLeaseReleaseConfigured={SerializeLocalLeaseReleaseConfigured}, RetainValidatedCatalogBytesConfigured={RetainValidatedCatalogBytesConfigured}",
                tenant, domain, aggregate, phase, options?.Value.ScopedDecodeReuse == true, options?.Value.SerializeLocalLeaseRelease == true, options?.Value.RetainValidatedCatalogBytes == true);
            void Attach() => _ = inspection.ContinueWith(completed =>
            {
                try
                {
                    SourceProofInspection? result = completed.IsCompletedSuccessfully ? completed.Result : null;
                    string status = result?.Status switch
                    {
                        SourceProofInspectionStatus.Available => "available",
                        SourceProofInspectionStatus.Unsupported => "unsupported",
                        SourceProofInspectionStatus.Invalid => "invalid",
                        _ => completed.IsCanceled ? "canceled" : completed.IsFaulted ? "faulted" : "unknown",
                    };
                    _ = completed.Exception;
                    logger.LogInformation("Private native inspection completed: TenantHash={TenantHash}, DomainHash={DomainHash}, AggregateHash={AggregateHash}, Phase={Phase}, Status={Status}, Exists={Exists}, CurrentSequence={CurrentSequence}, Erased={Erased}, ProofPresent={ProofPresent}, ElapsedMilliseconds={ElapsedMilliseconds}, RequestCanceled={RequestCanceled}, LateAuthorityAccepted=false",
                        tenant, domain, aggregate, phase, status, result?.Exists, result?.CurrentSequence, result?.Erased,
                        result?.Proof is not null, Stopwatch.GetElapsedTime(started).TotalMilliseconds, cancellationToken.IsCancellationRequested);
                }
                catch (Exception) { /* Diagnostics cannot alter the original inspection or its authority decision. */ }
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            if (ExecutionContext.IsFlowSuppressed()) Attach();
            else
            {
                using (ExecutionContext.SuppressFlow()) Attach();
            }
        }
        catch (Exception) { /* Diagnostic setup cannot replace the original task or authority result. */ }
        return inspection;
    }

    /// <summary>Observes the unchanged atomic-pair task, recording its total time without inventing per-inspection timings.</summary>
    /// <param name="inspection">The one original actor operation containing two real metadata inspections.</param>
    /// <param name="identity">The workload-validated source identity.</param>
    /// <param name="configuration">The actual host configuration.</param>
    /// <param name="options">The effective SDK host options.</param>
    /// <param name="environment">The actual hosting environment.</param>
    /// <param name="logger">The actual host logger.</param>
    /// <param name="cancellationToken">The original request cancellation.</param>
    /// <param name="protectedRead">The protection decision captured from the actual request before context suppression.</param>
    /// <returns>The unchanged original task.</returns>
    internal static Task<SourceProofInspectionPair> ObservePair(Task<SourceProofInspectionPair> inspection, AggregateIdentity identity,
        IConfiguration? configuration, IOptions<RouteCatalogHostOptions>? options, IHostEnvironment? environment,
        ILogger logger, CancellationToken cancellationToken, bool protectedRead = false)
    {
        // Capture this decision in the request before any detached completion can lose its ExecutionContext.
        bool omitDiagnostics = protectedRead;
        try
        {
            if (omitDiagnostics || !Enabled(configuration, options?.Value, environment)) return inspection;
            string tenant = Hash(identity.TenantId), domain = Hash(identity.Domain), aggregate = Hash(identity.AggregateId);
            long started = Stopwatch.GetTimestamp();
            logger.LogInformation("Private native metadata pair started: TenantHash={TenantHash}, DomainHash={DomainHash}, AggregateHash={AggregateHash}, ScopedDecodeReuseConfigured={ScopedDecodeReuseConfigured}, SerializeLocalLeaseReleaseConfigured={SerializeLocalLeaseReleaseConfigured}, RetainValidatedCatalogBytesConfigured={RetainValidatedCatalogBytesConfigured}",
                tenant, domain, aggregate, options?.Value.ScopedDecodeReuse == true, options?.Value.SerializeLocalLeaseRelease == true, options?.Value.RetainValidatedCatalogBytes == true);
            void Attach() => _ = inspection.ContinueWith(completed =>
            {
                try
                {
                    SourceProofInspectionPair? pair = completed.IsCompletedSuccessfully ? completed.Result : null;
                    _ = completed.Exception;
                    logger.LogInformation("Private native metadata pair completed: TenantHash={TenantHash}, DomainHash={DomainHash}, AggregateHash={AggregateHash}, InitialStatus={InitialStatus}, FinalStatus={FinalStatus}, InitialSequence={InitialSequence}, FinalSequence={FinalSequence}, ObservationsEqual={ObservationsEqual}, TotalElapsedMilliseconds={TotalElapsedMilliseconds}, TaskCanceled={TaskCanceled}, TaskFaulted={TaskFaulted}, RequestCanceled={RequestCanceled}, LateAuthorityAccepted=false",
                        tenant, domain, aggregate, Status(pair?.Initial), Status(pair?.Final), pair?.Initial?.CurrentSequence,
                        pair?.Final?.CurrentSequence, pair is not null && pair.Initial == pair.Final,
                        Stopwatch.GetElapsedTime(started).TotalMilliseconds, completed.IsCanceled, completed.IsFaulted,
                        cancellationToken.IsCancellationRequested);
                }
                catch (Exception) { /* Diagnostics cannot alter the original task or its authority decision. */ }
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            if (ExecutionContext.IsFlowSuppressed()) Attach();
            else
            {
                using (ExecutionContext.SuppressFlow()) Attach();
            }
        }
        catch (Exception) { /* Diagnostic setup cannot replace the original task or authority result. */ }
        return inspection;
    }

    private static string Status(SourceProofInspection? inspection) => inspection?.Status switch
    {
        SourceProofInspectionStatus.Available => "available",
        SourceProofInspectionStatus.Unsupported => "unsupported",
        SourceProofInspectionStatus.Invalid => "invalid",
        _ => "unknown",
    };

    private static bool Enabled(IConfiguration? configuration, RouteCatalogHostOptions? options, IHostEnvironment? environment)
    {
#if DEBUG
        return configuration?.GetValue<bool>("EventStore:Testing:NativeSourceReadDiagnostics:Enabled") == true
            && environment?.IsDevelopment() == true
            && options is { DevelopmentFixture: true, Profile: "Development", RequireTechnicalHttpBinding: true };
#else
        return false;
#endif
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
