using Confluent.Kafka;
using Domain.EndUsers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Application.Connections
{
    public abstract class MessageContext
    {

        public abstract string Type { get; }

        public abstract string Version { get; }

        public abstract string Token { get; }

        public abstract long ConnectAt { get; }

        public abstract string ProjectKey { get; }

        public abstract string EnvId { get; }

        public abstract string EnvKey { get; }

        public abstract EndUser? EndUser { get; }

        public abstract IEnumerable<Guid>? EnvIds { get; }

        public abstract JsonElement Data { get; }
    }
}
