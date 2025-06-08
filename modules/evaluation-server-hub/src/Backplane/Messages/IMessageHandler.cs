using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backplane.Messages
{
    public interface IMessageHandler
    {
        public string Type { get; }

        Task HandleAsync(EdgeMessage ctx);
    }
}
