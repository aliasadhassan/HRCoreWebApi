namespace HR.Employee.API.Domain.Employees;

using HR.Employee.API.Domain.Common;

/// <summary>Employee aggregate ka hissa — sirf Employee ke methods se banta/badalta hai.</summary>
public sealed class EmergencyContact : Entity
{
    public Guid EmployeeId { get; private set; }
    public string Name { get; private set; } = default!;
    public string Relationship { get; private set; } = default!;
    public string Phone { get; private set; } = default!;
    public string? AlternatePhone { get; private set; }
    public bool IsPrimary { get; private set; }

    private EmergencyContact() { }

    internal EmergencyContact(string name, string relationship, string phone, string? alternatePhone)
        => Update(name, relationship, phone, alternatePhone);

    internal void Update(string name, string relationship, string phone, string? alternatePhone)
    {
        Name = Guard.Required(name, "Contact name", 200);
        Relationship = Guard.Required(relationship, "Relationship", 50);
        Phone = Guard.Required(phone, "Phone", 50);
        AlternatePhone = Guard.Optional(alternatePhone, "Alternate phone", 50);
    }

    internal void SetPrimary(bool value) => IsPrimary = value;
}
