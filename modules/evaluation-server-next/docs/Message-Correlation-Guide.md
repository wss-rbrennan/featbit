# Message Correlation and Sender ID Guide

This guide explains how the Edge and Hub services handle message correlation and sender identification using the enhanced Message class.

## Overview

The messaging system now includes two key fields for tracking and correlating messages:

- **SenderId**: Identifies who sent the message
- **CorrelationId**: Links related messages together across service boundaries

## Message Class Structure

```csharp
public class Message
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("channelId")]
    public string? ChannelId { get; set; }

    [JsonPropertyName("channelName")]
    public string? ChannelName { get; set; }

    [JsonPropertyName("message")]
    public JsonElement MessageContent { get; set; }

    [JsonPropertyName("senderId")]
    public string? SenderId { get; set; }

    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; set; }

    [JsonPropertyName("serviceType")]
    public string? ServiceType { get; set; }
}
```

## Service Types

The system supports three service types:

```csharp
public static class ServiceTypes
{
    public const string Edge = "edge";  // Edge service - handles WebSocket connections
    public const string Hub = "hub";    // Hub service - handles business logic
    public const string Web = "web";    // Web service - handles web requests
}
```

## Service Identity

### ServiceIdentityProvider

Each service instance generates a unique GUID on startup:

```csharp
public interface IServiceIdentityProvider
{
    string ServiceId { get; }
}

public class ServiceIdentityProvider : IServiceIdentityProvider
{
    public string ServiceId { get; }

    public ServiceIdentityProvider(ILogger<ServiceIdentityProvider> logger)
    {
        ServiceId = Guid.NewGuid().ToString();
        logger.LogInformation("Service identity initialized with ID: {ServiceId}", ServiceId);
    }
}
```

### MessageFactory

The MessageFactory helps create messages with proper correlation and sender IDs:

```csharp
public interface IMessageFactory
{
    Message CreateMessage(string type, string? channelId, string? channelName, JsonElement messageContent, string senderId);
    Message CreateResponseMessage(string type, string? channelId, string? channelName, JsonElement messageContent, string senderId, string correlationId);
    string GenerateCorrelationId();
}
```

## Edge Service Behavior

### WebSocket Connection Messages

When the Edge service receives a message from a WebSocket client:

1. **SenderId**: Uses the WebSocket subscription ID (unique per connection)
2. **CorrelationId**: Generates a new GUID for each message

```csharp
// In WebSocketService.HandleMessageAsync
var backplaneMessage = _messageFactory.CreateMessage(
    type: "server",
    channelId: envId,
    channelName: envId,
    messageContent: JsonDocument.Parse(messageContextJson).RootElement,
    senderId: id // WebSocket subscription ID
);
```

### Service-Initiated Messages

When the Edge service sends its own messages (like test messages):

1. **SenderId**: Uses the Edge service's unique ID
2. **CorrelationId**: Generates a new GUID

```csharp
// In WebSocketService.SubscribeToBackplaneChannel
var testMessage = _messageFactory.CreateMessage(
    type: "server",
    channelId: envId,
    channelName: envId,
    messageContent: testContent,
    senderId: _serviceIdentityProvider.ServiceId // Edge service ID
);
```

## Hub Service Behavior

### Processing Messages from Edge

When the Hub receives a message from Edge:

1. **Preserve CorrelationId**: Keep the original correlation ID
2. **Update SenderId**: Use the Hub's service ID

```csharp
// Example Hub message processing
private async Task HandleDataSyncFromEdge(Message originalMessage)
{
    logger.LogInformation("Hub processing data-sync from Edge - SenderId: {SenderId}, CorrelationId: {CorrelationId}",
        originalMessage.SenderId, originalMessage.CorrelationId);

    // Create response preserving correlation ID
    var responseMessage = _messageFactory.CreateResponseMessage(
        type: "server",
        channelId: originalMessage.ChannelId,
        channelName: originalMessage.ChannelName,
        messageContent: responseContent,
        senderId: _serviceIdentityProvider.ServiceId, // Hub's ID
        correlationId: originalMessage.CorrelationId! // Preserve correlation
    );

    await _backplaneManager.PublishAsync(responseChannel, JsonSerializer.Serialize(responseMessage));
}
```

### Hub-Initiated Messages

