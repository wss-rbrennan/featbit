using Infrastructure.Scaling.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Scaling.Handlers
{
    public interface IMessageHandler
    {
        public string Type { get; }

        Task HandleAsync(MessageContext ctx);
    }
}
