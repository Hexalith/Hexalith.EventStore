namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Builds an isolated native invocation transport without changing ordinary HTTP configuration.</summary>
internal static class RouteCatalogInvocationTransport
{
    /// <summary>Copies effective native settings through their supported public configuration properties.</summary>
    public static HttpMessageHandler? Create(HttpMessageHandler ordinary)
    {
        // Unknown/custom transports need the owner's explicit factory. Never discard their
        // TLS, proxy, credential, connection or certificate configuration by substituting defaults.
        HttpMessageHandler? guarded = ordinary.GetType() == typeof(SocketsHttpHandler) ? new SocketsHttpHandler()
            : ordinary.GetType() == typeof(HttpClientHandler) ? new HttpClientHandler() : null;
        if (guarded is null) return null;
        try
        {
            foreach (var property in ordinary.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                if (!property.CanRead || property.SetMethod?.IsPublic != true || property.GetIndexParameters().Length != 0
                    || property.Name is "AllowAutoRedirect" or "UseDefaultCredentials") continue;
                property.SetValue(guarded, property.GetValue(ordinary));
            }
            if (ordinary is SocketsHttpHandler source && guarded is SocketsHttpHandler target)
            {
                foreach (var property in source.Properties) target.Properties[property.Key] = property.Value;
                target.AllowAutoRedirect = false;
            }
            else if (ordinary is HttpClientHandler sourceClient && guarded is HttpClientHandler targetClient)
            {
                foreach (var property in sourceClient.Properties) targetClient.Properties[property.Key] = property.Value;
                if (sourceClient.ClientCertificateOptions == ClientCertificateOption.Manual)
                    foreach (var certificate in sourceClient.ClientCertificates) targetClient.ClientCertificates.Add(certificate);
                targetClient.AllowAutoRedirect = false;
            }
            return guarded;
        }
        catch
        {
            guarded.Dispose();
            // Failure to reproduce effective settings closes invocation only. Ordinary HTTP
            // retains the exact owner-configured transport and continues to function.
            return null;
        }
    }
}