When the Hub initiates new messages to Edge:

1. **SenderId**: Uses the Hub's service ID
2. **CorrelationId**: Generates a new GUID

```csharp
// Example Hub-initiated message
public async Task SendNewMessageToEdge(string channelId, object data)
{
    var newMessage = _messageFactory.CreateMessage(
        type: "server",
        channelId: channelId,
        channelName: channelId,
        messageContent: messageContent,
        senderId: _serviceIdentityProvider.ServiceId // Hub's ID
    );

    await _backplaneManager.PublishAsync(edgeChannel, JsonSerializer.Serialize(newMessage));
}
```

## Message Flow Examples

### Example 1: WebSocket Client → Edge → Hub → Edge

1. **Client sends message to Edge**:
   ```json
   {
     "messageType": "data-sync",
     "data": { "flags": [...] }
   }
   ```

2. **Edge creates backplane message**:
   ```json
   {
     "type": "server",
     "channelId": "env-123",
     "senderId": "ws-subscription-abc",
     "correlationId": "corr-456",
     "serviceType": "edge",
     "message": { "messageType": "data-sync", ... }
   }
   ```

3. **Hub processes and responds**:
   ```json
   {
     "type": "server",
     "channelId": "env-123",
     "senderId": "hub-service-def",
     "correlationId": "corr-789",
     "serviceType": "hub",
     "message": { "messageType": "data-sync-response", ... }
   }
   ```

### Example 2: Hub → Edge (Hub-initiated)

1. **Hub sends new message**:
   ```json
   {
     "type": "server",
     "channelId": "env-123",
     "senderId": "hub-service-def",
     "correlationId": "corr-abc",
     "serviceType": "hub",
     "message": { "messageType": "flag-update", ... }
   }
   ```

## Implementation Checklist

### Edge Service
- [x] Inject `IMessageFactory` and `IServiceIdentityProvider`
- [x] Use subscription ID as sender for WebSocket messages
- [x] Use service ID as sender for service messages
- [x] Generate correlation IDs for new messages
- [x] Use `ServiceTypes.Edge` for all Edge messages
- [x] Log sender, correlation, and service type information

### Hub Service
- [x] Inject `IMessageFactory` and `IServiceIdentityProvider`
- [x] Preserve correlation IDs when responding to Edge
- [x] Use Hub service ID as sender for all Hub messages
- [x] Generate new correlation IDs for Hub-initiated messages
- [x] Use `ServiceTypes.Hub` for all Hub messages
- [x] Log message tracing information

### Service Registration
- [x] Register `IServiceIdentityProvider` as singleton
- [x] Register `IMessageFactory` as singleton
- [x] Register Hub services with message correlation support

## Logging and Monitoring

### Structured Logging

Both services should log correlation information including service type:

```csharp
logger.LogInformation("Processing message - ServiceType: {ServiceType}, SenderId: {SenderId}, CorrelationId: {CorrelationId}",
    message.ServiceType, message.SenderId, message.CorrelationId);
```

### Message Tracing

The correlation ID and service type enable comprehensive end-to-end message tracing:

1. **Request Tracking**: Follow a message from client to Hub and back
2. **Service Identification**: Know which service type sent each message
3. **Performance Monitoring**: Measure round-trip times between services
4. **Error Correlation**: Link errors across service boundaries
5. **Debugging**: Trace message flow in distributed scenarios

## Benefits

1. **Complete Traceability**: Full message flow visibility with service identification
2. **Service-Aware Debugging**: Easy correlation of related messages by service type
3. **Performance Monitoring**: Track performance between specific service types
4. **Comprehensive Auditing**: Who sent what, when, and from which service type
5. **Load Balancing**: Identify message sources, destinations, and service types
6. **Service Health Monitoring**: Monitor message patterns by service type

## Best Practices

1. **Always preserve correlation IDs** when responding to messages
2. **Generate new correlation IDs** only for new message flows
3. **Use appropriate service types** (edge, hub, web) for all messages
4. **Use meaningful sender IDs** (subscription IDs, service IDs)
5. **Log correlation and service type information** at key processing points
6. **Include correlation IDs in error messages** for debugging
7. **Monitor correlation ID patterns** for system health
8. **Use service types for routing and filtering** messages appropriately