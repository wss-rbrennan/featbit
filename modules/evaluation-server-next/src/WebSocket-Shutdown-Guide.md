# WebSocket Graceful Shutdown Guide

This guide explains how the Edge service handles graceful shutdown of WebSocket connections when the application receives a shutdown signal.

## Overview

The WebSocket shutdown functionality ensures that all active WebSocket connections are properly closed when the application is shutting down, providing a better user experience and preventing connection leaks.

## Components

### 1. WebSocketShutdownService

A dedicated hosted service that coordinates the shutdown process:

- **Purpose**: Listens for application shutdown signals and triggers WebSocket disconnection
- **Registration**: Automatically registered as a hosted service in the DI container
- **Lifecycle**: Starts with the application and registers for shutdown events

### 2. Enhanced SubscriptionService

The `SubscriptionService` now includes a `DisconnectAllAsync` method that:

- Gracefully closes all active WebSocket connections
- Uses parallel processing for efficient disconnection
- Includes proper error handling and logging
- Provides configurable close status and description

### 3. Updated WebSocketService

The `WebSocketService` now properly handles shutdown in its `StopAsync` method:

- Cancels ongoing operations
- Triggers disconnection of all WebSocket connections
- Provides comprehensive logging

## How It Works

### Automatic Shutdown (Recommended)

When the application receives a shutdown signal (SIGTERM, SIGINT, etc.):

1. **Signal Detection**: `IHostApplicationLifetime.ApplicationStopping` is triggered
2. **Service Coordination**: `WebSocketShutdownService` detects the shutdown signal
3. **Connection Cleanup**: All WebSocket connections are gracefully closed
4. **Logging**: Comprehensive logging tracks the shutdown process

### Manual Shutdown

You can also manually trigger WebSocket disconnection:

```csharp
// Using the shutdown service
var shutdownService = serviceProvider.GetRequiredService<WebSocketShutdownService>();
await shutdownService.DisconnectAllWebSocketsAsync(
    WebSocketCloseStatus.NormalClosure,
    "Manual shutdown requested"
);

// Using the subscription service directly
var subscriptionService = serviceProvider.GetRequiredService<ISubscriptionService>();
await subscriptionService.DisconnectAllAsync(
    WebSocketCloseStatus.PolicyViolation,
    "Policy violation detected"
);
```

## Configuration

### Service Registration

The shutdown functionality is automatically registered when you use the streaming services:

```csharp
services
    .AddStreamingCore()
    .UseScaling(); // This registers WebSocketShutdownService
```

### Timeout Configuration

The shutdown process includes a 30-second timeout for disconnecting all connections. This can be modified in the `SubscriptionService.DisconnectAllAsync` method if needed.

## Logging

The shutdown process provides comprehensive logging:

- **Information**: Connection counts, shutdown initiation/completion
- **Debug**: Individual connection closure details
- **Warning**: Timeout or connection state issues
- **Error**: Unexpected errors during shutdown

Example log output:
```
[INFO] WebSocket shutdown initiated - disconnecting 15 connections
[DEBUG] Closing WebSocket connection abc123 due to shutdown
[DEBUG] Closing WebSocket connection def456 due to shutdown
[INFO] WebSocket shutdown completed successfully
```

## Error Handling

The implementation includes robust error handling:

- **WebSocket Exceptions**: Logged as warnings, don't stop the shutdown process
- **Timeout Handling**: 30-second timeout prevents hanging during shutdown
- **State Validation**: Only attempts to close connections in appropriate states
- **Cleanup Guarantee**: Subscriptions are removed from tracking even if closure fails

## Best Practices

1. **Let It Handle Automatically**: The system automatically handles shutdown - no manual intervention needed
2. **Monitor Logs**: Use the structured logging to monitor shutdown behavior
3. **Test Shutdown**: Test your application's shutdown behavior under load
4. **Custom Close Codes**: Use appropriate WebSocket close status codes for different scenarios

## Testing

To test the shutdown functionality:

1. **Start the application** with WebSocket connections
2. **Send shutdown signal** (Ctrl+C, SIGTERM, etc.)
3. **Observe logs** for proper disconnection sequence
4. **Verify client behavior** - clients should receive close frames

## Troubleshooting

### Common Issues

1. **Connections not closing**: Check if WebSocketShutdownService is registered
2. **Timeout warnings**: Increase timeout or investigate slow connections
3. **Missing logs**: Ensure logging level includes Information and Debug

### Debugging

Enable debug logging to see detailed connection closure information:

```json
{
  "Logging": {
    "LogLevel": {
      "Streaming.Scaling.Service": "Debug"
    }
  }
}
```

## Integration with ASP.NET Core

The shutdown functionality integrates seamlessly with ASP.NET Core's shutdown process:

- Uses `IHostApplicationLifetime` for shutdown coordination
- Respects cancellation tokens in middleware
- Works with Docker, Kubernetes, and other hosting environments

## Performance Considerations

- **Parallel Processing**: Connections are closed in parallel for efficiency
- **Timeout Protection**: Prevents hanging during shutdown
- **Memory Cleanup**: Subscriptions are properly removed from memory
- **Resource Disposal**: WebSocket resources are properly disposed 