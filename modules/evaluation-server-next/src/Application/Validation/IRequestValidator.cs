using Infrastructure.Connections;

namespace Application.Validation;

public interface IRequestValidator
{
    Task<ValidationResult> ValidateAsync(ConnectionContext context);
}