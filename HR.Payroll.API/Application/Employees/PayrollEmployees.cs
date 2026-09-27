namespace HR.Payroll.API.Application.Employees;

using FluentValidation;
using HR.Payroll.API.Application.Common.Exceptions;
using HR.Payroll.API.Application.Common.Interfaces;
using HR.Payroll.API.Application.Common.Models;
using HR.Payroll.API.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record PayrollEmployeeDto(
    Guid EmployeeId, string EmployeeCode, string FullName, string WorkEmail, string? DepartmentName,
    string EmploymentType, DateOnly JoiningDate, DateOnly? ExitDate, bool IsActive,
    Guid? PayGroupId, string? PayGroupName, bool HasSalary, SalaryBasis? SalaryBasis, decimal? SalaryAmount, string? CurrencyCode);

public sealed record GetPayrollEmployeesQuery : IRequest<PagedResult<PayrollEmployeeDto>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public Guid? PayGroupId { get; init; }
    public bool OnlyUnassigned { get; init; }       // "payroll setup adhoora" wali list
    public bool IncludeInactive { get; init; }
}

/// <summary>Bulk: ek saath kai employees ko pay group mein daalna (ya PayGroupId null = nikalna).</summary>
public sealed record AssignPayGroupCommand(IReadOnlyList<Guid> EmployeeIds, Guid? PayGroupId) : IRequest<int>;

public sealed class GetPayrollEmployeesValidator : AbstractValidator<GetPayrollEmployeesQuery>
{
    public GetPayrollEmployeesValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Search).MaximumLength(100);
    }
}

public sealed class AssignPayGroupValidator : AbstractValidator<AssignPayGroupCommand>
{
    public AssignPayGroupValidator()
    {
        RuleFor(x => x.EmployeeIds).NotEmpty().Must(ids => ids.Count <= 500).WithMessage("Assign at most 500 employees at a time.");
    }
}

public sealed class PayrollEmployeeHandlers(IAppDbContext db) :
    IRequestHandler<GetPayrollEmployeesQuery, PagedResult<PayrollEmployeeDto>>,
    IRequestHandler<AssignPayGroupCommand, int>
{
    public Task<PagedResult<PayrollEmployeeDto>> Handle(GetPayrollEmployeesQuery q, CancellationToken ct)
    {
        var query = db.PayrollEmployees.AsNoTracking();

        if (!q.IncludeInactive)
            query = query.Where(e => e.IsActive);
        if (q.OnlyUnassigned)
            query = query.Where(e => e.PayGroupId == null || !db.EmployeeSalaries.Any(s => s.EmployeeId == e.Id && s.EffectiveTo == null));
        else if (q.PayGroupId is { } groupId)
            query = query.Where(e => e.PayGroupId == groupId);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim();
            query = query.Where(e => e.FullName.Contains(term) || e.EmployeeCode.Contains(term) || e.WorkEmail.Contains(term));
        }

        // Har field alag scalar subquery — EF isay saaf OUTER APPLY / subselect mein badal deta hai
        return query
            .OrderBy(e => e.FullName)
            .Select(e => new PayrollEmployeeDto(
                e.Id, e.EmployeeCode, e.FullName, e.WorkEmail, e.DepartmentName,
                e.EmploymentType, e.JoiningDate, e.ExitDate, e.IsActive,
                e.PayGroupId,
                db.PayGroups.Where(g => g.Id == e.PayGroupId).Select(g => g.Name).FirstOrDefault(),
                db.EmployeeSalaries.Any(s => s.EmployeeId == e.Id && s.EffectiveTo == null),
                db.EmployeeSalaries.Where(s => s.EmployeeId == e.Id && s.EffectiveTo == null).Select(s => (SalaryBasis?)s.SalaryBasis).FirstOrDefault(),
                db.EmployeeSalaries.Where(s => s.EmployeeId == e.Id && s.EffectiveTo == null).Select(s => (decimal?)s.BasisAmount).FirstOrDefault(),
                db.EmployeeSalaries.Where(s => s.EmployeeId == e.Id && s.EffectiveTo == null).Select(s => s.CurrencyCode).FirstOrDefault()))
            .ToPagedResultAsync(q.Page, q.PageSize, ct);
    }

    public async Task<int> Handle(AssignPayGroupCommand request, CancellationToken ct)
    {
        if (request.PayGroupId is { } groupId && !await db.PayGroups.AnyAsync(g => g.Id == groupId && g.IsActive, ct))
            throw new NotFoundException("Pay group", groupId);

        var ids = request.EmployeeIds.Distinct().ToList();
        var employees = await db.PayrollEmployees.Where(e => ids.Contains(e.Id)).ToListAsync(ct);

        if (employees.Count != ids.Count)
            throw new NotFoundException("Employee", ids.Except(employees.Select(e => e.Id)).First());

        foreach (var employee in employees)
            employee.AssignPayGroup(request.PayGroupId);

        await db.SaveChangesAsync(ct);
        return employees.Count;
    }
}
