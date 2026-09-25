namespace HR.Shared.Library.Events;

/// <summary>Employee API publish karti hai (outbox se). Payroll / Attendance / Identity (invite) subscribe karenge.</summary>
public record EmployeeCreatedIntegrationEvent(
    Guid EmployeeId,
    Guid TenantId,
    string EmployeeCode,
    string FullName,
    string WorkEmail,
    Guid DepartmentId,
    Guid LocationId,
    string EmploymentType,
    DateOnly JoiningDate);

/// <summary>Identity isay sun kar user disable karega; Payroll final settlement.</summary>
public record EmployeeExitedIntegrationEvent(
    Guid EmployeeId,
    Guid TenantId,
    Guid? UserId,
    DateOnly ExitDate);
