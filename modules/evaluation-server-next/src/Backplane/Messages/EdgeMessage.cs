//using Confluent.Kafka;
//using Domain.EndUsers;
//using Domain.Shared;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Net.WebSockets;
//using System.Text;
//using System.Threading.Tasks;

//namespace Backplane.Messages
//{
//    public class EdgeMessage
//    {
//        public string Id { get; }

//        //public WebSocket WebSocket { get; }

//        /// <summary>
//        /// client-side sdk EndUser
//        /// </summary>
//        public EndUser? User { get; set; }

//        public string Type { get; }

//        public string Version { get; }

//        public long ConnectAt { get; }

//        public long CloseAt { get; private set; }

//        #region extra

//        public string ProjectKey { get; }

//        public Guid EnvId { get; }

//        public string EnvKey { get; }

//        public string ClientIpAddress { get; private set; }

//        public string ClientHost { get; private set; }

//        public IEnumerable<Guid>? EnvIds { get; }

//        public long? Timestamp { get; set; }

//        //public Connection Connection { get; protected set; }
//        //public Connection[] MappedRpConnections { get; protected set; }

//        #endregion

//        public EdgeMessage(
//            string id,
//            Secret secret,
//            string type,
//            string version,
//            long connectAt)
//        {
//            Id = id;

//            //WebSocket = webSocket;
//            Type = type;
//            Version = version;
//            ConnectAt = connectAt;
//            CloseAt = 0;

//            ProjectKey = secret.ProjectKey;
//            EnvId = secret.EnvId;
//            EnvKey = secret.EnvKey;

//            ClientIpAddress = string.Empty;
//            ClientHost = string.Empty;
//        }

//        public EdgeMessage(
//            string id,
//            Secret secret,
//            string type,
//            string version,
//            long connectAt,
//            IEnumerable<Guid>? envIds) : this(id, secret, type, version, connectAt)
//        {
//            EnvIds = envIds;
//        }

//        public void AttachClient(string clientIpAddress, string clientHost)
//        {
//            ClientIpAddress = clientIpAddress;
//            ClientHost = clientHost;
//        }

//        /// <summary>
//        /// attach client-side sdk EndUser
//        /// </summary>
//        public void AttachUser(EndUser user)
//        {
//            User = user;
//        }
//    }
//}
