using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Scaling.Types
{
    public static class MessageType
    {
        public const string Server = "server";
        public const string Client = "client";
        public const string RelayProxy = "relay-proxy";

        public static readonly string[] All = [Server, Client, RelayProxy];
    }
}
