namespace HR.Employee.API.Domain.Employees;

using HR.Employee.API.Domain.Common;

/// <summary>Value object — Employees table ke andar hi columns (EF owned type).</summary>
public sealed class Address
{
    public string? Line1 { get; private set; }
    public string? Line2 { get; private set; }
    public string? City { get; private set; }
    public string? State { get; private set; }
    public string? PostalCode { get; private set; }
    public string? CountryCode { get; private set; }

    private Address() { }

    /// <summary>Saari fields khali hon to null — address hai hi nahi.</summary>
    public static Address? Create(string? line1, string? line2, string? city, string? state, string? postalCode, string? countryCode)
    {
        var address = new Address
        {
            Line1 = Guard.Optional(line1, "Address line 1", 300),
            Line2 = Guard.Optional(line2, "Address line 2", 300),
            City = Guard.Optional(city, "City", 100),
            State = Guard.Optional(state, "State", 100),
            PostalCode = Guard.Optional(postalCode, "Postal code", 20),
            CountryCode = string.IsNullOrWhiteSpace(countryCode) ? null : Guard.CountryCode(countryCode)
        };

        return address is { Line1: null, Line2: null, City: null, State: null, PostalCode: null, CountryCode: null }
            ? null
            : address;
    }
}
