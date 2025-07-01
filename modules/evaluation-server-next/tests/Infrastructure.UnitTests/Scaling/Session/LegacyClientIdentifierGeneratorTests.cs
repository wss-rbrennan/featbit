using System.Net.WebSockets;
using Domain.EndUsers;
using Domain.Shared;
using Infrastructure.Connections;
using Infrastructure.Protocol;
using Infrastructure.Scaling.Session;
using Microsoft.Extensions.Logging;
using Moq;

namespace Infrastructure.UnitTests.Scaling.Session;

public class LegacyClientIdentifierGeneratorTests
{
    private readonly Mock<ILogger<LegacyClientIdentifierGenerator>> _mockLogger;
    private readonly LegacyClientIdentifierGenerator _generator;

    public LegacyClientIdentifierGeneratorTests()
    {
        _mockLogger = new Mock<ILogger<LegacyClientIdentifierGenerator>>();
        _generator = new LegacyClientIdentifierGenerator(_mockLogger.Object);
    }

    [Fact]
    public void GenerateClientId_SameInputs_ReturnsSameId()
    {
        // Arrange
        var secret = new Secret("client", "webapp", Guid.NewGuid(), "dev");
        var user = new EndUser 
        { 
            KeyId = "user-123", 
            Name = "John Doe",
            CustomizedProperties = Array.Empty<CustomizedProperty>()
        };

        // Act
        var id1 = _generator.GenerateClientId(secret, user, "client");
        var id2 = _generator.GenerateClientId(secret, user, "client");

        // Assert
        Assert.Equal(id1, id2);
    }

