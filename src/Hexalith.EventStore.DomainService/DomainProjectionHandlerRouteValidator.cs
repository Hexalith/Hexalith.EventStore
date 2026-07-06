namespace Hexalith.EventStore.DomainService;

internal static class DomainProjectionHandlerRouteValidator {
    public static IDomainProjectionHandler[] MaterializeAndValidate(IEnumerable<IDomainProjectionHandler>? projectionHandlers) {
        IDomainProjectionHandler[] handlers = projectionHandlers is null ? [] : [.. projectionHandlers];
        ThrowIfDuplicateRoutes(handlers);
        return handlers;
    }

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
}
