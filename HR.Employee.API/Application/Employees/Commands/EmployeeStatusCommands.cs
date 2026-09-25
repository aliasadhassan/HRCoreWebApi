namespace HR.Employee.API.Application.Employees.Commands;

using FluentValidation;
using HR.Employee.API.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

// ───────────────────────────── Confirm probation ─────────────────────────────
public sealed record ConfirmEmployeeCommand(Guid EmployeeId, DateOnly ConfirmationDate) : IRequest;

public sealed class ConfirmEmployeeHandler(IAppDbContext db) : IRequestHandler<ConfirmEmployeeCommand>
{
    public async Task Handle(ConfirmEmployeeCommand request, CancellationToken ct)
    {
        var employee = await EmployeeReferenceChecks.GetEmployeeAsync(db, request.EmployeeId, ct);
        employee.ConfirmProbation(request.ConfirmationDate);
        await db.SaveChangesAsync(ct);
    }
}

// ─────────────────────────────────── Exit ────────────────────────────────────
public sealed record ExitEmployeeCommand(Guid EmployeeId, DateOnly ExitDate, string Reason) : IRequest;

public sealed class ExitEmployeeValidator : AbstractValidator<ExitEmployeeCommand>
{
    public ExitEmployeeValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class ExitEmployeeHandler(IAppDbContext db) : IRequestHandler<ExitEmployeeCommand>
{
    public async Task Handle(ExitEmployeeCommand request, CancellationToken ct)
    {
        var employee = await EmployeeReferenceChecks.GetEmployeeAsync(db, request.EmployeeId, ct);
        employee.Exit(request.ExitDate, request.Reason);

        // Department head tha to head ki seat khali
        var headedDepartments = await db.Departments.Where(d => d.HeadEmployeeId == employee.Id).ToListAsync(ct);
        foreach (var department in headedDepartments)
            department.AssignHead(null);

        // NOTE: jin employees ka ye manager tha, unka naya manager HR "Change job" se set karega.
        await db.SaveChangesAsync(ct);   // EmployeeExitedDomainEvent → integration event (Identity user disable)
    }
}
