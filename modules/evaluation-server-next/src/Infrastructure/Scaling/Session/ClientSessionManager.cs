using Infrastructure.Protocol;
using Infrastructure.Scaling.Service;
using Infrastructure.Scaling.Types;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Infrastructure.Scaling.Session;

/// <summary>
/// Manages client sessions and coordinates with subscription service for session persistence
/// </summary>
public class ClientSessionManager : IClientSessionManager
{
    private readonly IClientSessionStore _sessionStore;
    private readonly ISubscriptionManager _subscriptionManager;
    private readonly IServiceIdentityProvider _serviceIdentityProvider;
    private readonly ILogger<ClientSessionManager> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    
    // In-memory mapping of connectionId -> clientId for quick lookups
    private readonly ConcurrentDictionary<string, string> _connectionToClient = new();
    
    // In-memory mapping of clientId -> connectionId for this server instance
    private readonly ConcurrentDictionary<string, string> _clientToConnection = new();

    public ClientSessionManager(
        IClientSessionStore sessionStore,
        ISubscriptionManager subscriptionManager,
        IServiceIdentityProvider serviceIdentityProvider,
        ILogger<ClientSessionManager> logger)
    {
        _sessionStore = sessionStore;
        _subscriptionManager = subscriptionManager;
        _serviceIdentityProvider = serviceIdentityProvider;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<ClientIdentifyResponse> HandleClientIdentifyAsync(string connectionId, ClientIdentifyMessage identifyMessage)
    {
        try
        {
            var serverId = _serviceIdentityProvider.ServiceId;
            var response = new ClientIdentifyResponse();
            
            // Validate the identify message
            if (!identifyMessage.User.IsValid())
            {
                response.Success = false;
                response.Error = "Invalid user information provided";
                _logger.LogWarning("Client identification failed - invalid user information for connection {ConnectionId}", connectionId);
                return response;
            }

            // Check if client is trying to resume an existing session
            ClientSession? existingSession = null;
            if (!string.IsNullOrEmpty(identifyMessage.ClientId))
            {
                existingSession = await _sessionStore.GetSessionAsync(identifyMessage.ClientId);
                _logger.LogDebug("Client {ClientId} attempting session resume, existing session found: {Found}", 
                    identifyMessage.ClientId, existingSession != null);
            }

            string clientId;
            if (existingSession != null)
            {
                // Resume existing session
                clientId = existingSession.ClientId;
                
                // Update session with new connection details
                await _sessionStore.UpdateConnectionAsync(clientId, connectionId, serverId);
                
                // Restore channel subscriptions
                foreach (var channel in existingSession.SubscribedChannels)
                {
                    _subscriptionManager.AddChannelToSubscription(connectionId, channel);
                }
                
                response.Success = true;
                response.ClientId = clientId;
                response.SessionResumed = true;
                response.RestoredChannels = existingSession.SubscribedChannels.ToList();
                response.Session = new SessionInfo
                {
                    CreatedAt = existingSession.CreatedAt,
                    LastSeen = existingSession.LastSeen,
                    ServerId = serverId
                };
                
                _logger.LogInformation("Successfully resumed session for client {ClientId} on connection {ConnectionId}, restored {ChannelCount} channels", 
                    clientId, connectionId, existingSession.SubscribedChannels.Count);
            }
            else
            {
                // Create new session
                clientId = await _sessionStore.CreateOrUpdateSessionAsync(
                    identifyMessage.User, 
                    identifyMessage.EnvId, 
                    connectionId, 
                    serverId);
                
                response.Success = true;
                response.ClientId = clientId;
                response.SessionResumed = false;
                response.Session = new SessionInfo
                {
                    CreatedAt = DateTime.UtcNow,
                    LastSeen = DateTime.UtcNow,
                    ServerId = serverId
                };
                
                _logger.LogInformation("Created new session for client {ClientId} on connection {ConnectionId}", 
                    clientId, connectionId);
            }
            
            // Update in-memory mappings
            _connectionToClient[connectionId] = clientId;
            _clientToConnection[clientId] = connectionId;
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling client identification for connection {ConnectionId}", connectionId);
            return new ClientIdentifyResponse
            {
                Success = false,
                Error = "Internal server error during identification"
            };
        }
    }

    public async Task<ClientSession?> GetSessionByConnectionAsync(string connectionId)
    {
        try
        {
            if (_connectionToClient.TryGetValue(connectionId, out var clientId))
            {
                return await _sessionStore.GetSessionAsync(clientId);
            }
            
            _logger.LogDebug("No client session found for connection {ConnectionId}", connectionId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving session for connection {ConnectionId}", connectionId);
            return null;
        }
    }

    public async Task<bool> AddChannelSubscriptionAsync(string connectionId, string channel)
    {
        try
        {
            // ✅ ALWAYS add to subscription service first (works for both legacy and modern clients)
            _subscriptionManager.AddChannelToSubscription(connectionId, channel);
            
            // 🎯 OPTIONAL: Add to persistent session if client is identified (enhancement for modern clients)
            if (_connectionToClient.TryGetValue(connectionId, out var clientId))
            {
                try
                {
                    var added = await _sessionStore.AddChannelSubscriptionAsync(clientId, channel);
                    _logger.LogDebug("Added channel subscription {Channel} for identified client {ClientId} on connection {ConnectionId}", 
                        channel, clientId, connectionId);
                }
                catch (Exception ex)
                {
                    // ⚠️ Session persistence failure doesn't break basic functionality
                    _logger.LogWarning(ex, "Failed to persist channel subscription {Channel} for client {ClientId}, but subscription is active", 
                        channel, clientId);
                }
            }
            else
            {
                // ✅ Legacy client - log at debug level (not warning)
                _logger.LogDebug("Added channel subscription {Channel} for anonymous connection {ConnectionId} (no persistent session)", 
                    channel, connectionId);
            }
            
            return true; // Always succeeds for subscription service
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding channel subscription for connection {ConnectionId}", connectionId);
            return false;
        }
    }

    public async Task<bool> RemoveChannelSubscriptionAsync(string connectionId, string channel)
    {
        try
        {
            // ✅ ALWAYS remove from subscription service first (works for both legacy and modern clients)
            _subscriptionManager.RemoveChannelFromSubscription(connectionId, channel);
            
            // 🎯 OPTIONAL: Remove from persistent session if client is identified (enhancement for modern clients)
            if (_connectionToClient.TryGetValue(connectionId, out var clientId))
            {
                try
                {
                    var removed = await _sessionStore.RemoveChannelSubscriptionAsync(clientId, channel);
                    _logger.LogDebug("Removed channel subscription {Channel} for identified client {ClientId} on connection {ConnectionId}", 
                        channel, clientId, connectionId);
                }
                catch (Exception ex)
                {
                    // ⚠️ Session persistence failure doesn't break basic functionality
                    _logger.LogWarning(ex, "Failed to remove channel subscription {Channel} from client {ClientId} session, but subscription is inactive", 
                        channel, clientId);
                }
            }
            else
            {
                // ✅ Legacy client - log at debug level (not warning)
                _logger.LogDebug("Removed channel subscription {Channel} for anonymous connection {ConnectionId} (no persistent session)", 
                    channel, connectionId);
            }
            
            return true; // Always succeeds for subscription service
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing channel subscription for connection {ConnectionId}", connectionId);
            return false;
        }
    }

    public async Task HandleClientDisconnectAsync(string connectionId)
    {
        try
        {
            if (_connectionToClient.TryRemove(connectionId, out var clientId))
            {
                _clientToConnection.TryRemove(clientId, out _);
                
                // Remove from subscription service
                _subscriptionManager.RemoveSubscription(connectionId);
                
                // Note: We don't remove the session from the store as it should persist
                // for potential reconnection. Sessions will be cleaned up by the cleanup service.
                
                _logger.LogDebug("Handled disconnect for client {ClientId} on connection {ConnectionId}", 
                    clientId, connectionId);
            }
            else
            {
                // Still clean up the subscription service
                _subscriptionManager.RemoveSubscription(connectionId);
                _logger.LogDebug("Handled disconnect for unidentified connection {ConnectionId}", connectionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling client disconnect for connection {ConnectionId}", connectionId);
        }
    }

    public async Task<bool> SendToClientAsync(string clientId, string message)
    {
        try
        {
            // Check if client is on this server
            if (_clientToConnection.TryGetValue(clientId, out var connectionId))
            {
                // Client is on this server, send directly
                await _subscriptionManager.BroadcastToSubscriberAsync(connectionId, message);
                _logger.LogDebug("Sent message to client {ClientId} on local connection {ConnectionId}", 
                    clientId, connectionId);
                return true;
            }
            else
            {
                // Client is not on this server, would need to route via backplane
                // This would be implemented in Phase 3 of the solution
                _logger.LogDebug("Client {ClientId} not on this server, message routing not yet implemented", clientId);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to client {ClientId}", clientId);
            return false;
        }
    }

    public async Task<IEnumerable<ClientSession>> GetActiveSessionsAsync()
    {
        try
        {
            var serverId = _serviceIdentityProvider.ServiceId;
            return await _sessionStore.GetSessionsByServerAsync(serverId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active sessions");
            return Enumerable.Empty<ClientSession>();
        }
    }
} 