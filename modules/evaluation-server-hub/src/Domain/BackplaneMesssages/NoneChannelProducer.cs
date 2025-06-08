using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.BackplaneMesssages
{
    public class NoneChannelProducer : IChannelProducer
    {
        public Task PublishAsync<TMessage>(string topic, TMessage? message) where TMessage : class => Task.CompletedTask;
    }
}
