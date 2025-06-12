using Domain.EndUsers;
using Domain.Shared;


namespace Infrastructure.Scaling.Types;

public class ConnectionInfo
{
    public string Id { get; }

    public Secret Secret { get; }

    /// <summary>
    /// client-side sdk EndUser
    /// </summary>
    public EndUser? User { get; set; }

    public string Type => Secret.Type;
    public Guid EnvId => Secret.EnvId;
    public string ProjectKey => Secret.ProjectKey;
    public string EnvKey => Secret.EnvKey;

    public ConnectionInfo(string id, Secret secret)
    {
        Id = id;
        Secret = secret;
    }

    /// <summary>
    /// attach client-side sdk EndUser
    /// </summary>
    public void AttachUser(EndUser user)
    {
        User = user;
    }
}