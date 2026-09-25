namespace HR.Employee.API.Domain.Common;

/// <summary>Business rule toota — API isay 422 bana kar bhejti hai.</summary>
public sealed class DomainException(string message) : Exception(message);
