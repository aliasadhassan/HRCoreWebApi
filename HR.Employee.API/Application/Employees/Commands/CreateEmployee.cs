namespace HR.Employee.API.Application.Employees.Commands;

using FluentValidation;
using HR.Employee.API.Application.Common.Exceptions;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Employees;
using MediatR;
using Microsoft.EntityFrameworkCore;

/// <summary>EmployeeCode khali ho to system EMP-0001 jaisa code bana deta hai.</summary>
public sealed record CreateEmployeeCommand(
    string? EmployeeCode,
    string FirstName,
    string? MiddleName,
    string LastName,
    string WorkEmail,
    Guid LocationId,
    Guid DepartmentId,
    Guid DesignationId,
    Guid? ManagerId,
    EmploymentType EmploymentType,
    DateOnly JoiningDate,
    DateOnly? ProbationEndDate) : IRequest<Guid>;

public sealed class CreateEmployeeValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeValidator()
    {
        RuleFor(x => x.EmployeeCode).MaximumLength(20);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.MiddleName).MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.WorkEmail).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.LocationId).NotEmpty();
        RuleFor(x => x.DepartmentId).NotEmpty();
        RuleFor(x => x.DesignationId).NotEmpty();
        RuleFor(x => x.EmploymentType).IsInEnum();
        RuleFor(x => x.ProbationEndDate).GreaterThan(x => x.JoiningDate).When(x => x.ProbationEndDate.HasValue);
    }
}

public sealed class CreateEmployeeHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<CreateEmployeeCommand, Guid>
{
    public async Task<Guid> Handle(CreateEmployeeCommand request, CancellationToken ct)
    {
        var tenantId = currentUser.RequireTenantId();

        await EmployeeReferenceChecks.EnsureJobReferencesAsync(
            db, request.LocationId, request.DepartmentId, request.DesignationId, request.ManagerId, ct);

        var workEmail = request.WorkEmail.Trim();
        if (await db.Employees.AnyAsync(e => e.WorkEmail == workEmail, ct))
            throw new ConflictException($"An employee with work email '{workEmail}' already exists.");

        var code = string.IsNullOrWhiteSpace(request.EmployeeCode)
            ? await GenerateCodeAsync(tenantId, ct)
            : request.EmployeeCode.Trim().ToUpperInvariant();

        if (await db.Employees.AnyAsync(e => e.EmployeeCode == code, ct))
            throw new ConflictException($"Employee code '{code}' is already in use.");

        var employee = Employee.Create(
            tenantId, code,
            request.FirstName, request.MiddleName, request.LastName, workEmail,
            request.LocationId, request.DepartmentId, request.DesignationId, request.ManagerId,
            request.EmploymentType, request.JoiningDate, request.ProbationEndDate);

        db.Employees.Add(employee);
        await db.SaveChangesAsync(ct);   // EmployeeCreatedDomainEvent → outbox → integration event
        return employee.Id;
    }

    private async Task<string> GenerateCodeAsync(Guid tenantId, CancellationToken ct)
    {
        // Soft-deleted bhi gino, taake purana code dobara na mile. Race pe unique index 409 de dega.
        var count = await db.Employees.IgnoreQueryFilters().CountAsync(e => e.TenantId == tenantId, ct);
        return $"EMP-{count + 1:D4}";
    }
}
