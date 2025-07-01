using Domain.EndUsers;

namespace Infrastructure.Protocol;

/// <summary>
/// Message sent by clients to identify themselves and resume sessions
/// </summary>
public class ClientIdentifyMessage
{
    /// <summary>
    /// Type of message - should be "identify"
    /// </summary>
    public string Type { get; set; } = "identify";
    
    /// <summary>
    /// Optional client ID for session resumption (if null, a new session will be created)
    /// </summary>
    public string? ClientId { get; set; }
    
    /// <summary>
    /// Optional session ID for additional session validation
    /// </summary>
    public string? SessionId { get; set; }
    
    /// <summary>
    /// End user information
    /// </summary>
    public EndUser User { get; set; } = new();
    
    /// <summary>
    /// Environment ID
    /// </summary>
    public Guid EnvId { get; set; }
    
    /// <summary>
    /// Client SDK version (for debugging/analytics)
    /// </summary>
    public string? ClientVersion { get; set; }
    
    /// <summary>
    /// Timestamp when the message was created
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Response message sent to clients after identification
/// </summary>
public class ClientIdentifyResponse
{
    /// <summary>
    /// Type of message - "identify_response"
    /// </summary>
    public string Type { get; set; } = "identify_response";
    
    /// <summary>
    /// Whether identification was successful
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// The assigned client ID
    /// </summary>
    public string ClientId { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether this is a resumed session
    /// </summary>
    public bool SessionResumed { get; set; }
    
    /// <summary>
    /// List of channels that were restored (for resumed sessions)
    /// </summary>
    public List<string> RestoredChannels { get; set; } = new();
    
    /// <summary>
    /// Error message if identification failed
    /// </summary>
    public string? Error { get; set; }
    
    /// <summary>
    /// Session information
    /// </summary>
    public SessionInfo? Session { get; set; }
}

/// <summary>
/// Session information included in identify response
/// </summary>
public class SessionInfo
{
    /// <summary>
    /// When the session was created
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Last time the session was seen
    /// </summary>
    public DateTime LastSeen { get; set; }
    
    /// <summary>
    /// Server ID currently handling the session
    /// </summary>
    public string ServerId { get; set; } = string.Empty;
} 