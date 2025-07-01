using DataStore.Caches.Redis;
using Domain.EndUsers;
using Infrastructure.Scaling.Types;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace Infrastructure.Scaling.Session;

/// <summary>
/// Redis-based implementation of client session storage for cross-server session persistence
/// </summary>
public class RedisClientSessionStore : IClientSessionStore
{
    private readonly IRedisClient _redis;
    private readonly ILogger<RedisClientSessionStore> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    
    private const string SESSION_KEY_PREFIX = "featbit:session:";
    private const string SERVER_SESSIONS_KEY_PREFIX = "featbit:server:sessions:";
    private const int DEFAULT_SESSION_TTL_HOURS = 24; // Sessions expire after 24 hours of inactivity

    public RedisClientSessionStore(IRedisClient redis, ILogger<RedisClientSessionStore> logger)
    {
        _redis = redis;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task<string> CreateOrUpdateSessionAsync(EndUser user, Guid envId, string connectionId, string serverId)
    {
        try
        {
            var clientId = ClientSession.GenerateClientId(user, envId);
            var sessionKey = GetSessionKey(clientId);
            var serverSessionsKey = GetServerSessionsKey(serverId);
            
            var database = _redis.GetDatabase();
            
            // Check if session already exists
            var existingSessionJson = await database.StringGetAsync(sessionKey);
            ClientSession session;
            
            if (existingSessionJson.HasValue)
            {
                // Update existing session
                session = JsonSerializer.Deserialize<ClientSession>(existingSessionJson!, _jsonOptions)!;
                session.UpdateConnection(connectionId, serverId);
                session.User = user; // Update user info in case it changed
                
                _logger.LogDebug("Updated existing session for client {ClientId}, connection {ConnectionId}", 
                    clientId, connectionId);
            }
            else
            {
                // Create new session
                session = new ClientSession
                {
                    ClientId = clientId,
                    EnvId = envId,
                    User = user,
                    CurrentConnectionId = connectionId,
                    ServerId = serverId,
                    CreatedAt = DateTime.UtcNow,
                    LastSeen = DateTime.UtcNow
                };
                
                _logger.LogDebug("Created new session for client {ClientId}, connection {ConnectionId}", 
                    clientId, connectionId);
            }
            
            // Save session with TTL
            var sessionJson = JsonSerializer.Serialize(session, _jsonOptions);
            await database.StringSetAsync(sessionKey, sessionJson, TimeSpan.FromHours(DEFAULT_SESSION_TTL_HOURS));
            
            // Add to server's session set
            await database.SetAddAsync(serverSessionsKey, clientId);
            await database.KeyExpireAsync(serverSessionsKey, TimeSpan.FromHours(DEFAULT_SESSION_TTL_HOURS + 1));
            
            return clientId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating/updating session for user {UserKeyId} in env {EnvId}", 
                user.KeyId, envId);
            throw;
        }
    }

    public async Task<ClientSession?> GetSessionAsync(string clientId)
    {
        try
        {
            var sessionKey = GetSessionKey(clientId);
            var database = _redis.GetDatabase();
            
            var sessionJson = await database.StringGetAsync(sessionKey);
            if (!sessionJson.HasValue)
            {
                _logger.LogDebug("Session not found for client {ClientId}", clientId);
                return null;
            }

            var session = JsonSerializer.Deserialize<ClientSession>(sessionJson!, _jsonOptions);
            _logger.LogDebug("Retrieved session for client {ClientId}", clientId);
            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving session for client {ClientId}", clientId);
            return null;
        }
    }

