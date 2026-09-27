namespace HR.Payroll.API.Domain.Common;

internal static class Guard
{
    public static string Required(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException($"{field} is required.");

        value = value.Trim();
        if (value.Length > maxLength)
            throw new DomainException($"{field} cannot exceed {maxLength} characters.");
        return value;
    }

    public static string? Optional(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        value = value.Trim();
        if (value.Length > maxLength)
            throw new DomainException($"{field} cannot exceed {maxLength} characters.");
        return value;
    }

    public static Guid NotEmpty(Guid id, string field)
        => id == Guid.Empty ? throw new DomainException($"{field} is required.") : id;

    public static string CountryCode(string? value, string field = "Country code")
    {
        var code = Required(value, field, 2).ToUpperInvariant();
        if (code.Length != 2 || !code.All(char.IsAsciiLetterUpper))
            throw new DomainException($"{field} must be a 2-letter ISO code (e.g. PK, AE).");
        return code;
    }

    public static string Currency(string? value, string field = "Currency")
    {
        var code = Required(value, field, 3).ToUpperInvariant();
        if (code.Length != 3 || !code.All(char.IsAsciiLetterUpper))
            throw new DomainException($"{field} must be a 3-letter ISO code (e.g. PKR, AED).");
        return code;
    }

    public static decimal Positive(decimal value, string field)
        => value > 0 ? value : throw new DomainException($"{field} must be greater than zero.");

    public static decimal NotNegative(decimal value, string field)
        => value >= 0 ? value : throw new DomainException($"{field} cannot be negative.");

    public static decimal Percentage(decimal value, string field)
        => value is >= 0 and <= 1000 ? value : throw new DomainException($"{field} must be between 0 and 1000 percent.");
}
