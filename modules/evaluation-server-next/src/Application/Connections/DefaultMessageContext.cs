
using Domain.EndUsers;
using System.Text.Json;

namespace Application.Connections
{
    public class DefaultMessageContext : MessageContext
    {
        public override string Type { get; }
        public override string Version { get; }
        public override string Token { get; }
        public override long ConnectAt { get; }

        public override string ProjectKey { get; }

        public override string EnvId { get; }

        public override string EnvKey { get; }

        public override EndUser? EndUser { get; }

        public override IEnumerable<Guid>? EnvIds { get; }

        public override JsonElement Data { get; }

        public DefaultMessageContext()
        {

        }

        public DefaultMessageContext(Dictionary<string, string> message)
        {
            Type = message["type"].ToString();
            Version = message["version"].ToString();
            Token = message["token"].ToString();
            ConnectAt = Convert.ToInt64(message["connectAt"]);
            ProjectKey = message["projectKey"];
            EnvId = message["envId"];
            EnvKey = message["envKey"];
            
            //ConnectAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public DefaultMessageContext(Dictionary<string, string> message, EndUser? endUser) : this(message) 
        {
            EndUser = EndUser;
            //ConnectAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public DefaultMessageContext(Dictionary<string, string> message, IEnumerable<Guid>? envIds) : this(message)
        {
            EnvIds = envIds;
        }

        public DefaultMessageContext(JsonElement dataElement) : this()
        {
            Data = dataElement;
        }



    }
}
