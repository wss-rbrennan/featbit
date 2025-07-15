using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Internal;
using Moq;
using System.Net.WebSockets;
using System.Text;

namespace Streaming.IntegrationTests;

public class SimpleWebSocketTest : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SimpleWebSocketTest(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task WebSocket_Echo_ShouldWork()
    {
        // Arrange - Mock system clock and store for authentication
        var testFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                // Mock system clock to token timestamp to prevent expiration
                services.Replace(ServiceDescriptor.Singleton<ISystemClock>(new TestClock(Domain.Shared.TestData.ClientToken.Timestamp)));
                
                // Mock store to provide test secrets for authentication
                var mockStore = new Mock<Domain.Shared.IStore>();
                mockStore.Setup(x => x.Name).Returns("TestStore");
                mockStore.Setup(x => x.IsAvailableAsync()).ReturnsAsync(true);
                mockStore.Setup(x => x.GetFlagsAsync(It.IsAny<Guid>(), It.IsAny<long>())).ReturnsAsync(Array.Empty<byte[]>());
                mockStore.Setup(x => x.GetFlagsAsync(It.IsAny<IEnumerable<string>>())).ReturnsAsync(Array.Empty<byte[]>());
                mockStore.Setup(x => x.GetSegmentAsync(It.IsAny<string>())).ReturnsAsync(Array.Empty<byte>());
                mockStore.Setup(x => x.GetSegmentsAsync(It.IsAny<Guid>(), It.IsAny<long>())).ReturnsAsync(Array.Empty<byte[]>());
                mockStore.Setup(x => x.GetSecretAsync(It.IsAny<string>()))
                    .ReturnsAsync((string secretString) => Domain.Shared.TestData.GetSecret(secretString));
                services.Replace(ServiceDescriptor.Singleton(mockStore.Object));
            });
        });

        var client = testFactory.Server.CreateWebSocketClient();
        using var webSocket = await client.ConnectAsync(new Uri("ws://localhost/streaming?type=client&version=2&token=" + Domain.Shared.TestData.ClientTokenString), CancellationToken.None);

        // Act - Send echo message
        var echoMessage = "{\"messageType\":\"echo\",\"data\":{}}";
        var messageBytes = Encoding.UTF8.GetBytes(echoMessage);
        await webSocket.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true, CancellationToken.None);

        // Assert - Receive echo response
        var buffer = new byte[1024];
        var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        
        Assert.Equal(WebSocketMessageType.Text, result.MessageType);
        Assert.True(result.EndOfMessage);
        
        var responseMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
        Assert.Equal(echoMessage, responseMessage);
    }

    [Fact] 
    public async Task WebSocket_Ping_ShouldReturnPong()
    {
        // Arrange - Mock system clock and store for authentication
        var testFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                // Mock system clock to token timestamp to prevent expiration
                services.Replace(ServiceDescriptor.Singleton<ISystemClock>(new TestClock(Domain.Shared.TestData.ClientToken.Timestamp)));
                
                // Mock store to provide test secrets for authentication
                var mockStore = new Mock<Domain.Shared.IStore>();
                mockStore.Setup(x => x.Name).Returns("TestStore");
                mockStore.Setup(x => x.IsAvailableAsync()).ReturnsAsync(true);
                mockStore.Setup(x => x.GetFlagsAsync(It.IsAny<Guid>(), It.IsAny<long>())).ReturnsAsync(Array.Empty<byte[]>());
                mockStore.Setup(x => x.GetFlagsAsync(It.IsAny<IEnumerable<string>>())).ReturnsAsync(Array.Empty<byte[]>());
                mockStore.Setup(x => x.GetSegmentAsync(It.IsAny<string>())).ReturnsAsync(Array.Empty<byte>());
                mockStore.Setup(x => x.GetSegmentsAsync(It.IsAny<Guid>(), It.IsAny<long>())).ReturnsAsync(Array.Empty<byte[]>());
                mockStore.Setup(x => x.GetSecretAsync(It.IsAny<string>()))
                    .ReturnsAsync((string secretString) => Domain.Shared.TestData.GetSecret(secretString));
                services.Replace(ServiceDescriptor.Singleton(mockStore.Object));
            });
        });

        var client = testFactory.Server.CreateWebSocketClient();
        using var webSocket = await client.ConnectAsync(new Uri("ws://localhost/streaming?type=client&version=2&token=" + Domain.Shared.TestData.ClientTokenString), CancellationToken.None);

        // Act - Send ping message
        var pingMessage = "{\"messageType\":\"ping\",\"data\":{}}";
        var messageBytes = Encoding.UTF8.GetBytes(pingMessage);
        await webSocket.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true, CancellationToken.None);

        // Assert - Receive pong response
        var buffer = new byte[1024];
        var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        
        Assert.Equal(WebSocketMessageType.Text, result.MessageType);
        Assert.True(result.EndOfMessage);
        
        var responseMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
        Assert.Contains("pong", responseMessage);
    }
}

internal class TestClock(long timestampMillis) : ISystemClock
{
    public DateTimeOffset UtcNow { get; } = DateTimeOffset.FromUnixTimeMilliseconds(timestampMillis);
} 