    public async Task<bool> UpdateConnectionAsync(string clientId, string newConnectionId, string serverId)
    {
        try
        {
            var sessionKey = GetSessionKey(clientId);
            var database = _redis.GetDatabase();
            
            var sessionJson = await database.StringGetAsync(sessionKey);
            if (!sessionJson.HasValue)
            {
                _logger.LogWarning("Cannot update connection - session not found for client {ClientId}", clientId);
                return false;
            }

            var session = JsonSerializer.Deserialize<ClientSession>(sessionJson!, _jsonOptions)!;
            var oldServerId = session.ServerId;
            
            session.UpdateConnection(newConnectionId, serverId);
            
            // Save updated session
            var updatedSessionJson = JsonSerializer.Serialize(session, _jsonOptions);
            await database.StringSetAsync(sessionKey, updatedSessionJson, TimeSpan.FromHours(DEFAULT_SESSION_TTL_HOURS));
            
            // Update server session sets if server changed
            if (oldServerId != serverId)
            {
                if (!string.IsNullOrEmpty(oldServerId))
                {
                    await database.SetRemoveAsync(GetServerSessionsKey(oldServerId), clientId);
                }
                await database.SetAddAsync(GetServerSessionsKey(serverId), clientId);
                await database.KeyExpireAsync(GetServerSessionsKey(serverId), TimeSpan.FromHours(DEFAULT_SESSION_TTL_HOURS + 1));
            }
            
            _logger.LogDebug("Updated connection for client {ClientId} to {ConnectionId} on server {ServerId}", 
                clientId, newConnectionId, serverId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating connection for client {ClientId}", clientId);
            return false;
        }
    }

    public async Task<bool> AddChannelSubscriptionAsync(string clientId, string channel)
    {
        try
        {
            var sessionKey = GetSessionKey(clientId);
            var database = _redis.GetDatabase();
            
            var sessionJson = await database.StringGetAsync(sessionKey);
            if (!sessionJson.HasValue)
            {
                _logger.LogWarning("Cannot add channel subscription - session not found for client {ClientId}", clientId);
                return false;
            }

            var session = JsonSerializer.Deserialize<ClientSession>(sessionJson!, _jsonOptions)!;
            var added = session.AddChannel(channel);
            
            if (added)
            {
                var updatedSessionJson = JsonSerializer.Serialize(session, _jsonOptions);
                await database.StringSetAsync(sessionKey, updatedSessionJson, TimeSpan.FromHours(DEFAULT_SESSION_TTL_HOURS));
                
                _logger.LogDebug("Added channel subscription {Channel} for client {ClientId}", channel, clientId);
            }
            
            return added;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding channel subscription for client {ClientId}", clientId);
            return false;
        }
    }

    public async Task<bool> RemoveChannelSubscriptionAsync(string clientId, string channel)
    {
        try
        {
            var sessionKey = GetSessionKey(clientId);
            var database = _redis.GetDatabase();
            
            var sessionJson = await database.StringGetAsync(sessionKey);
            if (!sessionJson.HasValue)
            {
                _logger.LogWarning("Cannot remove channel subscription - session not found for client {ClientId}", clientId);
                return false;
            }

            var session = JsonSerializer.Deserialize<ClientSession>(sessionJson!, _jsonOptions)!;
            var removed = session.RemoveChannel(channel);
            
            if (removed)
            {
                var updatedSessionJson = JsonSerializer.Serialize(session, _jsonOptions);
                await database.StringSetAsync(sessionKey, updatedSessionJson, TimeSpan.FromHours(DEFAULT_SESSION_TTL_HOURS));
                
                _logger.LogDebug("Removed channel subscription {Channel} for client {ClientId}", channel, clientId);
            }
            
            return removed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing channel subscription for client {ClientId}", clientId);
            return false;
        }
    }

    public async Task<bool> TransferSessionAsync(string clientId, string fromServerId, string toServerId)
    {
        try
        {
            var database = _redis.GetDatabase();
            
            // Update server session sets
            await database.SetRemoveAsync(GetServerSessionsKey(fromServerId), clientId);
            await database.SetAddAsync(GetServerSessionsKey(toServerId), clientId);
            await database.KeyExpireAsync(GetServerSessionsKey(toServerId), TimeSpan.FromHours(DEFAULT_SESSION_TTL_HOURS + 1));
            
            _logger.LogDebug("Transferred session {ClientId} from server {FromServerId} to {ToServerId}", 
                clientId, fromServerId, toServerId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transferring session {ClientId} from {FromServerId} to {ToServerId}", 
                clientId, fromServerId, toServerId);
            return false;
        }
    }

    public async Task<bool> RemoveSessionAsync(string clientId)
    {
        try
        {
            var sessionKey = GetSessionKey(clientId);
            var database = _redis.GetDatabase();
            
            // Get session to find which server it belonged to
            var sessionJson = await database.StringGetAsync(sessionKey);
            if (sessionJson.HasValue)
            {
                var session = JsonSerializer.Deserialize<ClientSession>(sessionJson!, _jsonOptions)!;
                
                // Remove from server's session set
                await database.SetRemoveAsync(GetServerSessionsKey(session.ServerId), clientId);
            }
            
            // Remove the session itself
            var deleted = await database.KeyDeleteAsync(sessionKey);
            
            if (deleted)
            {
                _logger.LogDebug("Removed session for client {ClientId}", clientId);
            }
            
            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing session for client {ClientId}", clientId);
            return false;
        }
    }

    public async Task<IEnumerable<ClientSession>> GetSessionsByServerAsync(string serverId)
    {
        try
        {
            var serverSessionsKey = GetServerSessionsKey(serverId);
            var database = _redis.GetDatabase();
            
            var clientIds = await database.SetMembersAsync(serverSessionsKey);
            var sessions = new List<ClientSession>();
            
            foreach (var clientId in clientIds)
            {
                var session = await GetSessionAsync(clientId!);
                if (session != null)
                {
                    sessions.Add(session);
                }
            }
            
            _logger.LogDebug("Retrieved {Count} sessions for server {ServerId}", sessions.Count, serverId);
            return sessions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sessions for server {ServerId}", serverId);
            return Enumerable.Empty<ClientSession>();
        }
    }

    public async Task<int> CleanupExpiredSessionsAsync(DateTime expiredBefore)
    {
        try
        {
            var database = _redis.GetDatabase();
            var pattern = SESSION_KEY_PREFIX + "*";
            
            int cleanedCount = 0;
            var server = _redis.Connection.GetServer(_redis.Connection.GetEndPoints().First());
            
            foreach (var key in server.Keys(pattern: pattern))
            {
                var sessionJson = await database.StringGetAsync(key);
                if (sessionJson.HasValue)
                {
                    var session = JsonSerializer.Deserialize<ClientSession>(sessionJson!, _jsonOptions);
                    if (session?.LastSeen < expiredBefore)
                    {
                        await RemoveSessionAsync(session.ClientId);
                        cleanedCount++;
                    }
                }
            }
            
            _logger.LogInformation("Cleaned up {Count} expired sessions (older than {ExpiredBefore})", 
                cleanedCount, expiredBefore);
            return cleanedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during session cleanup");
            return 0;
        }
    }

    public async Task<IEnumerable<ClientSession>> GetOrphanedSessionsAsync(IEnumerable<string> activeServerIds)
    {
        try
        {
            var database = _redis.GetDatabase();
            var pattern = SESSION_KEY_PREFIX + "*";
            var orphanedSessions = new List<ClientSession>();
            var activeServerSet = activeServerIds.ToHashSet();
            
            var server = _redis.Connection.GetServer(_redis.Connection.GetEndPoints().First());
            
            foreach (var key in server.Keys(pattern: pattern))
            {
                var sessionJson = await database.StringGetAsync(key);
                if (sessionJson.HasValue)
                {
                    var session = JsonSerializer.Deserialize<ClientSession>(sessionJson!, _jsonOptions);
                    if (session != null && !activeServerSet.Contains(session.ServerId))
                    {
                        orphanedSessions.Add(session);
                    }
                }
            }
            
            _logger.LogInformation("Found {Count} orphaned sessions", orphanedSessions.Count);
            return orphanedSessions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding orphaned sessions");
            return Enumerable.Empty<ClientSession>();
        }
    }

    private static string GetSessionKey(string clientId)
    {
        return SESSION_KEY_PREFIX + clientId;
    }

    private static string GetServerSessionsKey(string serverId)
    {
        return SERVER_SESSIONS_KEY_PREFIX + serverId;
    }
} 