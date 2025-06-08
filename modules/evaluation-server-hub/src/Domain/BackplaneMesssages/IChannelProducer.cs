using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.BackplaneMesssages
{
    public interface IChannelProducer
    {
        Task PublishAsync<TMessage>(string topic, TMessage? message) where TMessage : class;
    }
}
