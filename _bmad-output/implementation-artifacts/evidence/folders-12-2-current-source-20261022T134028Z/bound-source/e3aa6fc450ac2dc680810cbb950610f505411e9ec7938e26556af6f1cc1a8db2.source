using Hexalith.EventStore.Client.Conventions;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.DomainService;

internal static class DomainProjectionHandlerRouteValidator {
    /// <summary>Materializes and validates unchanged legacy domain-only handlers.</summary>
    /// <param name="projectionHandlers">The legacy handlers.</param>
    /// <returns>The materialized handlers.</returns>
    public static IDomainProjectionHandler[] MaterializeAndValidate(IEnumerable<IDomainProjectionHandler>? projectionHandlers) {
        IDomainProjectionHandler[] handlers = projectionHandlers is null ? [] : [.. projectionHandlers];
        ThrowIfDuplicateRoutes(handlers);
        return handlers;
    }

    /// <summary>Validates unchanged legacy domain-only handler uniqueness.</summary>
    /// <param name="handlers">The legacy handlers.</param>
    public static void ThrowIfDuplicateRoutes(IReadOnlyList<IDomainProjectionHandler> handlers) {
        ArgumentNullException.ThrowIfNull(handlers);

        for (int i = 0; i < handlers.Count; i++) {
            IDomainProjectionHandler candidate = handlers[i];
            List<IDomainProjectionHandler>? duplicates = null;

            for (int j = i + 1; j < handlers.Count; j++) {
                IDomainProjectionHandler other = handlers[j];
                if (!string.Equals(candidate.Domain, other.Domain, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                duplicates ??= [candidate];
                duplicates.Add(other);
            }

            if (duplicates is null) {
                continue;
            }

            string handlerTypes = string.Join(
                ", ",
                duplicates
                    .Select(h => h.GetType().FullName ?? h.GetType().Name)
                    .Order(StringComparer.Ordinal));

            throw new InvalidOperationException(
                $"Duplicate projection handlers are registered for domain '{candidate.Domain}': {handlerTypes}");
        }
    }

    /// <summary>
    /// Materializes named handlers, validates canonical pair ownership and domain limits, and returns
    /// deterministic projection-type ordinal order.
    /// </summary>
    /// <param name="projectionHandlers">The named asynchronous handlers.</param>
    /// <param name="options">The validated dispatch limits.</param>
    /// <returns>The canonical deterministically ordered handlers.</returns>
    public static IAsyncDomainProjectionHandler[] MaterializeAndValidateNamed(
        IEnumerable<IAsyncDomainProjectionHandler>? projectionHandlers,
        ProjectionDispatchOptions options) {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        IAsyncDomainProjectionHandler[] handlers = projectionHandlers is null ? [] : [.. projectionHandlers];
        foreach (IAsyncDomainProjectionHandler handler in handlers) {
            ArgumentNullException.ThrowIfNull(handler);
            NamingConventionEngine.ValidateKebabCase(handler.Domain, nameof(handler.Domain));
            NamingConventionEngine.ValidateKebabCase(handler.ProjectionType, nameof(handler.ProjectionType));
        }

        IGrouping<ProjectionDispatchRoute, IAsyncDomainProjectionHandler>? duplicate = handlers
            .GroupBy(
                static handler => new ProjectionDispatchRoute(handler.Domain, handler.ProjectionType))
            .Where(static group => group.Skip(1).Any())
            .OrderBy(static group => group.Key.Domain, StringComparer.Ordinal)
            .ThenBy(static group => group.Key.ProjectionType, StringComparer.Ordinal)
            .FirstOrDefault();
        if (duplicate is not null) {
            string handlerTypes = string.Join(
                ", ",
                duplicate
                    .Select(static handler => handler.GetType().FullName ?? handler.GetType().Name)
                    .Order(StringComparer.Ordinal));
            throw new InvalidOperationException(
                $"{ProjectionDispatchReasonCodes.DuplicateRoute}: duplicate named projection route "
                + $"'{duplicate.Key.Domain}/{duplicate.Key.ProjectionType}': {handlerTypes}");
        }

        IGrouping<string, IAsyncDomainProjectionHandler>? overLimit = handlers
            .GroupBy(static handler => handler.Domain, StringComparer.Ordinal)
            .Where(group => group.Count() > options.MaxHandlersPerDomain)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .FirstOrDefault();
        if (overLimit is not null) {
            throw new InvalidOperationException(
                $"Named projection domain '{overLimit.Key}' registers {overLimit.Count()} handlers; "
                + $"the configured maximum {options.MaxHandlersPerDomain} was exceeded.");
        }

        return [.. handlers
            .OrderBy(static handler => handler.ProjectionType, StringComparer.Ordinal)
            .ThenBy(static handler => handler.Domain, StringComparer.Ordinal)];
    }
}
