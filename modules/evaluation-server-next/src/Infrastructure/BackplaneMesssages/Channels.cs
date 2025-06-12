using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.BackplaneMesssages
{
    public static class Channels
    {
        private const string Namespace = "featbit-els-";
        public const string EnvironmentPattern = Namespace + "*";

        public static string GetEnvironmentChannelPattern()
        {
            return EnvironmentPattern;
        }

        public static string GetEnvironmentChannel(string environmentId)
        {
            if (string.IsNullOrWhiteSpace(environmentId))
            {
                throw new ArgumentException("Environment ID cannot be null or empty.", nameof(environmentId));
            }
            return Namespace + environmentId;
        }
    }
}
