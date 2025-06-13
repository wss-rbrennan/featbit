using Domain.Shared;

namespace Application.Services;

public interface IRelayProxyService
{
    Task<Secret[]> GetServerSecretsAsync(string key);
}