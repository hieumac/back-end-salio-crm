namespace Salio.Domain.Common;

public class DomainException : Exception
{
    public string? Code { get; }
    public DomainException(string message, string? code = null) : base(message) => Code = code;
}

public class NotFoundException(string resource, object? key = null)
    : DomainException($"{resource} not found{(key is null ? "" : $" (key: {key})")}", "NOT_FOUND");

public class ForbiddenException(string action)
    : DomainException($"Forbidden: {action}", "FORBIDDEN");

public class ValidationException(string message) : DomainException(message, "VALIDATION");

public class ConflictException(string message) : DomainException(message, "CONFLICT");
