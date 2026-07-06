namespace Hexalith.EventStore.DomainService;

internal static class DomainQueryHandlerRouteValidator {
    public static IDomainQueryHandler[] MaterializeAndValidate(IEnumerable<IDomainQueryHandler>? queryHandlers) {
        IDomainQueryHandler[] handlers = queryHandlers is null ? [] : [.. queryHandlers];
        ThrowIfDuplicateRoutes(handlers);
        return handlers;
    }

    public static void ThrowIfDuplicateRoutes(IReadOnlyList<IDomainQueryHandler> handlers) {
        ArgumentNullException.ThrowIfNull(handlers);

        for (int i = 0; i < handlers.Count; i++) {
            IDomainQueryHandler candidate = handlers[i];
            List<IDomainQueryHandler>? duplicates = null;

            for (int j = i + 1; j < handlers.Count; j++) {
                IDomainQueryHandler other = handlers[j];
                if (!string.Equals(candidate.Domain, other.Domain, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(candidate.QueryType, other.QueryType, StringComparison.OrdinalIgnoreCase)) {
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
                $"Duplicate query handlers are registered for domain '{candidate.Domain}' query type '{candidate.QueryType}': {handlerTypes}");
        }
    }
}
