using System.Diagnostics.CodeAnalysis;

using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Connects generated actions to hosting validation using their already-resolved partition tenant.</summary>
public static class RouteCatalogGeneratedAction
{
    /// <summary>Gets the hosting-owned validation callback key for one generated action execution.</summary>
    public const string ValidatorKey = "Hexalith.EventStore.RouteCatalog.ResolvedTenantValidator";

    /// <summary>
    /// Gets the hosting-owned key under which hosting validation publishes the validated command declaration's
    /// <c>MessageId</c> grammar version for one generated action execution.
    /// </summary>
    public const string MessageIdVersionKey = "Hexalith.EventStore.RouteCatalog.MessageIdVersion";

    /// <summary>Validates a generated action before dispatch; missing hosting validation refuses dispatch.</summary>
    public static bool ValidateTenant(IDictionary<object, object?> executionContext, Type contractType, string kind, string tenant)
    {
        ArgumentNullException.ThrowIfNull(executionContext);
        ArgumentNullException.ThrowIfNull(contractType);
        return executionContext.TryGetValue(ValidatorKey, out object? value)
            && value is Func<Type, string, string, bool> validate && validate(contractType, kind, tenant);
    }

    /// <summary>
    /// Selects the accepted response <c>MessageId</c> as command-status identity. A correlation identifier is never
    /// considered, and no request input can select the grammar version.
    /// </summary>
    /// <param name="executionContext">The generated action's execution items, where hosting validation published the version.</param>
    /// <param name="messageId">The accepted response <c>MessageId</c>.</param>
    /// <param name="statusMessageId">The unchanged <c>MessageId</c> when it is selected; otherwise <see langword="null"/>.</param>
    /// <returns>
    /// Whether hosting published the <see cref="int"/> version 1 or 2 and <paramref name="messageId"/> is valid under it.
    /// </returns>
    public static bool TrySelectStatusMessageId(
        IDictionary<object, object?> executionContext,
        string? messageId,
        [NotNullWhen(true)] out string? statusMessageId)
    {
        ArgumentNullException.ThrowIfNull(executionContext);
        statusMessageId = null;
        if (!executionContext.TryGetValue(MessageIdVersionKey, out object? value)
            || value is not int version
            || version is not (1 or 2)
            || !MessageIdGrammar.IsValid(messageId, version))
        {
            return false;
        }

        statusMessageId = messageId;
        return true;
    }
}
