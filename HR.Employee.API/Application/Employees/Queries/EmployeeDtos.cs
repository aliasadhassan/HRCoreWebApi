namespace HR.Employee.API.Application.Employees.Queries;

using HR.Employee.API.Application.Common.Models;
using HR.Employee.API.Domain.Employees;

public sealed record EmployeeListItemDto(
    Guid Id,
    string EmployeeCode,
    string FullName,
    string WorkEmail,
    string DepartmentName,
    string DesignationTitle,
    string LocationName,
    string? ManagerName,
    EmploymentType EmploymentType,
    EmploymentStatus EmploymentStatus,
    DateOnly JoiningDate,
    string? PhotoStorageKey);

/// <summary>NationalIdNumber jaan boojh kar nahi — sensitive, alag permission wale endpoint se aayega.</summary>
public sealed record EmployeeDetailsDto(
    Guid Id,
    string EmployeeCode,
    Guid? UserId,
    string FirstName,
    string? MiddleName,
    string LastName,
    Gender? Gender,
    DateOnly? DateOfBirth,
    MaritalStatus? MaritalStatus,
    string? NationalityCode,
    string WorkEmail,
    string? PersonalEmail,
    string? WorkPhone,
    string? PersonalPhone,
    AddressDto Address,
    LookupDto Location,
    LookupDto Department,
    LookupDto Designation,
    LookupDto? Manager,
    EmploymentType EmploymentType,
    EmploymentStatus EmploymentStatus,
    DateOnly JoiningDate,
    DateOnly? ProbationEndDate,
    DateOnly? ConfirmationDate,
    short? NoticePeriodDays,
    DateOnly? ExitDate,
    string? ExitReason,
    string? PhotoStorageKey,
    IReadOnlyList<EmergencyContactDto> EmergencyContacts,
    IReadOnlyList<JobHistoryDto> JobHistory);

public sealed record AddressDto(string? Line1, string? Line2, string? City, string? State, string? PostalCode, string? CountryCode);

public sealed record EmergencyContactDto(Guid Id, string Name, string Relationship, string Phone, string? AlternatePhone, bool IsPrimary);

public sealed record JobHistoryDto(
    Guid Id,
    DateOnly EffectiveDate,
    JobChangeType ChangeType,
    Guid DepartmentId,
    Guid DesignationId,
    Guid LocationId,
    Guid? ManagerId,
    EmploymentStatus EmploymentStatus,
    string? Remarks);
