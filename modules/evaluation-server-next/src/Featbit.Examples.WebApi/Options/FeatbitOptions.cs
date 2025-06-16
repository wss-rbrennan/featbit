using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebApi.Options
{
    public class FeatbitOptions
    {
        public string EventServerUri { get; set; }
        public string StreamingServerUri { get; set; }
        public string ServerKey { get; set; }
        public int StartWaitTimeout { get; set; } = 180;
        public int ConnectTimeout { get; set; } = 120;
    }
}