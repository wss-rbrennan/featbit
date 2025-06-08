using Domain.EndUsers;

namespace Application.Protocol;

public class DataSyncMessage
{
    public long? Timestamp { get; set; }

    public EndUser? User { get; set; }
}