using System.Collections.Concurrent;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.TypeCatalog;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Commands;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Services;

/// <summary>
/// Resolves command aggregate types from the EventStore-owned admin type catalog.
/// </summary>
public sealed class DaprCommandAggregateTypeResolver(
    DaprClient daprClient,
    IOptions<CommandStatusOptions> commandStatusOptions,
    ILogger<DaprCommandAggregateTypeResolver> logger) : ICommandAggregateTypeResolver {
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _stateStoreName = commandStatusOptions.Value.StateStoreName;

    /// <inheritdoc/>
    public async Task<string?> ResolveAsync(CommandEnvelope command, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Domain) || string.IsNullOrWhiteSpace(command.CommandType)) {
            return null;
        }

        IReadOnlyList<CommandTypeInfo> commands = await GetCommandsAsync(command.Domain, cancellationToken)
            .ConfigureAwait(false);
        CommandTypeInfo? match = commands.FirstOrDefault(c => IsCommandMatch(c, command.CommandType));
        return string.IsNullOrWhiteSpace(match?.TargetAggregateType)
            ? null
            : match.TargetAggregateType.Trim();
    }

    private async Task<IReadOnlyList<CommandTypeInfo>> GetCommandsAsync(string domain, CancellationToken cancellationToken) {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (_cache.TryGetValue(domain, out CacheEntry cached) && cached.ExpiresAtUtc > now) {
            return cached.Commands;
        }

        try {
            List<CommandTypeInfo>? commands = await daprClient
                .GetStateAsync<List<CommandTypeInfo>>(
                    _stateStoreName,
                    $"admin:type-catalog:commands:{domain}",
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            IReadOnlyList<CommandTypeInfo> result = commands ?? [];
            _cache[domain] = new CacheEntry(result, now.Add(CacheTtl));
            return result;
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            logger.LogWarning(
                ex,
                "Command aggregate type catalog lookup failed; domain fallback will be used: Domain={Domain}",
                domain);
            return [];
        }
    }

    private static bool IsCommandMatch(CommandTypeInfo commandTypeInfo, string commandType)
        => string.Equals(commandTypeInfo.TypeName, commandType, StringComparison.Ordinal)
        || string.Equals(SimpleName(commandTypeInfo.TypeName), SimpleName(commandType), StringComparison.Ordinal);

    private static string SimpleName(string typeName) {
        int index = typeName.LastIndexOf('.');
        return index >= 0 ? typeName[(index + 1)..] : typeName;
    }

    private sealed record CacheEntry(IReadOnlyList<CommandTypeInfo> Commands, DateTimeOffset ExpiresAtUtc);
}
