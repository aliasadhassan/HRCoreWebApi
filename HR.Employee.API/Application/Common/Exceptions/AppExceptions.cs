namespace HR.Employee.API.Application.Common.Exceptions;

/// <summary>404</summary>
public sealed class NotFoundException(string entity, object key)
    : Exception($"{entity} '{key}' was not found.");

/// <summary>409 — duplicate code/email, ya aisa action jo current data se takrata hai.</summary>
public sealed class ConflictException(string message) : Exception(message);
