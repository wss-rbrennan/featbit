namespace Infrastructure.Scaling.Types
{
    /// <summary>
    /// Constants for different service types that can send messages
    /// </summary>
    public static class ServiceTypes
    {
        /// <summary>
        /// Edge service - handles WebSocket connections and client communication
        /// </summary>
        public const string Edge = "edge";

        /// <summary>
        /// Hub service - handles business logic and data processing
        /// </summary>
        public const string Hub = "hub";

        /// <summary>
        /// Web service - handles web-based requests and API calls
        /// </summary>
        public const string Web = "web";
    }
} 