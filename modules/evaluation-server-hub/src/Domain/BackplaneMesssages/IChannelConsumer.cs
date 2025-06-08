using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.BackplaneMesssages
{
    public interface IChannelConsumer
    {
        public string Channel { get; set; }

        Task HandleAsync(string message, CancellationToken cancellationToken);
    }
}
