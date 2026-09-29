namespace HR.Payroll.API.Application.Salaries;

using FluentValidation;
using HR.Payroll.API.Application.Common.Exceptions;
using HR.Payroll.API.Application.Common.Interfaces;
using HR.Payroll.API.Domain.Common;
using HR.Payroll.API.Domain.Salaries;
using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record SalaryOverrideDto(Guid PayComponentId, bool IsExcluded, CalcType? CalcType, decimal? Amount, decimal? Percentage, Guid? BaseComponentId);

public sealed record EmployeeSalaryDto(
    Guid Id, Guid SalaryTemplateId, string TemplateName, Guid? SalaryGradeId, string CurrencyCode,
    SalaryBasis SalaryBasis, decimal BasisAmount, DateOnly EffectiveFrom, DateOnly? EffectiveTo,
    SalaryChangeReason ChangeReason, string? Remarks, IReadOnlyList<SalaryOverrideDto> Overrides);

public sealed record AssignSalaryResult(Guid Id, string? Warning);

/// <summary>
/// Pehli salary ya increment — dono yahi. Current salary ho to uska EffectiveTo = naye EffectiveFrom − 1,
/// aur uske overrides nayi salary pe copy (HR ko dobara set na karne pade).
/// </summary>
public sealed record AssignSalaryCommand(
    Guid EmployeeId, Guid SalaryTemplateId, Guid? SalaryGradeId, string? CurrencyCode,
    SalaryBasis SalaryBasis, decimal BasisAmount, DateOnly EffectiveFrom, SalaryChangeReason ChangeReason, string? Remarks)
    : IRequest<AssignSalaryResult>;

public sealed record GetSalaryHistoryQuery(Guid EmployeeId) : IRequest<IReadOnlyList<EmployeeSalaryDto>>;

public sealed record SetSalaryOverrideCommand(
    Guid EmployeeId, Guid PayComponentId, CalcType CalcType, decimal? Amount, decimal? Percentage, Guid? BaseComponentId) : IRequest;

public sealed record ExcludeSalaryComponentCommand(Guid EmployeeId, Guid PayComponentId) : IRequest;

public sealed record RemoveSalaryOverrideCommand(Guid EmployeeId, Guid PayComponentId) : IRequest;

public sealed class AssignSalaryValidator : AbstractValidator<AssignSalaryCommand>
{
    public AssignSalaryValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.SalaryTemplateId).NotEmpty();
        RuleFor(x => x.CurrencyCode).Length(3).When(x => !string.IsNullOrEmpty(x.CurrencyCode));
        RuleFor(x => x.SalaryBasis).IsInEnum();
        RuleFor(x => x.BasisAmount).GreaterThan(0);
        RuleFor(x => x.ChangeReason).IsInEnum();
        RuleFor(x => x.Remarks).MaximumLength(500);
    }
}

public sealed class SetSalaryOverrideValidator : AbstractValidator<SetSalaryOverrideCommand>
{
    public SetSalaryOverrideValidator()
    {
        RuleFor(x => x.PayComponentId).NotEmpty();
        RuleFor(x => x.CalcType).IsInEnum()
            .NotEqual(CalcType.Variable).WithMessage("Variable components come from payroll inputs, not salary overrides.")
            .NotEqual(CalcType.Remainder).WithMessage("The remainder component is set on the salary template, not per employee.");
    }
}

