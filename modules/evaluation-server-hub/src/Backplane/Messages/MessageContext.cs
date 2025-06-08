using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Backplane.Messages
{
    public class MessageContext
    {
        public string Environment { get; set; }

        public JsonElement Data { get; set; }

        public CancellationToken CancellationToken { get; set; }

        public MessageContext(string environment, JsonElement data)
        {
            Environment = environment;
            Data = data;
        }
    }
}
