using Domain.EndUsers;

namespace Infrastructure.Protocol;

public class DataSyncMessage
{
    public long? Timestamp { get; set; }

    public EndUser? User { get; set; }
}