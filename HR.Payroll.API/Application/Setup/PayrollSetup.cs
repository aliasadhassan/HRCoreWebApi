namespace HR.Payroll.API.Application.Setup;

using FluentValidation;
using HR.Payroll.API.Application.Common.Exceptions;
using HR.Payroll.API.Application.Common.Interfaces;
using HR.Payroll.API.Domain.Common;
using HR.Payroll.API.Domain.Setup;
using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record PayrollSettingsDto(
    string BaseCurrency, ProrationMethod ProrationMethod, byte RoundingDecimals, string PayslipNumberPrefix, bool RequireApproval);

/// <summary>
/// Tenant ka payroll "on" karna: settings + system components (BASIC, INCOME_TAX).
/// Idempotent — dobara chalao to kuch duplicate nahi banta.
/// (Aage TenantCreated event pe khud chalega.)
/// </summary>
public sealed record InitializePayrollCommand(string BaseCurrency) : IRequest;
public sealed record GetPayrollSettingsQuery : IRequest<PayrollSettingsDto>;
public sealed record UpdatePayrollSettingsCommand(
    string BaseCurrency, ProrationMethod ProrationMethod, byte RoundingDecimals, string PayslipNumberPrefix, bool RequireApproval) : IRequest;

public sealed class InitializePayrollValidator : AbstractValidator<InitializePayrollCommand>
{
    public InitializePayrollValidator() => RuleFor(x => x.BaseCurrency).NotEmpty().Length(3);
}

public sealed class UpdatePayrollSettingsValidator : AbstractValidator<UpdatePayrollSettingsCommand>
{
    public UpdatePayrollSettingsValidator()
    {
        RuleFor(x => x.BaseCurrency).NotEmpty().Length(3);
        RuleFor(x => x.ProrationMethod).IsInEnum();
        RuleFor(x => x.RoundingDecimals).LessThanOrEqualTo((byte)4);
        RuleFor(x => x.PayslipNumberPrefix).NotEmpty().MaximumLength(10);
    }
}

public sealed class PayrollSetupHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<InitializePayrollCommand>,
    IRequestHandler<GetPayrollSettingsQuery, PayrollSettingsDto>,
    IRequestHandler<UpdatePayrollSettingsCommand>
{
    public async Task Handle(InitializePayrollCommand request, CancellationToken ct)
    {
        var tenantId = currentUser.RequireTenantId();

        if (!await db.PayrollSettings.AnyAsync(ct))
            db.PayrollSettings.Add(Domain.Setup.PayrollSettings.CreateDefault(tenantId, request.BaseCurrency));

        var existing = await db.PayComponents
            .Where(c => c.SystemCode != null)
            .Select(c => c.SystemCode!)
            .ToListAsync(ct);

        if (!existing.Contains(SystemComponentCodes.Basic))
            db.PayComponents.Add(PayComponent.Create(
                tenantId, SystemComponentCodes.Basic, "Basic Salary", ComponentType.Earning, CalcType.Fixed, null,
                isTaxable: true, isProrated: true, isRecurring: true, showOnPayslip: true, sortOrder: 1,
                systemCode: SystemComponentCodes.Basic));

        if (!existing.Contains(SystemComponentCodes.IncomeTax))
            db.PayComponents.Add(PayComponent.Create(
                tenantId, SystemComponentCodes.IncomeTax, "Income Tax", ComponentType.Deduction, CalcType.Variable, null,
                isTaxable: false, isProrated: false, isRecurring: true, showOnPayslip: true, sortOrder: 900,
                systemCode: SystemComponentCodes.IncomeTax));

        await db.SaveChangesAsync(ct);
    }

    public async Task<PayrollSettingsDto> Handle(GetPayrollSettingsQuery request, CancellationToken ct)
        => await db.PayrollSettings.AsNoTracking()
               .Select(s => new PayrollSettingsDto(s.BaseCurrency, s.ProrationMethod, s.RoundingDecimals, s.PayslipNumberPrefix, s.RequireApproval))
               .FirstOrDefaultAsync(ct)
           ?? throw new NotFoundException("Payroll settings", "current tenant (run initialize first)");

    public async Task Handle(UpdatePayrollSettingsCommand request, CancellationToken ct)
    {
        var settings = await db.PayrollSettings.FirstOrDefaultAsync(ct)
                       ?? throw new NotFoundException("Payroll settings", "current tenant (run initialize first)");

        settings.Update(request.BaseCurrency, request.ProrationMethod, request.RoundingDecimals, request.PayslipNumberPrefix, request.RequireApproval);
        await db.SaveChangesAsync(ct);
    }
}