    [Fact]
    public void GenerateClientId_DifferentUsers_ReturnsDifferentIds()
    {
        // Arrange
        var secret = new Secret("client", "webapp", Guid.NewGuid(), "dev");
        var user1 = new EndUser { KeyId = "user-123", Name = "John", CustomizedProperties = Array.Empty<CustomizedProperty>() };
        var user2 = new EndUser { KeyId = "user-456", Name = "Jane", CustomizedProperties = Array.Empty<CustomizedProperty>() };

        // Act
        var id1 = _generator.GenerateClientId(secret, user1, "client");
        var id2 = _generator.GenerateClientId(secret, user2, "client");

        // Assert
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void GenerateClientId_DifferentEnvironments_ReturnsDifferentIds()
    {
        // Arrange
        var env1 = Guid.NewGuid();
        var env2 = Guid.NewGuid();
        var secret1 = new Secret("client", "webapp", env1, "dev");
        var secret2 = new Secret("client", "webapp", env2, "dev");
        var user = new EndUser { KeyId = "user-123", Name = "John", CustomizedProperties = Array.Empty<CustomizedProperty>() };

        // Act
        var id1 = _generator.GenerateClientId(secret1, user, "client");
        var id2 = _generator.GenerateClientId(secret2, user, "client");

        // Assert
        Assert.NotEqual(id1, id2);
    }

    [Theory]
    [InlineData("client")]
    [InlineData("server")]
    [InlineData("relay-proxy")]
    public void GenerateClientId_DifferentConnectionTypes_ReturnsCorrectPrefix(string connectionType)
    {
        // Arrange
        var secret = new Secret(connectionType, "webapp", Guid.NewGuid(), "dev");
        var user = connectionType == "client" 
            ? new EndUser { KeyId = "user-123", Name = "John", CustomizedProperties = Array.Empty<CustomizedProperty>() }
            : null;

        // Act
        var clientId = _generator.GenerateClientId(secret, user, connectionType);

        // Assert
        Assert.StartsWith($"legacy:{connectionType}:", clientId);
        Assert.Contains($"env:{secret.EnvId:N}", clientId);
    }

    [Fact]
    public void GenerateClientId_WithCustomProperties_IsDeterministic()
    {
        // Arrange
        var secret = new Secret("client", "webapp", Guid.NewGuid(), "dev");
        var user = new EndUser 
        { 
            KeyId = "user-123", 
            Name = "John",
            CustomizedProperties = new[]
            {
                new CustomizedProperty { Name = "email", Value = "john@example.com" },
                new CustomizedProperty { Name = "role", Value = "admin" }
            }
        };

        // Act
        var id1 = _generator.GenerateClientId(secret, user, "client");
        var id2 = _generator.GenerateClientId(secret, user, "client");

        // Assert
        Assert.Equal(id1, id2);
    }

    [Fact]
    public void GenerateClientId_DifferentCustomProperties_ReturnsDifferentIds()
    {
        // Arrange
        var secret = new Secret("client", "webapp", Guid.NewGuid(), "dev");
        var user1 = new EndUser 
        { 
            KeyId = "user-123", 
            Name = "John",
            CustomizedProperties = new[]
            {
                new CustomizedProperty { Name = "role", Value = "admin" }
            }
        };
        var user2 = new EndUser 
        { 
            KeyId = "user-123", 
            Name = "John",
            CustomizedProperties = new[]
            {
                new CustomizedProperty { Name = "role", Value = "user" }
            }
        };

        // Act
        var id1 = _generator.GenerateClientId(secret, user1, "client");
        var id2 = _generator.GenerateClientId(secret, user2, "client");

        // Assert
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void IsLegacyGeneratedId_WithLegacyId_ReturnsTrue()
    {
        // Arrange
        var legacyId = "legacy:client:abc123:env:def456";

        // Act
        var result = _generator.IsLegacyGeneratedId(legacyId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsLegacyGeneratedId_WithNonLegacyId_ReturnsFalse()
    {
        // Arrange
        var nonLegacyId = "explicit:client:abc123";

        // Act
        var result = _generator.IsLegacyGeneratedId(nonLegacyId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ExtractUser_WithConnectionContextAndUser_ReturnsUser()
    {
        // Arrange
        var user = new EndUser { KeyId = "user-123", Name = "John Doe" };
        var connection = CreateTestConnection("client", user);
        var connectionContext = CreateTestConnectionContext(connection);

        // Act
        var result = _generator.ExtractUser(connectionContext);

        // Assert
        Assert.Equal(user, result);
    }

    [Fact]
    public void ExtractUser_WithConnectionContextNoUser_ReturnsNull()
    {
        // Arrange
        var connection = CreateTestConnection("client", null);
        var connectionContext = CreateTestConnectionContext(connection);

        // Act
        var result = _generator.ExtractUser(connectionContext);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GenerateClientId_FromConnectionContext_Success()
    {
        // Arrange
        var user = new EndUser { KeyId = "user-123", Name = "Test User" };
        var connection = CreateTestConnection("client", user);
        var connectionContext = CreateTestConnectionContext(connection);

        // Act
        var result = _generator.GenerateClientId(connectionContext);

        // Assert
        Assert.NotNull(result);
        Assert.StartsWith("legacy:client:", result);
        Assert.Contains("env:", result);
    }

    [Fact]
    public void GenerateClientId_FromConnectionContextNullSecret_ThrowsException()
    {
        // Arrange
        var mockContext = new Mock<ConnectionContext>();
        mockContext.Setup(x => x.Connection).Returns((Connection?)null);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _generator.GenerateClientId(mockContext.Object));
    }

    [Fact]
    public void GenerateClientId_WithNetworkContext_CreatesUniqueIds()
    {
        // Arrange
        var secret = CreateTestSecret("server");
        var networkContext1 = new LegacyClientIdentifierGenerator.NetworkContext
        {
            IpAddress = "192.168.1.10",
            Host = "server1.example.com"
        };
        var networkContext2 = new LegacyClientIdentifierGenerator.NetworkContext
        {
            IpAddress = "192.168.1.11",
            Host = "server2.example.com"
        };

        // Act
        var clientId1 = _generator.GenerateClientId(secret, null, "server", networkContext1, null);
        var clientId2 = _generator.GenerateClientId(secret, null, "server", networkContext2, null);

        // Assert
        Assert.NotEqual(clientId1, clientId2);
        Assert.Contains("legacy:server:", clientId1);
        Assert.Contains("legacy:server:", clientId2);
    }

    [Fact]
    public void GenerateClientId_WithCustomIdentifiers_CreatesUniqueIds()
    {
        // Arrange
        var secret = CreateTestSecret("server");
        var customIds1 = new Dictionary<string, string>
        {
            ["instanceId"] = "instance-1",
            ["region"] = "us-east-1"
        };
        var customIds2 = new Dictionary<string, string>
        {
            ["instanceId"] = "instance-2",
            ["region"] = "us-west-2"
        };

        // Act
        var clientId1 = _generator.GenerateClientId(secret, null, "server", null, customIds1);
        var clientId2 = _generator.GenerateClientId(secret, null, "server", null, customIds2);

        // Assert
        Assert.NotEqual(clientId1, clientId2);
        Assert.Contains("legacy:server:", clientId1);
        Assert.Contains("legacy:server:", clientId2);
    }

    [Fact]
    public void GenerateClientId_WithNetworkAndCustomIdentifiers_CreatesUniqueIds()
    {
        // Arrange
        var secret = CreateTestSecret("relay-proxy");
        var networkContext = new LegacyClientIdentifierGenerator.NetworkContext
        {
            IpAddress = "10.0.0.5",
            Host = "relay.example.com"
        };
        var customIds = new Dictionary<string, string>
        {
            ["instanceId"] = "relay-instance-1",
            ["datacenter"] = "dc1"
        };

        // Act
        var clientId1 = _generator.GenerateClientId(secret, null, "relay-proxy", networkContext, customIds);
        var clientId2 = _generator.GenerateClientId(secret, null, "relay-proxy", null, null);

        // Assert
        Assert.NotEqual(clientId1, clientId2);
        Assert.Contains("legacy:relay-proxy:", clientId1);
        Assert.Contains("legacy:relay-proxy:", clientId2);
    }

    [Fact]
    public void ExtractNetworkContext_WithValidClient_ReturnsNetworkContext()
    {
        // Arrange
        var user = new EndUser { KeyId = "user-123", Name = "Test User" };
        var connection = CreateTestConnection("server", user);
        var connectionContext = CreateTestConnectionContext(connection);

        // Act
        var result = _generator.ExtractNetworkContext(connectionContext);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("192.168.1.100", result.IpAddress);
        Assert.Equal("test-host", result.Host);
    }

    [Fact]
    public void ExtractCustomIdentifiers_WithQueryParameters_ReturnsCustomIdentifiers()
    {
        // Arrange
        var user = new EndUser { KeyId = "user-123", Name = "Test User" };
        var connection = CreateTestConnection("server", user);
        var connectionContext = CreateTestConnectionContextWithQuery(connection, "instanceId=server-1&region=us-east-1&datacenter=dc1");

        // Act
        var result = _generator.ExtractCustomIdentifiers(connectionContext);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("server-1", result["instanceId"]);
        Assert.Equal("us-east-1", result["region"]);
        Assert.Equal("dc1", result["datacenter"]);
    }

    [Fact]
    public void ExtractCustomIdentifiers_WithNoQueryParameters_ReturnsNull()
    {
        // Arrange
        var user = new EndUser { KeyId = "user-123", Name = "Test User" };
        var connection = CreateTestConnection("server", user);
        var connectionContext = CreateTestConnectionContextWithQuery(connection, null);

        // Act
        var result = _generator.ExtractCustomIdentifiers(connectionContext);

        // Assert
        Assert.Null(result);
    }

    private Connection CreateTestConnection(string type, EndUser? user)
    {
        var mockWebSocket = new Mock<WebSocket>();
        var secret = CreateTestSecret(type);
        
        var connection = new Connection(mockWebSocket.Object, secret);
        if (user != null)
        {
            connection.AttachUser(user);
        }
        
        return connection;
    }

    private Secret CreateTestSecret(string type)
    {
        return new Secret
        {
            Type = type,
            EnvId = Guid.NewGuid(),
            ProjectKey = "test-project",
            EnvKey = "test-env"
        };
    }

    private ConnectionContext CreateTestConnectionContext(Connection connection)
    {
        return new TestConnectionContext(connection);
    }

    private ConnectionContext CreateTestConnectionContextWithQuery(Connection connection, string? query)
    {
        return new TestConnectionContext(connection, query);
    }

    private class TestConnectionContext : ConnectionContext
    {
        private readonly Connection _connection;
        private readonly string? _rawQuery;

        public TestConnectionContext(Connection connection, string? rawQuery = null)
        {
            _connection = connection;
            _rawQuery = rawQuery;
            Connection = connection;
            Client = new Client("192.168.1.100", "test-host");
        }

        public override string? RawQuery => _rawQuery;

        public override WebSocket WebSocket => _connection.WebSocket;

        public override string Type => _connection.Secret.Type;

        public override string Version => "1.0";

        public override string Token => $"{_connection.Secret.Type}-{_connection.Secret.ProjectKey}-{_connection.Secret.EnvKey}";

        public override Client? Client { get; protected set; }

        public override Connection Connection { get; protected set; }

        public override Connection[] MappedRpConnections { get; protected set; } = Array.Empty<Connection>();

        public override long ConnectAt => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public override long ClosedAt { get; protected set; }
    }
} 