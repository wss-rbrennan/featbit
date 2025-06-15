using Infrastructure.Connections;

namespace Infrastructure.Protocol;

public class StreamingOptions
{
    public const string Streaming = "Streaming";
    
    public string[] SupportedVersions { get; set; } = ConnectionVersion.All;

    public string[] SupportedTypes { get; set; } = ConnectionType.All;
    
    /// <summary>
    /// Token timeout in milliseconds. Default is 30 seconds (30000ms).
    /// </summary>
    public int TokenTimeoutMs { get; set; } = 30000;

    // Performance settings
    public int MaxMessageSize { get; set; } = 32768;
    public int KeepAliveIntervalMs { get; set; } = 30000;
    public int CloseTimeoutMs { get; set; } = 5000;
    public int MaxConcurrentConnections { get; set; } = 1000;
    public bool EnableConnectionThrottling { get; set; } = true;
    public int MessageBufferSize { get; set; } = 2048;
    public int MessageQueueLimit { get; set; } = 50;
}