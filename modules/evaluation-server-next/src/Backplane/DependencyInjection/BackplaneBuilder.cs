using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backplane.DependencyInjection
{
    public class BackplaneBuilder : IBackplaneBuilder
    {
        public IServiceCollection Services { get; }
        public BackplaneBuilder(IServiceCollection services)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));
        }
    }
}
