using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using Domain.EndUsers;
using Domain.Shared;
using Infrastructure.Connections;
using Microsoft.Extensions.Logging;
using static Domain.EndUsers.EndUser;

namespace Infrastructure.Scaling.Session;

/// <summary>
/// Generates deterministic client identifiers for legacy clients based on connection context.
/// This enables seamless session management without requiring client code changes.
/// </summary>
public class LegacyClientIdentifierGenerator : ILegacyClientIdentifierGenerator
{
    private readonly ILogger<LegacyClientIdentifierGenerator> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    
    // Prefix to identify legacy-generated client IDs
    private const string LEGACY_PREFIX = "legacy:";
    
    public LegacyClientIdentifierGenerator(ILogger<LegacyClientIdentifierGenerator> logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public string GenerateClientId(ConnectionContext connectionContext)
    {
        try
        {
            var secret = connectionContext.Connection?.Secret;
            if (secret == null)
            {
                throw new InvalidOperationException("Connection secret is required for client identification");
            }

            var user = ExtractUser(connectionContext);
            var connectionType = connectionContext.Type;
            
            // Extract additional network and query context for better differentiation
            var networkContext = ExtractNetworkContext(connectionContext);
            var customIdentifiers = ExtractCustomIdentifiers(connectionContext);
            
            return GenerateClientId(secret, user, connectionType, networkContext, customIdentifiers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating client ID from connection context");
            throw;
        }
    }

    public string GenerateClientId(Secret secret, EndUser? user = null, string? connectionType = null)
    {
        return GenerateClientId(secret, user, connectionType, null, null);
    }

    public string GenerateClientId(Secret secret, EndUser? user = null, string? connectionType = null, 
        NetworkContext? networkContext = null, Dictionary<string, string>? customIdentifiers = null)
    {
        try
        {
            // Build identifier components based on connection type
            var identifierData = connectionType?.ToLowerInvariant() switch
            {
                "client" => BuildClientSideIdentifier(secret, user, networkContext, customIdentifiers),
                "server" => BuildServerSideIdentifier(secret, networkContext, customIdentifiers),
                "relay-proxy" => BuildRelayProxyIdentifier(secret, networkContext, customIdentifiers),
                _ => BuildGenericIdentifier(secret, user, connectionType, networkContext, customIdentifiers)
            };

            // Create deterministic hash
            var hash = ComputeDeterministicHash(identifierData);
            
            // Format: legacy:type:hash:env
            var clientId = $"{LEGACY_PREFIX}{connectionType?.ToLowerInvariant() ?? "unknown"}:{hash}:env:{secret.EnvId:N}";
            
            _logger.LogDebug("Generated legacy client ID for {ConnectionType} connection in environment {EnvId}", 
                connectionType, secret.EnvId);
                
            return clientId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating client ID from components");
            throw;
        }
    }

    public EndUser? ExtractUser(ConnectionContext connectionContext)
    {
        try
        {
            // Check connection for attached user (client-side SDKs)
            var user = connectionContext.Connection?.User;
            if (user != null)
            {
                _logger.LogDebug("Extracted user {UserId} from connection context", user.KeyId);
                return user;
            }

            // Could extract from query parameters, headers, etc. if needed
            // For now, return null for server-side connections
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting user from connection context");
            return null;
        }
    }

    public NetworkContext? ExtractNetworkContext(ConnectionContext connectionContext)
    {
        try
        {
            var client = connectionContext.Client;
            if (client == null) return null;

            return new NetworkContext
            {
                IpAddress = client.IpAddress,
                Host = client.Host
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting network context from connection");
            return null;
        }
    }

    public Dictionary<string, string>? ExtractCustomIdentifiers(ConnectionContext connectionContext)
    {
        try
        {
            var customIdentifiers = new Dictionary<string, string>();

            // Extract custom identification parameters from query string
            var rawQuery = connectionContext.RawQuery;
            if (!string.IsNullOrEmpty(rawQuery))
            {
                var queryParams = HttpUtility.ParseQueryString(rawQuery);
                
                // Standard custom identification parameters
                var identifierKeys = new[] { "instanceId", "serverId", "nodeId", "region", "datacenter", "version" };
                
                foreach (var key in identifierKeys)
                {
                    var value = queryParams[key];
                    if (!string.IsNullOrEmpty(value))
                    {
                        customIdentifiers[key] = value;
                    }
                }

                // Kubernetes/Container-specific parameters
                var containerKeys = new[] { "podName", "podNamespace", "containerName", "replicaSet", "deployment" };
                
                foreach (var key in containerKeys)
                {
                    var value = queryParams[key];
                    if (!string.IsNullOrEmpty(value))
                    {
                        customIdentifiers[key] = value;
                    }
                }
            }

            // Add connection-specific unique identifier as fallback
            // This ensures uniqueness even when network context and custom params are identical
            var connectionId = ExtractConnectionIdentifier(connectionContext);
            if (!string.IsNullOrEmpty(connectionId))
            {
                customIdentifiers["connectionId"] = connectionId;
            }

            return customIdentifiers.Count > 0 ? customIdentifiers : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting custom identifiers from connection");
            return null;
        }
    }

    private string ExtractConnectionIdentifier(ConnectionContext connectionContext)
    {
        try
        {
            // Use connection-specific data to create a unique identifier
            // This combines connection time + token hash to ensure uniqueness
            var connectTime = connectionContext.ConnectAt;
            var tokenHash = ComputeShortHash(connectionContext.Token ?? "");
            
            return $"{connectTime}-{tokenHash}";
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error extracting connection identifier, using fallback");
            // Fallback to a simple timestamp-based identifier
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        }
    }

    private string ComputeShortHash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash)[..6].Replace("+", "-").Replace("/", "_");
    }

    public bool IsLegacyGeneratedId(string clientId)
    {
        return !string.IsNullOrEmpty(clientId) && clientId.StartsWith(LEGACY_PREFIX, StringComparison.OrdinalIgnoreCase);
    }

    #region Private Methods

    private IdentifierData BuildClientSideIdentifier(Secret secret, EndUser? user, NetworkContext? networkContext, Dictionary<string, string>? customIdentifiers)
    {
        return new IdentifierData
        {
            Type = "client",
            EnvId = secret.EnvId,
            ProjectKey = secret.ProjectKey,
            EnvKey = secret.EnvKey,
            UserId = user?.KeyId,
            UserName = user?.Name,
            // Include a subset of user properties for uniqueness (but not all to avoid instability)
            UserPropertiesHash = user?.CustomizedProperties?.Length > 0 
                ? ComputePropertiesHash(user.CustomizedProperties) 
                : null,
            IpAddress = networkContext?.IpAddress,
            Host = networkContext?.Host,
            CustomIdentifiers = customIdentifiers
        };
    }

    private IdentifierData BuildServerSideIdentifier(Secret secret, NetworkContext? networkContext, Dictionary<string, string>? customIdentifiers)
    {
        return new IdentifierData
        {
            Type = "server",
            EnvId = secret.EnvId,
            ProjectKey = secret.ProjectKey,
            EnvKey = secret.EnvKey,
            // Server connections are identified by their credentials alone
            UserId = null,
            UserName = null,
            IpAddress = networkContext?.IpAddress,
            Host = networkContext?.Host,
            CustomIdentifiers = customIdentifiers
        };
    }

    private IdentifierData BuildRelayProxyIdentifier(Secret secret, NetworkContext? networkContext, Dictionary<string, string>? customIdentifiers)
    {
        return new IdentifierData
        {
            Type = "relay-proxy",
            EnvId = secret.EnvId,
            ProjectKey = secret.ProjectKey,
            EnvKey = secret.EnvKey,
            UserId = null,
            UserName = null,
            IpAddress = networkContext?.IpAddress,
            Host = networkContext?.Host,
            CustomIdentifiers = customIdentifiers
        };
    }

    private IdentifierData BuildGenericIdentifier(Secret secret, EndUser? user, string? connectionType, NetworkContext? networkContext, Dictionary<string, string>? customIdentifiers)
    {
        return new IdentifierData
        {
            Type = connectionType ?? "unknown",
            EnvId = secret.EnvId,
            ProjectKey = secret.ProjectKey,
            EnvKey = secret.EnvKey,
            UserId = user?.KeyId,
            UserName = user?.Name,
            IpAddress = networkContext?.IpAddress,
            Host = networkContext?.Host,
            CustomIdentifiers = customIdentifiers
        };
    }

    private string ComputeDeterministicHash(IdentifierData data)
    {
        // Serialize to consistent JSON
        var json = JsonSerializer.Serialize(data, _jsonOptions);
        
        // Create deterministic hash
        var bytes = Encoding.UTF8.GetBytes(json);
        var hash = SHA256.HashData(bytes);
        
        // Return first 16 characters of base64 for reasonable length
        return Convert.ToBase64String(hash)[..16].Replace("+", "-").Replace("/", "_");
    }

    private string? ComputePropertiesHash(CustomizedProperty[] properties)
    {
        try
        {
            // Sort properties for consistent hashing
            var sortedProps = properties
                .OrderBy(p => p.Name)
                .Select(p => new { p.Name, p.Value })
                .ToList();
                
            var json = JsonSerializer.Serialize(sortedProps, _jsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);
            var hash = SHA256.HashData(bytes);
            
            return Convert.ToBase64String(hash)[..8].Replace("+", "-").Replace("/", "_");
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region Data Models

    public class NetworkContext
    {
        public string? IpAddress { get; set; }
        public string? Host { get; set; }
    }

    private class IdentifierData
    {
        public string Type { get; set; } = string.Empty;
        public Guid EnvId { get; set; }
        public string ProjectKey { get; set; } = string.Empty;
        public string EnvKey { get; set; } = string.Empty;
        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public string? UserPropertiesHash { get; set; }
        public string? IpAddress { get; set; }
        public string? Host { get; set; }
        public Dictionary<string, string>? CustomIdentifiers { get; set; }
    }

    #endregion
} 