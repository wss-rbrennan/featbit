using Domain.Shared;

namespace Backplane.Services;

public interface IRelayProxyService
{
    Task<Secret[]> GetServerSecretsAsync(string key);
}