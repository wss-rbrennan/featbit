using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Application.Connections;
using MongoDB.Driver.Core.WireProtocol.Messages.Encoders.BinaryEncoders;

namespace Application.Messages;

public sealed partial class MessageDispatcher
{
    // JSON message format: {"messageType": "<type>", "data": <object>}
    private const string MessageTypePropertyName = "messageType";
    private const string DataPropertyName = "data";

    // Default buffer size for receiving messages (in bytes)
    private const int DefaultBufferSize = 2 * 1024;

    // Maximum number of fragments for a message, for most of the time messages should be single-fragment
    private const int MaxMessageFragment = 4;

    private readonly Dictionary<string, IMessageHandler> _handlers;
    private readonly ILogger<MessageDispatcher> _logger;

    public MessageDispatcher(IEnumerable<IMessageHandler> handlers, ILogger<MessageDispatcher> logger)
    {
        _handlers = handlers.ToDictionary(handler => handler.Type, StringComparer.OrdinalIgnoreCase);
        _logger = logger;
    }



    private async Task HandleMessageAsync(MessageContext messageContext, Memory<byte> bytes)
    {
        //TODO: Handle incoming Channel message here
        try
        {
            using var message = JsonDocument.Parse(bytes);

            var root = message.RootElement;
            if (!root.TryGetProperty(MessageTypePropertyName, out var messageTypeElement) ||
                !root.TryGetProperty(DataPropertyName, out var dataElement))
            {
                return;
            }

            var messageType = messageTypeElement.GetString() ?? string.Empty;
            if (!_handlers.TryGetValue(messageType, out var handler))
            {
                //Log.NoHandlerFor(_logger, messageType, messageContext);
                return;
            }

            var ctx = new DefaultMessageContext(dataElement);
            await handler.HandleAsync(messageContext);
        }
        catch (JsonException)
        {
            //Log.ReceivedInvalid(_logger, Encoding.UTF8.GetString(bytes.Span), messageContext);
        }
        catch (Exception ex)
        {
            //Log.ErrorHandleMessage(_logger, Encoding.UTF8.GetString(bytes.Span), messageContext, ex);
        }
    }
}