using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backplane.DependencyInjection
{
    public interface IBackplaneBuilder
    {
        IServiceCollection Services { get; }
    }
}
