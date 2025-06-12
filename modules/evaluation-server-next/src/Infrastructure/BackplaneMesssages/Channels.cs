using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.BackplaneMesssages
{
    public static class Channels
    {
        private const string EdgeNamespace = "featbit-els-edge-";
        public const string EdgePattern = EdgeNamespace + "*";

        private const string BackplaneNamespace = "featbit-els-backplane-";
        public const string BackplanePattern = BackplaneNamespace + "*";

        public static string GetEdgeChannelPattern()
        {
            return EdgePattern;
        }

        public static string GetEdgeChannel(string environmentId)
        {
            if (string.IsNullOrWhiteSpace(environmentId))
            {
                throw new ArgumentException("Environment ID cannot be null or empty.", nameof(environmentId));
            }
            return EdgeNamespace + environmentId;
        }

        public static string GetBackplaneChannelPattern()
        {
            return BackplanePattern;
        }

        public static string GetBackplaneChannel(string environmentId)
        {
            if (string.IsNullOrWhiteSpace(environmentId))
            {
                throw new ArgumentException("Environment ID cannot be null or empty.", nameof(environmentId));
            }
            return BackplaneNamespace + environmentId;
        }
    }
}
