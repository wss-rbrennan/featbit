using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Infrastructure.Scaling.Types;

namespace Infrastructure.BackplaneMesssages
{
    public interface IChannelConsumer
    {
        public string Channel { get; set; }

        Task HandleAsync(MessageContext message, CancellationToken cancellationToken);
    }
}
