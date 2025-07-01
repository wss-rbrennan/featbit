using System.Net.WebSockets;
using Domain.Shared;
using DataStore.Caches;
using DataStore.Persistence;
using Infrastructure.Providers;
using Infrastructure.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Internal;
using Testcontainers.Redis;
using DataStore.Caches.Redis;
using Infrastructure.MQ;
using Domain.Messages;
using Infrastructure.Channels;
using Application.Services;
using Infrastructure.BackplaneMesssages;
using Infrastructure.Scaling.Handlers;
using Moq;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Application.IntegrationTests;

public class TestApp : WebApplicationFactory<Program>
{

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Configure for fake/mock services (Redis integration can be added later)
        builder.UseSetting("DbProvider:Name", DbProvider.Fake);
        builder.UseSetting("BackplaneProvider:Name", BackplaneProvider.None);
        builder.UseSetting("CacheProvider:Name", CacheProvider.None);

        builder.ConfigureTestServices(services =>
        {
            // Replace problematic services with mocks for fake mode
            ReplaceProblemServicesWithMocks(services);

            // Always replace system clock for deterministic tests
            services.Replace(ServiceDescriptor.Singleton<ISystemClock>(new TestClock(0)));
        });

        base.ConfigureWebHost(builder);
    }

    private static void ReplaceProblemServicesWithMocks(IServiceCollection services)
    {
        // Remove Redis-dependent services
        RemoveService<IRedisClient>(services);
        RemoveService<IChannelPublisher>(services);
        RemoveService<IChannelProducer>(services);
        RemoveService<IMessageProducer>(services);
        RemoveService<IMessageHandler>(services);
        RemoveService<IDataSyncService>(services);
        RemoveService<Domain.Shared.IStore>(services);

        // Add mock implementations
        var mockRedisClient = new Mock<IRedisClient>();
        services.AddSingleton(mockRedisClient.Object);

        var mockChannelPublisher = new Mock<IChannelPublisher>();
        services.AddSingleton(mockChannelPublisher.Object);

        var mockChannelProducer = new Mock<IChannelProducer>();
        services.AddSingleton(mockChannelProducer.Object);

        var mockMessageProducer = new Mock<IMessageProducer>();
        services.AddSingleton(mockMessageProducer.Object);

        var mockMessageHandler = new Mock<IMessageHandler>();
        services.AddSingleton(mockMessageHandler.Object);

        var mockDataSyncService = new Mock<IDataSyncService>();
        services.AddSingleton(mockDataSyncService.Object);

        var mockStore = new Mock<Domain.Shared.IStore>();
        mockStore.Setup(x => x.Name).Returns("FakeStore");
        mockStore.Setup(x => x.IsAvailableAsync()).ReturnsAsync(true);
        mockStore.Setup(x => x.GetFlagsAsync(It.IsAny<Guid>(), It.IsAny<long>())).ReturnsAsync(Array.Empty<byte[]>());
        mockStore.Setup(x => x.GetFlagsAsync(It.IsAny<IEnumerable<string>>())).ReturnsAsync(Array.Empty<byte[]>());
        mockStore.Setup(x => x.GetSegmentAsync(It.IsAny<string>())).ReturnsAsync(Array.Empty<byte>());
        mockStore.Setup(x => x.GetSegmentsAsync(It.IsAny<Guid>(), It.IsAny<long>())).ReturnsAsync(Array.Empty<byte[]>());
        // Return proper test secrets for authentication
        mockStore.Setup(x => x.GetSecretAsync(It.IsAny<string>()))
            .ReturnsAsync((string secretString) => Domain.Shared.TestData.GetSecret(secretString));
        services.AddSingleton(mockStore.Object);

        // Remove problematic hosted services
        RemoveHostedServices(services);
    }

    private static void RemoveService<T>(IServiceCollection services)
    {
        var serviceDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(T));
        if (serviceDescriptor != null)
        {
            services.Remove(serviceDescriptor);
        }
    }

    private static void RemoveHostedServices(IServiceCollection services)
    {
        // Remove Redis message consumer hosted service
        var hostedServicesToRemove = services
            .Where(d => d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService))
            .Where(d => d.ImplementationType?.Name.Contains("Redis") == true)
            .ToList();

        foreach (var service in hostedServicesToRemove)
        {
            services.Remove(service);
        }
    }

    public async Task<WebSocket> ConnectAsync(long timestamp = 0, string queryString = "")
    {
        var streamingApp = WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(collection =>
            {
                collection.Replace(ServiceDescriptor.Singleton<ISystemClock>(new TestClock(timestamp)));
            });
        });

        var client = streamingApp.Server.CreateWebSocketClient();
        var streamingUri = new Uri($"http://localhost/streaming{queryString}");

        var ws = await client.ConnectAsync(streamingUri, CancellationToken.None);
        return ws;
    }

    public async Task<WebSocket> ConnectWithTokenAsync(string type = "client")
    {
        var (tokenCreatedAt, token) = type switch
        {
            ConnectionType.Client => (TestData.ClientToken.Timestamp, TestData.ClientTokenString),
            ConnectionType.Server => (TestData.ServerToken.Timestamp, TestData.ServerTokenString),
            ConnectionType.RelayProxy => (0, TestData.RelayProxyTokenString),
            _ => throw new ArgumentException("Invalid connection type", nameof(type))
        };

        return await ConnectAsync(tokenCreatedAt, $"?type={type}&version=2&token={token}");
    }
}