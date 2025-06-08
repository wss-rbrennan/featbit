using System.Text.Json;
using Domain.EndUsers;
using Backplane.Messages;
using Backplane.Protocol;

namespace Backplane.Services;

public interface IDataSyncService
{
    Task<object> GetPayloadAsync(EdgeMessage edgeMessage);

    Task<ClientSdkPayload> GetClientSdkPayloadAsync(Guid envId, EndUser user, long timestamp);

    Task<ServerSdkPayload> GetServerSdkPayloadAsync(Guid envId, long timestamp);

    Task<object> GetRelayProxyPayloadAsync(IEnumerable<Guid> envIds, long timestamp);

    Task<object> GetFlagChangePayloadAsync(JsonElement flag);

    Task<object> GetSegmentChangePayloadAsync(EdgeMessage edgeMessage, JsonElement segment, string[] affectedFlagIds);
}