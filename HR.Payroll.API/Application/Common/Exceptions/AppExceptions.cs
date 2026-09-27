namespace HR.Payroll.API.Application.Common.Exceptions;

public sealed class NotFoundException(string entity, object key)
    : Exception($"{entity} '{key}' was not found.");

public sealed class ConflictException(string message) : Exception(message);
