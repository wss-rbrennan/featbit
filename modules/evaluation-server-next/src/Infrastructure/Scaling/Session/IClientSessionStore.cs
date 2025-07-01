using Domain.EndUsers;
using Infrastructure.Scaling.Types;

namespace Infrastructure.Scaling.Session;

/// <summary>
/// Interface for managing persistent client sessions across server instances
/// </summary>
public interface IClientSessionStore
{
    /// <summary>
    /// Create a new session or update existing session for a client
    /// </summary>
    /// <param name="user">End user information</param>
    /// <param name="envId">Environment ID</param>
    /// <param name="connectionId">Current WebSocket connection ID</param>
    /// <param name="serverId">ID of the server handling this connection</param>
    /// <returns>The client ID for the session</returns>
    Task<string> CreateOrUpdateSessionAsync(EndUser user, Guid envId, string connectionId, string serverId);
    
    /// <summary>
    /// Retrieve a client session by client ID
    /// </summary>
    /// <param name="clientId">Client identifier</param>
    /// <returns>Client session if found, null otherwise</returns>
    Task<ClientSession?> GetSessionAsync(string clientId);
    
    /// <summary>
    /// Update the connection information for an existing session
    /// </summary>
    /// <param name="clientId">Client identifier</param>
    /// <param name="newConnectionId">New WebSocket connection ID</param>
    /// <param name="serverId">ID of the server now handling this connection</param>
    /// <returns>True if session was updated, false if not found</returns>
    Task<bool> UpdateConnectionAsync(string clientId, string newConnectionId, string serverId);
    
    /// <summary>
    /// Add a channel subscription to a client session
    /// </summary>
    /// <param name="clientId">Client identifier</param>
    /// <param name="channel">Channel to subscribe to</param>
    /// <returns>True if channel was added, false if already subscribed or client not found</returns>
    Task<bool> AddChannelSubscriptionAsync(string clientId, string channel);
    
    /// <summary>
    /// Remove a channel subscription from a client session
    /// </summary>
    /// <param name="clientId">Client identifier</param>
    /// <param name="channel">Channel to unsubscribe from</param>
    /// <returns>True if channel was removed, false if not subscribed or client not found</returns>
    Task<bool> RemoveChannelSubscriptionAsync(string clientId, string channel);
    
    /// <summary>
    /// Transfer a session from one server to another
    /// </summary>
    /// <param name="clientId">Client identifier</param>
    /// <param name="fromServerId">Current server ID</param>
    /// <param name="toServerId">Target server ID</param>
    /// <returns>True if transfer was successful</returns>
    Task<bool> TransferSessionAsync(string clientId, string fromServerId, string toServerId);
    
    /// <summary>
    /// Remove a client session
    /// </summary>
    /// <param name="clientId">Client identifier</param>
    /// <returns>True if session was removed, false if not found</returns>
    Task<bool> RemoveSessionAsync(string clientId);
    
    /// <summary>
    /// Get all sessions for a specific server
    /// </summary>
    /// <param name="serverId">Server identifier</param>
    /// <returns>List of client sessions handled by the server</returns>
    Task<IEnumerable<ClientSession>> GetSessionsByServerAsync(string serverId);
    
    /// <summary>
    /// Clean up expired sessions
    /// </summary>
    /// <param name="expiredBefore">Remove sessions not seen since this time</param>
    /// <returns>Number of sessions cleaned up</returns>
    Task<int> CleanupExpiredSessionsAsync(DateTime expiredBefore);
    
    /// <summary>
    /// Get sessions that may be orphaned due to server shutdowns
    /// </summary>
    /// <param name="activeServerIds">List of currently active server IDs</param>
    /// <returns>Sessions that may need reassignment</returns>
    Task<IEnumerable<ClientSession>> GetOrphanedSessionsAsync(IEnumerable<string> activeServerIds);
} 