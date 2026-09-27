namespace HR.Payroll.API.Domain.Common;

public sealed class DomainException(string message) : Exception(message);
