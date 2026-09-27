namespace HR.Payroll.API.Application.Setup;

using FluentValidation;
using HR.Payroll.API.Application.Common.Exceptions;
using HR.Payroll.API.Application.Common.Interfaces;
using HR.Payroll.API.Domain.Common;
using HR.Payroll.API.Domain.Setup;
using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record PayGroupDto(
    Guid Id, string Name, string Code, PayFrequency PayFrequency, string CountryCode, string CurrencyCode,
    DateOnly AnchorDate, short PayDayOffset, bool IsActive, int EmployeeCount);

public sealed record PayPeriodDto(
    Guid Id, DateOnly PeriodStart, DateOnly PeriodEnd, DateOnly PayDate, short FiscalYear, byte PeriodNumber, PayPeriodStatus Status);

public sealed record CreatePayGroupRequest(
    string Name, string Code, PayFrequency PayFrequency, string CountryCode, string CurrencyCode, DateOnly AnchorDate, short PayDayOffset);

public sealed record UpdatePayGroupRequest(string Name, string Code, short PayDayOffset);

public sealed record CreatePayGroupCommand(CreatePayGroupRequest Data) : IRequest<Guid>;
public sealed record UpdatePayGroupCommand(Guid Id, UpdatePayGroupRequest Data) : IRequest;
public sealed record GetPayGroupsQuery(bool IncludeInactive = false) : IRequest<IReadOnlyList<PayGroupDto>>;
public sealed record GeneratePayPeriodsCommand(Guid PayGroupId, int Count) : IRequest<int>;
public sealed record GetPayPeriodsQuery(Guid PayGroupId, short? Year) : IRequest<IReadOnlyList<PayPeriodDto>>;

public sealed class CreatePayGroupValidator : AbstractValidator<CreatePayGroupCommand>
{
    public CreatePayGroupValidator()
    {
        RuleFor(x => x.Data).NotNull();
        RuleFor(x => x.Data.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Data.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Data.PayFrequency).IsInEnum();
        RuleFor(x => x.Data.CountryCode).NotEmpty().Length(2);
        RuleFor(x => x.Data.CurrencyCode).NotEmpty().Length(3);
        RuleFor(x => x.Data.PayDayOffset).InclusiveBetween((short)0, (short)31);
    }
}

public sealed class UpdatePayGroupValidator : AbstractValidator<UpdatePayGroupCommand>
{
    public UpdatePayGroupValidator()
    {
        RuleFor(x => x.Data).NotNull();
        RuleFor(x => x.Data.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Data.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Data.PayDayOffset).InclusiveBetween((short)0, (short)31);
    }
}

public sealed class GeneratePayPeriodsValidator : AbstractValidator<GeneratePayPeriodsCommand>
{
    public GeneratePayPeriodsValidator() => RuleFor(x => x.Count).InclusiveBetween(1, 60);
}

public sealed class PayGroupHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<CreatePayGroupCommand, Guid>,
    IRequestHandler<UpdatePayGroupCommand>,
    IRequestHandler<GetPayGroupsQuery, IReadOnlyList<PayGroupDto>>,
    IRequestHandler<GeneratePayPeriodsCommand, int>,
    IRequestHandler<GetPayPeriodsQuery, IReadOnlyList<PayPeriodDto>>
{
    private const int InitialPeriods = 3;

    public async Task<Guid> Handle(CreatePayGroupCommand request, CancellationToken ct)
    {
        var d = request.Data;
        await EnsureCodeIsFreeAsync(d.Code, null, ct);

        var group = PayGroup.Create(currentUser.RequireTenantId(), d.Name, d.Code, d.PayFrequency,
                                    d.CountryCode, d.CurrencyCode, d.AnchorDate, d.PayDayOffset);
        db.PayGroups.Add(group);

        // Pehle 3 periods foran — HR ko khali screen na mile
        db.PayPeriods.AddRange(PayPeriodGenerator.GenerateNext(group, null, InitialPeriods));

        await db.SaveChangesAsync(ct);
        return group.Id;
    }

    public async Task Handle(UpdatePayGroupCommand request, CancellationToken ct)
    {
        var group = await db.PayGroups.FirstOrDefaultAsync(g => g.Id == request.Id, ct)
                    ?? throw new NotFoundException("Pay group", request.Id);

        await EnsureCodeIsFreeAsync(request.Data.Code, group.Id, ct);
        group.Update(request.Data.Name, request.Data.Code, request.Data.PayDayOffset);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PayGroupDto>> Handle(GetPayGroupsQuery request, CancellationToken ct)
        => await db.PayGroups.AsNoTracking()
            .Where(g => request.IncludeInactive || g.IsActive)
            .OrderBy(g => g.Name)
            .Select(g => new PayGroupDto(
                g.Id, g.Name, g.Code, g.PayFrequency, g.CountryCode, g.CurrencyCode, g.AnchorDate, g.PayDayOffset, g.IsActive,
                db.PayrollEmployees.Count(e => e.PayGroupId == g.Id && e.IsActive)))
            .ToListAsync(ct);

    public async Task<int> Handle(GeneratePayPeriodsCommand request, CancellationToken ct)
    {
        var group = await db.PayGroups.FirstOrDefaultAsync(g => g.Id == request.PayGroupId, ct)
                    ?? throw new NotFoundException("Pay group", request.PayGroupId);

        var last = await db.PayPeriods
            .Where(p => p.PayGroupId == group.Id)
            .OrderByDescending(p => p.PeriodStart)
            .FirstOrDefaultAsync(ct);

        var periods = PayPeriodGenerator.GenerateNext(group, last, request.Count);
        db.PayPeriods.AddRange(periods);
        await db.SaveChangesAsync(ct);
        return periods.Count;
    }

    public async Task<IReadOnlyList<PayPeriodDto>> Handle(GetPayPeriodsQuery request, CancellationToken ct)
        => await db.PayPeriods.AsNoTracking()
            .Where(p => p.PayGroupId == request.PayGroupId && (request.Year == null || p.FiscalYear == request.Year))
            .OrderBy(p => p.PeriodStart)
            .Select(p => new PayPeriodDto(p.Id, p.PeriodStart, p.PeriodEnd, p.PayDate, p.FiscalYear, p.PeriodNumber, p.Status))
            .ToListAsync(ct);

    private async Task EnsureCodeIsFreeAsync(string code, Guid? excludeId, CancellationToken ct)
    {
        var normalized = code.Trim().ToUpperInvariant();
        if (await db.PayGroups.AnyAsync(g => g.Code == normalized && g.Id != excludeId, ct))
            throw new ConflictException($"Pay group code '{normalized}' is already in use.");
    }
}
