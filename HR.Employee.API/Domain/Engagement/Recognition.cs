namespace HR.Employee.API.Domain.Engagement;

using HR.Employee.API.Domain.Common;

/// <summary>Shabash / kudos: ek employee doosre ko ek company value ke saath. Sab ko feed mein dikhta hai.</summary>
public sealed class Recognition : AuditableEntity
{
    public Guid FromEmployeeId { get; private set; }
    public Guid ToEmployeeId { get; private set; }
    public RecognitionValue Value { get; private set; }
    public string Message { get; private set; } = default!;

    private Recognition() { }

    public static Recognition Create(Guid tenantId, Guid fromEmployeeId, Guid toEmployeeId, RecognitionValue value, string message)
    {
        Guard.NotEmpty(fromEmployeeId, "From");
        Guard.NotEmpty(toEmployeeId, "Person");
        if (fromEmployeeId == toEmployeeId)
            throw new DomainException("You cannot recognise yourself.");
        if (!Enum.IsDefined(value))
            throw new DomainException("Select a valid value.");
        var text = Guard.Required(message, "Message", 500);
        if (text.Length < 3)
            throw new DomainException("Write a few words about what they did.");

        return new Recognition
        {
            TenantId = Guard.NotEmpty(tenantId, "Tenant"),
            FromEmployeeId = fromEmployeeId,
            ToEmployeeId = toEmployeeId,
            Value = value,
            Message = text
        };
    }
}

/// <summary>Kisi recognition par "cheer" (like). Ek banda ek baar.</summary>
public sealed class RecognitionCheer : TenantChildEntity
{
    public Guid RecognitionId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private RecognitionCheer() { }

    public static RecognitionCheer Create(Guid recognitionId, Guid employeeId, DateTime now)
        => new() { RecognitionId = Guard.NotEmpty(recognitionId, "Recognition"), EmployeeId = Guard.NotEmpty(employeeId, "Employee"), CreatedAt = now };
}
