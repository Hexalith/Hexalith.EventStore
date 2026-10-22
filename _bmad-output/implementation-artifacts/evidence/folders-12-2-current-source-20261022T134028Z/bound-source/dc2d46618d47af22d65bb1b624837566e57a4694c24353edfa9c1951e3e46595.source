namespace Hexalith.EventStore.Controllers;

using Dapr;
using Hexalith.EventStore.Server.Configuration;
using Microsoft.Extensions.Options;

/// <summary>Marks the receiver for binding to the publisher's configured component at endpoint construction.</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class ProjectionNotificationTopicAttribute() : TopicAttribute(ProjectionChangeNotifierOptions.DefaultPubSubName, "*.*.projection-changed")
{
    /// <summary>Binds programmatic metadata from the same options consumed by the publisher, or removes it in declarative mode.</summary>
    internal static void Bind(EndpointBuilder endpoint, IServiceProvider services, bool declarative)
    {
        for (int index = endpoint.Metadata.Count - 1; index >= 0; index--)
        {
            if (endpoint.Metadata[index] is not ProjectionNotificationTopicAttribute) continue;
            if (declarative) endpoint.Metadata.RemoveAt(index);
            else endpoint.Metadata[index] = new TopicAttribute(services.GetRequiredService<IOptions<ProjectionChangeNotifierOptions>>().Value.PubSubName, "*.*.projection-changed");
        }
    }
}
