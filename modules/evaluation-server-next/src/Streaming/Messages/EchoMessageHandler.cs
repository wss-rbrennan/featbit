using Infrastructure.Protocol;

namespace Streaming.Messages;

public class EchoMessageHandler : IMessageHandler
{
    public string Type => MessageTypes.Echo;

    public async Task HandleAsync(MessageContext ctx)
    {
        var connection = ctx.Connection;
        var token = ctx.CancellationToken;

        // Echo back the same message that was received
        var echoMessage = ctx.Data.GetRawText();
        var echoBytes = System.Text.Encoding.UTF8.GetBytes(echoMessage);
        await connection.SendAsync(echoBytes, token);
    }
} 