using Infrastructure.Protocol;
using Infrastructure.Scaling.Types;

namespace Infrastructure.Scaling.Session;

/// <summary>
/// Interface for managing client sessions and handling client identification
/// </summary>
public interface IClientSessionManager
{
    /// <summary>
    /// Handle client identification and session resume
    /// </summary>
    /// <param name="connectionId">WebSocket connection ID</param>
    /// <param name="identifyMessage">Client identification message</param>
    /// <returns>Identification response</returns>
    Task<ClientIdentifyResponse> HandleClientIdentifyAsync(string connectionId, ClientIdentifyMessage identifyMessage);
    
    /// <summary>
    /// Get active client session by connection ID
    /// </summary>
    /// <param name="connectionId">WebSocket connection ID</param>
    /// <returns>Client session if found</returns>
    Task<ClientSession?> GetSessionByConnectionAsync(string connectionId);
    
    /// <summary>
    /// Add a channel subscription for a client
    /// </summary>
    /// <param name="connectionId">WebSocket connection ID</param>
    /// <param name="channel">Channel to subscribe to</param>
    /// <returns>True if subscription was added</returns>
    Task<bool> AddChannelSubscriptionAsync(string connectionId, string channel);
    
    /// <summary>
    /// Remove a channel subscription for a client
    /// </summary>
    /// <param name="connectionId">WebSocket connection ID</param>
    /// <param name="channel">Channel to unsubscribe from</param>
    /// <returns>True if subscription was removed</returns>
    Task<bool> RemoveChannelSubscriptionAsync(string connectionId, string channel);
    
    /// <summary>
    /// Handle client disconnection
    /// </summary>
    /// <param name="connectionId">WebSocket connection ID</param>
    /// <returns>Task</returns>
    Task HandleClientDisconnectAsync(string connectionId);
    
    /// <summary>
    /// Send a message to a specific client
    /// </summary>
    /// <param name="clientId">Client identifier</param>
    /// <param name="message">Message to send</param>
    /// <returns>True if message was sent</returns>
    Task<bool> SendToClientAsync(string clientId, string message);
    
    /// <summary>
    /// Get all active sessions for this server
    /// </summary>
    /// <returns>List of active sessions</returns>
    Task<IEnumerable<ClientSession>> GetActiveSessionsAsync();
} 