using Domain.EndUsers;

namespace Infrastructure.Scaling.Types;

/// <summary>
/// Represents a persistent client session that survives WebSocket reconnections across different servers
/// </summary>
public class ClientSession
{
    /// <summary>
    /// Persistent identifier for the client (e.g., "user-123-env-456")
    /// </summary>
    public string ClientId { get; set; } = string.Empty;
    
    /// <summary>
    /// Current WebSocket connection ID (changes on reconnection)
    /// </summary>
    public string CurrentConnectionId { get; set; } = string.Empty;
    
    /// <summary>
    /// Environment ID the client is connected to
    /// </summary>
    public Guid EnvId { get; set; }
    
    /// <summary>
    /// End user information
    /// </summary>
    public EndUser User { get; set; } = new();
    
    /// <summary>
    /// List of channels the client is subscribed to
    /// </summary>
    public List<string> SubscribedChannels { get; set; } = new();
    
    /// <summary>
    /// Last time the client was seen (for cleanup purposes)
    /// </summary>
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// ID of the server currently handling this client
    /// </summary>
    public string ServerId { get; set; } = string.Empty;
    
    /// <summary>
    /// When the session was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Generate a client ID based on user and environment
    /// </summary>
    public static string GenerateClientId(EndUser user, Guid envId)
    {
        return $"client:{user.KeyId}:env:{envId:N}";
    }
    
    /// <summary>
    /// Update the session with a new connection
    /// </summary>
    public void UpdateConnection(string connectionId, string serverId)
    {
        CurrentConnectionId = connectionId;
        ServerId = serverId;
        LastSeen = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Add a channel subscription
    /// </summary>
    public bool AddChannel(string channel)
    {
        if (!SubscribedChannels.Contains(channel))
        {
            SubscribedChannels.Add(channel);
            LastSeen = DateTime.UtcNow;
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// Remove a channel subscription
    /// </summary>
    public bool RemoveChannel(string channel)
    {
        var removed = SubscribedChannels.Remove(channel);
        if (removed)
        {
            LastSeen = DateTime.UtcNow;
        }
        return removed;
    }
} 