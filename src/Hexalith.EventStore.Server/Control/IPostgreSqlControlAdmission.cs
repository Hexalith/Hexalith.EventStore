using Npgsql;

namespace Hexalith.EventStore.Server.Control;

/// <summary>Required external family/owner and pinned-backend authority for the unregistered SQL kernel.</summary>
/// <remarks>No production implementation is registered; a test implementation cannot grant readiness.</remarks>
internal interface IPostgreSqlControlAdmission
{
    ValueTask RequireBackendAsync(NpgsqlConnection connection, CancellationToken cancellationToken);

    ValueTask RequireMutationAsync(PostgreSqlControlMutation mutation, CancellationToken cancellationToken);
}
