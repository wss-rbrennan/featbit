using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Backplane
{
    public static class BackplaneBuilderExtensions
    {
        public static IBackplaneBuilder AddBackplane(this IBackplaneBuilder builder, IConfiguration config)
        {
            var services = builder.Services;

            
            return builder;
        }



    }
}
