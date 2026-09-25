namespace HR.Employee.API.Domain.Common;

/// <summary>Chhote reusable checks taake har entity mein same if/throw na likhna pade.</summary>
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

    public static string Email(string? value, string field = "Email")
    {
        var email = Required(value, field, 256);
        if (!email.Contains('@') || email.Contains(' '))
            throw new DomainException($"{field} is not a valid email address.");
        return email;
    }

    public static string CountryCode(string? value, string field = "Country code")
    {
        var code = Required(value, field, 2).ToUpperInvariant();
        if (code.Length != 2 || !code.All(char.IsAsciiLetterUpper))
            throw new DomainException($"{field} must be a 2-letter ISO code (e.g. PK, AE).");
        return code;
    }
}
