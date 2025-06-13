//using System.Net.WebSockets;
//using Domain.Shared;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Internal;
//using Microsoft.Extensions.Logging;
//using Application.Services;

//namespace Backplane.Validators;

//public sealed class RequestValidator(
//    ISystemClock systemClock,
//    IStore store,
//    ILogger<RequestValidator> logger)
//    : IRequestValidator
//{
//    // TODO, this should be configurable
//    private const long TokenTimeoutMs = 30 * 1000;

//    public async Task<ValidationResult> ValidateAsync(string tokenString)
//    {
//        try
//        {
//            return await ValidateSecretTokenAsync(tokenString);
//        }
//        catch (Exception ex)
//        {
//            logger.ErrorValidateRequest(tokenString, ex);

//            // throw original exception
//            throw;
//        }
//    }

//    private async Task<ValidationResult> ValidateSecretTokenAsync(string tokenString )
//    {
//        var token = new Token(tokenString.AsSpan());
//        var current = systemClock.UtcNow.ToUnixTimeMilliseconds();
//        if (!token.IsValid)
//        {
//            return ValidationResult.Failed($"Invalid token: {tokenString}");
//        }

//        if (Math.Abs(current - token.Timestamp) > TokenTimeoutMs)
//        {
//            return ValidationResult.Failed($"Token is expired: {tokenString}");
//        }

//        var secret = await store.GetSecretAsync(token.SecretString);
//        if (secret is null)
//        {
//            return ValidationResult.Failed($"Secret is not found: {token.SecretString}");
//        }

//        return ValidationResult.Ok([secret]);
//    }
//}