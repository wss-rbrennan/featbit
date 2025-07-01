using Domain.EndUsers;
using Infrastructure.Protocol;
using Infrastructure.Scaling.Session;
using Infrastructure.Scaling.Service;
using Infrastructure.Scaling.Types;
using Microsoft.Extensions.Logging;
using Moq;

namespace Infrastructure.UnitTests.Scaling.Session;

public class ClientSessionManagerTests
{
    private readonly Mock<IClientSessionStore> _mockSessionStore;
    private readonly Mock<ISubscriptionManager> _mockSubscriptionManager;
    private readonly Mock<IServiceIdentityProvider> _mockServiceIdentity;
    private readonly Mock<ILogger<ClientSessionManager>> _mockLogger;
    private readonly ClientSessionManager _sessionManager;

    public ClientSessionManagerTests()
    {
        _mockSessionStore = new Mock<IClientSessionStore>();
        _mockSubscriptionManager = new Mock<ISubscriptionManager>();
        _mockServiceIdentity = new Mock<IServiceIdentityProvider>();
        _mockLogger = new Mock<ILogger<ClientSessionManager>>();

        _mockServiceIdentity.Setup(x => x.ServiceId).Returns("test-server-1");

        _sessionManager = new ClientSessionManager(
            _mockSessionStore.Object,
            _mockSubscriptionManager.Object,
            _mockServiceIdentity.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task HandleClientIdentifyAsync_NewSession_CreatesSessionSuccessfully()
    {
        // Arrange
        var connectionId = "conn-123";
        var clientId = "test-client-id";
        var envId = Guid.NewGuid();
        var user = new EndUser { KeyId = "user-123", Name = "John", CustomizedProperties = Array.Empty<CustomizedProperty>() };

        var identifyMessage = new ClientIdentifyMessage
        {
            ClientId = clientId,
            User = user,
            EnvId = envId
        };

        _mockSessionStore
            .Setup(x => x.CreateOrUpdateSessionAsync(user, envId, connectionId, "test-server-1"))
            .ReturnsAsync(clientId);

        _mockSessionStore
            .Setup(x => x.GetSessionAsync(clientId))
            .ReturnsAsync((ClientSession?)null); // No existing session

        // Act
        var result = await _sessionManager.HandleClientIdentifyAsync(connectionId, identifyMessage);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(clientId, result.ClientId);
        Assert.False(result.SessionResumed);
        Assert.Empty(result.RestoredChannels);
    }

    [Fact]
    public async Task HandleClientIdentifyAsync_ExistingSession_RestoresChannels()
    {
        // Arrange
        var connectionId = "conn-123";
        var clientId = "test-client-id";
        var envId = Guid.NewGuid();
        var user = new EndUser { KeyId = "user-123", Name = "John", CustomizedProperties = Array.Empty<CustomizedProperty>() };
        var existingChannels = new List<string> { "channel1", "channel2" };

        var identifyMessage = new ClientIdentifyMessage
        {
            ClientId = clientId,
            User = user,
            EnvId = envId
        };

        var existingSession = new ClientSession
        {
            ClientId = clientId,
            SubscribedChannels = existingChannels
        };

        _mockSessionStore
            .Setup(x => x.CreateOrUpdateSessionAsync(user, envId, connectionId, "test-server-1"))
            .ReturnsAsync(clientId);

        _mockSessionStore
            .Setup(x => x.GetSessionAsync(clientId))
            .ReturnsAsync(existingSession);

        // Act
        var result = await _sessionManager.HandleClientIdentifyAsync(connectionId, identifyMessage);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(clientId, result.ClientId);
        Assert.True(result.SessionResumed);
        Assert.Equal(2, result.RestoredChannels.Count);
        Assert.Contains("channel1", result.RestoredChannels);
        Assert.Contains("channel2", result.RestoredChannels);

        // Verify subscription manager calls
        _mockSubscriptionManager.Verify(x => x.AddChannelToSubscription(connectionId, "channel1"), Times.Once);
        _mockSubscriptionManager.Verify(x => x.AddChannelToSubscription(connectionId, "channel2"), Times.Once);
    }

    [Fact]
    public async Task HandleClientIdentifyAsync_SessionStoreFailure_ReturnsError()
    {
        // Arrange
        var connectionId = "conn-123";
        var clientId = "test-client-id";
        var envId = Guid.NewGuid();
        var user = new EndUser { KeyId = "user-123", Name = "John", CustomizedProperties = Array.Empty<CustomizedProperty>() };

        var identifyMessage = new ClientIdentifyMessage
        {
            ClientId = clientId,
            User = user,
            EnvId = envId
        };

        _mockSessionStore
            .Setup(x => x.CreateOrUpdateSessionAsync(user, envId, connectionId, "test-server-1"))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await _sessionManager.HandleClientIdentifyAsync(connectionId, identifyMessage);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Internal server error during identification", result.Error);
    }

    [Fact]
    public async Task AddChannelSubscriptionAsync_ValidConnection_AddsChannelSuccessfully()
    {
        // Arrange
        var connectionId = "conn-123";
        var clientId = "test-client-id";
        var channel = "test-channel";

        // Setup connection mapping
        await SetupConnectionMapping(connectionId, clientId);

        _mockSessionStore
            .Setup(x => x.AddChannelSubscriptionAsync(clientId, channel))
            .ReturnsAsync(true);

        // Act
        var result = await _sessionManager.AddChannelSubscriptionAsync(connectionId, channel);

        // Assert
        Assert.True(result);
        _mockSubscriptionManager.Verify(x => x.AddChannelToSubscription(connectionId, channel), Times.Once);
        _mockSessionStore.Verify(x => x.AddChannelSubscriptionAsync(clientId, channel), Times.Once);
    }

    [Fact]
    public async Task AddChannelSubscriptionAsync_UnknownConnection_StillSucceeds()
    {
        // Arrange
        var connectionId = "unknown-conn";
        var channel = "test-channel";

        // Act
        var result = await _sessionManager.AddChannelSubscriptionAsync(connectionId, channel);

        // Assert - The method always succeeds for subscription service, even for unknown connections
        Assert.True(result);
        _mockSubscriptionManager.Verify(x => x.AddChannelToSubscription(connectionId, channel), Times.Once);
    }

    [Fact]
    public async Task RemoveChannelSubscriptionAsync_ValidConnection_RemovesChannelSuccessfully()
    {
        // Arrange
        var connectionId = "conn-123";
        var clientId = "test-client-id";
        var channel = "test-channel";

        // Setup connection mapping
        await SetupConnectionMapping(connectionId, clientId);

        _mockSessionStore
            .Setup(x => x.RemoveChannelSubscriptionAsync(clientId, channel))
            .ReturnsAsync(true);

        // Act
        var result = await _sessionManager.RemoveChannelSubscriptionAsync(connectionId, channel);

        // Assert
        Assert.True(result);
        _mockSubscriptionManager.Verify(x => x.RemoveChannelFromSubscription(connectionId, channel), Times.Once);
        _mockSessionStore.Verify(x => x.RemoveChannelSubscriptionAsync(clientId, channel), Times.Once);
    }

    [Fact]
    public async Task GetSessionByConnectionAsync_ValidConnection_ReturnsSession()
    {
        // Arrange
        var connectionId = "conn-123";
        var clientId = "test-client-id";
        var expectedSession = new ClientSession { ClientId = clientId };

        // Setup connection mapping
        await SetupConnectionMapping(connectionId, clientId);

        _mockSessionStore
            .Setup(x => x.GetSessionAsync(clientId))
            .ReturnsAsync(expectedSession);

        // Act
        var result = await _sessionManager.GetSessionByConnectionAsync(connectionId);

        // Assert
        Assert.Equal(expectedSession, result);
    }

    [Fact]
    public async Task GetSessionByConnectionAsync_UnknownConnection_ReturnsNull()
    {
        // Arrange
        var connectionId = "unknown-conn";

        // Act
        var result = await _sessionManager.GetSessionByConnectionAsync(connectionId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task HandleClientDisconnectAsync_ValidConnection_CleansUpSuccessfully()
    {
        // Arrange
        var connectionId = "conn-123";
        var clientId = "test-client-id";

        // Setup connection mapping
        await SetupConnectionMapping(connectionId, clientId);

        // Act
        await _sessionManager.HandleClientDisconnectAsync(connectionId);

        // Assert
        _mockSubscriptionManager.Verify(x => x.RemoveSubscription(connectionId), Times.Once);
        // Note: Session persists in store for potential reconnection - not removed on disconnect
    }

    [Fact]
    public async Task SendToClientAsync_ValidClient_SendsSuccessfully()
    {
        // Arrange
        var connectionId = "conn-123";
        var clientId = "test-client-id";
        var message = "test-message";

        // Setup connection mapping
        await SetupConnectionMapping(connectionId, clientId);

        _mockSubscriptionManager
            .Setup(x => x.BroadcastToSubscriberAsync(connectionId, message))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sessionManager.SendToClientAsync(clientId, message);

        // Assert
        Assert.True(result);
        _mockSubscriptionManager.Verify(x => x.BroadcastToSubscriberAsync(connectionId, message), Times.Once);
    }

    [Fact]
    public async Task GetActiveSessionsAsync_ReturnsActiveSessions()
    {
        // Arrange
        var expectedSessions = new List<ClientSession>
        {
            new ClientSession { ClientId = "client1" },
            new ClientSession { ClientId = "client2" }
        };

        _mockSessionStore
            .Setup(x => x.GetSessionsByServerAsync("test-server-1"))
            .ReturnsAsync(expectedSessions);

        // Act
        var result = await _sessionManager.GetActiveSessionsAsync();

        // Assert
        Assert.Equal(expectedSessions, result);
        _mockSessionStore.Verify(x => x.GetSessionsByServerAsync("test-server-1"), Times.Once);
    }

    [Fact]
    public async Task HandleClientIdentifyAsync_NullClientId_GeneratesClientId()
    {
        // Arrange
        var connectionId = "conn-123";
        var envId = Guid.NewGuid();
        var user = new EndUser { KeyId = "user-123", Name = "John", CustomizedProperties = Array.Empty<CustomizedProperty>() };

        var identifyMessage = new ClientIdentifyMessage
        {
            ClientId = null, // Let system generate
            User = user,
            EnvId = envId
        };

        var generatedClientId = $"client:{user.KeyId}:env:{envId:N}";

        _mockSessionStore
            .Setup(x => x.CreateOrUpdateSessionAsync(user, envId, connectionId, "test-server-1"))
            .ReturnsAsync(generatedClientId);

        _mockSessionStore
            .Setup(x => x.GetSessionAsync(generatedClientId))
            .ReturnsAsync((ClientSession?)null);

        // Act
        var result = await _sessionManager.HandleClientIdentifyAsync(connectionId, identifyMessage);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(generatedClientId, result.ClientId);
        Assert.False(result.SessionResumed);
    }

    [Fact]
    public async Task HandleClientIdentifyAsync_EmptyClientId_GeneratesClientId()
    {
        // Arrange
        var connectionId = "conn-123";
        var envId = Guid.NewGuid();
        var user = new EndUser { KeyId = "user-123", Name = "John", CustomizedProperties = Array.Empty<CustomizedProperty>() };

        var identifyMessage = new ClientIdentifyMessage
        {
            ClientId = string.Empty, // Empty string
            User = user,
            EnvId = envId
        };

        var generatedClientId = $"client:{user.KeyId}:env:{envId:N}";

        _mockSessionStore
            .Setup(x => x.CreateOrUpdateSessionAsync(user, envId, connectionId, "test-server-1"))
            .ReturnsAsync(generatedClientId);

        _mockSessionStore
            .Setup(x => x.GetSessionAsync(generatedClientId))
            .ReturnsAsync((ClientSession?)null);

        // Act
        var result = await _sessionManager.HandleClientIdentifyAsync(connectionId, identifyMessage);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(generatedClientId, result.ClientId);
        Assert.False(result.SessionResumed);
    }

    [Fact] 
    public async Task HandleClientIdentifyAsync_SessionStoreReturnsNull_HandlesGracefully()
    {
        // Arrange
        var connectionId = "conn-123";
        var clientId = "test-client-id";
        var envId = Guid.NewGuid();
        var user = new EndUser { KeyId = "user-123", Name = "John", CustomizedProperties = Array.Empty<CustomizedProperty>() };

        var identifyMessage = new ClientIdentifyMessage
        {
            ClientId = clientId,
            User = user,
            EnvId = envId
        };

        _mockSessionStore
            .Setup(x => x.CreateOrUpdateSessionAsync(user, envId, connectionId, "test-server-1"))
            .ReturnsAsync(default(string)); // Session creation failed

        // Act
        var result = await _sessionManager.HandleClientIdentifyAsync(connectionId, identifyMessage);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Internal server error during identification", result.Error);
    }

    #region Helper Methods

    private async Task SetupConnectionMapping(string connectionId, string clientId)
    {
        // Simulate a successful identify to establish connection mapping
        var envId = Guid.NewGuid();
        var user = new EndUser { KeyId = "user-123", Name = "John", CustomizedProperties = Array.Empty<CustomizedProperty>() };

        var identifyMessage = new ClientIdentifyMessage
        {
            ClientId = clientId,
            User = user,
            EnvId = envId
        };

        _mockSessionStore
            .Setup(x => x.CreateOrUpdateSessionAsync(user, envId, connectionId, "test-server-1"))
            .ReturnsAsync(clientId);

        _mockSessionStore
            .Setup(x => x.GetSessionAsync(clientId))
            .ReturnsAsync((ClientSession?)null);

        await _sessionManager.HandleClientIdentifyAsync(connectionId, identifyMessage);
    }

    #endregion
} 