using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
            
            return GenerateClientId(secret, user, connectionType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating client ID from connection context");
            throw;
        }
    }

    public string GenerateClientId(Secret secret, EndUser? user = null, string? connectionType = null)
    {
        try
        {
            // Build identifier components based on connection type
            var identifierData = connectionType?.ToLowerInvariant() switch
            {
                "client" => BuildClientSideIdentifier(secret, user),
                "server" => BuildServerSideIdentifier(secret),
                "relay-proxy" => BuildRelayProxyIdentifier(secret),
                _ => BuildGenericIdentifier(secret, user, connectionType)
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

    public bool IsLegacyGeneratedId(string clientId)
    {
        return !string.IsNullOrEmpty(clientId) && clientId.StartsWith(LEGACY_PREFIX, StringComparison.OrdinalIgnoreCase);
    }

    #region Private Methods

    private IdentifierData BuildClientSideIdentifier(Secret secret, EndUser? user)
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
                : null
        };
    }

    private IdentifierData BuildServerSideIdentifier(Secret secret)
    {
        return new IdentifierData
        {
            Type = "server",
            EnvId = secret.EnvId,
            ProjectKey = secret.ProjectKey,
            EnvKey = secret.EnvKey,
            // Server connections are identified by their credentials alone
            UserId = null,
            UserName = null
        };
    }

    private IdentifierData BuildRelayProxyIdentifier(Secret secret)
    {
        return new IdentifierData
        {
            Type = "relay-proxy",
            EnvId = secret.EnvId,
            ProjectKey = secret.ProjectKey,
            EnvKey = secret.EnvKey,
            UserId = null,
            UserName = null
        };
    }

    private IdentifierData BuildGenericIdentifier(Secret secret, EndUser? user, string? connectionType)
    {
        return new IdentifierData
        {
            Type = connectionType ?? "unknown",
            EnvId = secret.EnvId,
            ProjectKey = secret.ProjectKey,
            EnvKey = secret.EnvKey,
            UserId = user?.KeyId,
            UserName = user?.Name
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

    private class IdentifierData
    {
        public string Type { get; set; } = string.Empty;
        public Guid EnvId { get; set; }
        public string ProjectKey { get; set; } = string.Empty;
        public string EnvKey { get; set; } = string.Empty;
        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public string? UserPropertiesHash { get; set; }
    }

    #endregion
} 