public sealed class EmployeeSalaryHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<AssignSalaryCommand, AssignSalaryResult>,
    IRequestHandler<GetSalaryHistoryQuery, IReadOnlyList<EmployeeSalaryDto>>,
    IRequestHandler<SetSalaryOverrideCommand>,
    IRequestHandler<ExcludeSalaryComponentCommand>,
    IRequestHandler<RemoveSalaryOverrideCommand>
{
    public async Task<AssignSalaryResult> Handle(AssignSalaryCommand request, CancellationToken ct)
    {
        var employee = await db.PayrollEmployees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == request.EmployeeId, ct)
                       ?? throw new NotFoundException("Employee", request.EmployeeId);

        if (!employee.IsActive)
            throw new ConflictException("This employee has exited; salary cannot be changed.");
        if (request.EffectiveFrom < employee.JoiningDate)
            throw new ConflictException("Salary cannot start before the joining date.");

        if (!await db.SalaryTemplates.AnyAsync(t => t.Id == request.SalaryTemplateId && t.IsActive, ct))
            throw new NotFoundException("Salary template", request.SalaryTemplateId);

        var grade = request.SalaryGradeId is { } gradeId
            ? await db.SalaryGrades.AsNoTracking().FirstOrDefaultAsync(g => g.Id == gradeId && g.IsActive, ct)
              ?? throw new NotFoundException("Salary grade", gradeId)
            : null;

        var currency = request.CurrencyCode
            ?? await db.PayGroups.Where(g => g.Id == employee.PayGroupId).Select(g => g.CurrencyCode).FirstOrDefaultAsync(ct)
            ?? grade?.CurrencyCode
            ?? await db.PayrollSettings.Select(s => s.BaseCurrency).FirstOrDefaultAsync(ct)
            ?? throw new ConflictException("Currency could not be determined. Initialize payroll or pass a currency.");

        var current = await db.EmployeeSalaries.Include(s => s.Overrides)
            .FirstOrDefaultAsync(s => s.EmployeeId == employee.Id && s.EffectiveTo == null, ct);

        var salary = EmployeeSalary.Create(
            currentUser.RequireTenantId(), employee.Id, request.SalaryTemplateId, request.SalaryGradeId, currency,
            request.SalaryBasis, request.BasisAmount, request.EffectiveFrom, request.ChangeReason, request.Remarks);

        if (current is not null)
            CopyOverrides(current, salary);

        // Purani band + nayi add: filtered unique index (ek hi current) ki wajah se do SaveChanges, ek transaction
        await db.ExecuteInTransactionAsync(async token =>
        {
            if (current is not null)
            {
                current.End(request.EffectiveFrom.AddDays(-1));
                await db.SaveChangesAsync(token);
            }

            db.EmployeeSalaries.Add(salary);
            await db.SaveChangesAsync(token);
        }, ct);

        return new AssignSalaryResult(salary.Id, BandWarning(grade, request.SalaryBasis, request.BasisAmount));
    }

    public async Task<IReadOnlyList<EmployeeSalaryDto>> Handle(GetSalaryHistoryQuery request, CancellationToken ct)
        => await db.EmployeeSalaries.AsNoTracking()
            .Where(s => s.EmployeeId == request.EmployeeId)
            .OrderByDescending(s => s.EffectiveFrom)
            .Select(s => new EmployeeSalaryDto(
                s.Id, s.SalaryTemplateId,
                db.SalaryTemplates.Where(t => t.Id == s.SalaryTemplateId).Select(t => t.Name).FirstOrDefault() ?? "",
                s.SalaryGradeId, s.CurrencyCode, s.SalaryBasis, s.BasisAmount, s.EffectiveFrom, s.EffectiveTo,
                s.ChangeReason, s.Remarks,
                s.Overrides.Select(o => new SalaryOverrideDto(o.PayComponentId, o.IsExcluded, o.CalcType, o.Amount, o.Percentage, o.BaseComponentId)).ToList()))
            .ToListAsync(ct);

    public async Task Handle(SetSalaryOverrideCommand request, CancellationToken ct)
    {
        var salary = await GetCurrentAsync(request.EmployeeId, ct);
        await EnsureComponentAsync(request.PayComponentId, ct);
        if (request.BaseComponentId is { } baseId)
            await EnsureComponentAsync(baseId, ct);

        salary.SetOverride(request.PayComponentId, request.CalcType, request.Amount, request.Percentage, request.BaseComponentId);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(ExcludeSalaryComponentCommand request, CancellationToken ct)
    {
        var salary = await GetCurrentAsync(request.EmployeeId, ct);
        await EnsureComponentAsync(request.PayComponentId, ct);
        salary.ExcludeComponent(request.PayComponentId);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(RemoveSalaryOverrideCommand request, CancellationToken ct)
    {
        var salary = await GetCurrentAsync(request.EmployeeId, ct);
        salary.RemoveOverride(request.PayComponentId);
        await db.SaveChangesAsync(ct);
    }

    private async Task<EmployeeSalary> GetCurrentAsync(Guid employeeId, CancellationToken ct)
        => await db.EmployeeSalaries.Include(s => s.Overrides)
               .FirstOrDefaultAsync(s => s.EmployeeId == employeeId && s.EffectiveTo == null, ct)
           ?? throw new NotFoundException("Current salary for employee", employeeId);

    private async Task EnsureComponentAsync(Guid componentId, CancellationToken ct)
    {
        if (!await db.PayComponents.AnyAsync(c => c.Id == componentId && c.IsActive, ct))
            throw new NotFoundException("Pay component", componentId);
    }

    private static void CopyOverrides(EmployeeSalary from, EmployeeSalary to)
    {
        foreach (var item in from.Overrides)
        {
            if (item.IsExcluded)
                to.ExcludeComponent(item.PayComponentId);
            else if (item.Formula is { } f)
                to.SetOverride(item.PayComponentId, f.CalcType, f.Amount, f.Percentage, f.BaseComponentId);
        }
    }

    private static string? BandWarning(SalaryGrade? grade, SalaryBasis basis, decimal amount)
    {
        if (grade is null || basis == SalaryBasis.Hourly)
            return null;

        var annual = SalaryMath.AnnualEquivalent(basis, amount);
        return grade.IsWithinBand(annual)
            ? null
            : $"Annual salary {annual:N0} is outside grade {grade.Code} band ({grade.MinAnnual:N0} – {grade.MaxAnnual:N0}).";
    }
}
