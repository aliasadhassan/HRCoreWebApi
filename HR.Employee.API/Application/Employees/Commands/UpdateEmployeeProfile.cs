namespace HR.Employee.API.Application.Employees.Commands;

using FluentValidation;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Employees;
using MediatR;

public sealed record AddressInput(string? Line1, string? Line2, string? City, string? State, string? PostalCode, string? CountryCode);

/// <summary>"Edit profile" form: personal + contact info (job fields yahan nahi, woh ChangeEmployeeJob mein).</summary>
public sealed record UpdateEmployeeProfileCommand(
    Guid EmployeeId,
    string FirstName,
    string? MiddleName,
    string LastName,
    Gender? Gender,
    DateOnly? DateOfBirth,
    MaritalStatus? MaritalStatus,
    string? NationalityCode,
    string? PersonalEmail,
    string? WorkPhone,
    string? PersonalPhone,
    AddressInput? Address) : IRequest;

public sealed class UpdateEmployeeProfileValidator : AbstractValidator<UpdateEmployeeProfileCommand>
{
    public UpdateEmployeeProfileValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.MiddleName).MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Gender).IsInEnum();
        RuleFor(x => x.MaritalStatus).IsInEnum();
        RuleFor(x => x.NationalityCode).Length(2).When(x => !string.IsNullOrEmpty(x.NationalityCode));
        RuleFor(x => x.PersonalEmail).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrEmpty(x.PersonalEmail));
        RuleFor(x => x.WorkPhone).MaximumLength(50);
        RuleFor(x => x.PersonalPhone).MaximumLength(50);
        RuleFor(x => x.Address!.CountryCode).Length(2).When(x => !string.IsNullOrEmpty(x.Address?.CountryCode));
    }
}

public sealed class UpdateEmployeeProfileHandler(IAppDbContext db) : IRequestHandler<UpdateEmployeeProfileCommand>
{
    public async Task Handle(UpdateEmployeeProfileCommand request, CancellationToken ct)
    {
        var employee = await EmployeeReferenceChecks.GetEmployeeAsync(db, request.EmployeeId, ct);

        employee.UpdatePersonalInfo(
            request.FirstName, request.MiddleName, request.LastName,
            request.Gender, request.DateOfBirth, request.MaritalStatus, request.NationalityCode);

        var address = request.Address is null
            ? null
            : Address.Create(request.Address.Line1, request.Address.Line2, request.Address.City,
                             request.Address.State, request.Address.PostalCode, request.Address.CountryCode);

        employee.UpdateContactInfo(request.PersonalEmail, request.WorkPhone, request.PersonalPhone, address);

        await db.SaveChangesAsync(ct);
    }
}
