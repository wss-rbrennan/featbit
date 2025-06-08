using Microsoft.Extensions.Logging;

namespace Application.Connections;

internal static class MessageContextTagProvider
{
    public static void RecordTags(ITagCollector collector, MessageContext context)
    {
        collector.Add("type", context.Type);
        collector.Add("token", context.Token);
        collector.Add("version", context.Version);

        collector.Add("project.key", context.ProjectKey);
        collector.Add("env.id", context.EnvId);
        collector.Add("env.key", context.EnvKey);
        
    }
}