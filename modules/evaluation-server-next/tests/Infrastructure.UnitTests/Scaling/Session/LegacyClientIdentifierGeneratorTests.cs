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

    private Connection CreateTestConnection(string type, EndUser? user)
    {
        var mockWebSocket = new Mock<WebSocket>();
        var secret = new Secret
        {
            Type = type,
            EnvId = Guid.NewGuid(),
            ProjectKey = "test-project",
            EnvKey = "test-env"
        };
        
        var connection = new Connection(mockWebSocket.Object, secret);
        if (user != null)
        {
            connection.AttachUser(user);
        }
        
        return connection;
    }

    private ConnectionContext CreateTestConnectionContext(Connection connection)
    {
        return new TestConnectionContext(connection);
    }

    private class TestConnectionContext : ConnectionContext
    {
        private readonly Connection _connection;

        public TestConnectionContext(Connection connection)
        {
            _connection = connection;
            Connection = connection;
        }

        public override string? RawQuery => null;
        public override WebSocket WebSocket => _connection.WebSocket;
        public override string Type => _connection.Type;
        public override string Version => "1.0";
        public override string Token => $"{_connection.Secret.Type}-{_connection.Secret.ProjectKey}-{_connection.Secret.EnvKey}";
        public override Client? Client { get; protected set; }
        public override Connection Connection { get; protected set; }
        public override Connection[] MappedRpConnections { get; protected set; } = Array.Empty<Connection>();
        public override long ConnectAt => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public override long ClosedAt { get; protected set; }
    }
} 