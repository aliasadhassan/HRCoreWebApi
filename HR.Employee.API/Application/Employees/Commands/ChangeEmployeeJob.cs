namespace HR.Employee.API.Application.Employees.Commands;

using FluentValidation;
using HR.Employee.API.Application.Common.Interfaces;
using MediatR;

/// <summary>Promotion / transfer / manager change — ek hi form. Jo badla, uski JobHistory row domain khud banata hai.</summary>
public sealed record ChangeEmployeeJobCommand(
    Guid EmployeeId,
    Guid DepartmentId,
    Guid LocationId,
    Guid DesignationId,
    Guid? ManagerId,
    DateOnly EffectiveDate,
    string? Remarks) : IRequest;

public sealed class ChangeEmployeeJobValidator : AbstractValidator<ChangeEmployeeJobCommand>
{
    public ChangeEmployeeJobValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.DepartmentId).NotEmpty();
        RuleFor(x => x.LocationId).NotEmpty();
        RuleFor(x => x.DesignationId).NotEmpty();
        RuleFor(x => x.Remarks).MaximumLength(500);
    }
}

public sealed class ChangeEmployeeJobHandler(IAppDbContext db) : IRequestHandler<ChangeEmployeeJobCommand>
{
    public async Task Handle(ChangeEmployeeJobCommand request, CancellationToken ct)
    {
        var employee = await EmployeeReferenceChecks.GetEmployeeAsync(db, request.EmployeeId, ct);

        await EmployeeReferenceChecks.EnsureJobReferencesAsync(
            db, request.LocationId, request.DepartmentId, request.DesignationId, request.ManagerId, ct);
        await EmployeeReferenceChecks.EnsureNoManagerCycleAsync(db, employee.Id, request.ManagerId, ct);

        employee.ChangeJob(request.DepartmentId, request.LocationId, request.DesignationId,
                           request.ManagerId, request.EffectiveDate, request.Remarks);

        await db.SaveChangesAsync(ct);
    }
}
