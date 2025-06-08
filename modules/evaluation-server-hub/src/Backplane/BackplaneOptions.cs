using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Backplane.Connections;
using Backplane.EdgeConnections;

namespace Backplane
{
    public class BackplaneOptions
    {
        public string[] SupportedVersions { get; set; } = EdgeConnectionVersion.All;

        public string[] SupportedTypes { get; set; } = ConnectionType.All;
    }
}
