namespace HR.Employee.API.Application.Policies;

using FluentValidation;
using HR.Employee.API.Application.Common.Exceptions;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Common;
using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Policies;
using MediatR;
using Microsoft.EntityFrameworkCore;

// Statutory calendar: tax returns, social security, licence renewals. settings.view dekhe, settings.manage chalaye.

#region DTOs + requests

public sealed record ObligationDto(
    Guid Id, string Title, string? Authority, ObligationCategory Category, ObligationFrequency Frequency, string? Description, string? LinkUrl,
    Guid? OwnerEmployeeId, string? OwnerName, short RemindDaysBefore, DateOnly? NextDueOn, bool IsActive, ObligationState State, int? DaysLeft,
    DateOnly? LastFiledOn, int FilingCount, int LateCount, uint RowVersion);

public sealed record FilingDto(
    Guid Id, DateOnly PeriodDueOn, DateOnly FiledOn, bool IsLate, string? Reference, string? EvidenceUrl, string? Notes,
    string? ByName, DateTime RecordedAt, bool CanUndo);

public sealed record ObligationDetailDto(ObligationDto Obligation, IReadOnlyList<FilingDto> Filings, bool CanManage);

public sealed record GetObligationsQuery : IRequest<IReadOnlyList<ObligationDto>>;
public sealed record GetObligationQuery(Guid Id) : IRequest<ObligationDetailDto>;

public sealed record SaveObligationRequest(
    string Title, string? Authority, ObligationCategory Category, ObligationFrequency Frequency, string? Description, string? LinkUrl,
    Guid? OwnerEmployeeId, short RemindDaysBefore, DateOnly? NextDueOn, bool IsActive);
public sealed record CreateObligationCommand(SaveObligationRequest Data) : IRequest<Guid>;
public sealed record UpdateObligationCommand(Guid Id, SaveObligationRequest Data) : IRequest;
public sealed record DeleteObligationCommand(Guid Id) : IRequest;

public sealed record RecordFilingRequest(DateOnly FiledOn, string? Reference, string? EvidenceUrl, string? Notes);
public sealed record RecordFilingCommand(Guid ObligationId, RecordFilingRequest Data) : IRequest<Guid>;
public sealed record UndoFilingCommand(Guid ObligationId, Guid FilingId) : IRequest;

public sealed class SaveObligationRequestValidator : AbstractValidator<SaveObligationRequest>
{
    public SaveObligationRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Authority).MaximumLength(100);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Frequency).IsInEnum();
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.LinkUrl).MaximumLength(1000);
        RuleFor(x => x.RemindDaysBefore).InclusiveBetween((short)0, (short)90);
    }
}

public sealed class CreateObligationValidator : AbstractValidator<CreateObligationCommand>
{
    public CreateObligationValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveObligationRequestValidator());
}

public sealed class UpdateObligationValidator : AbstractValidator<UpdateObligationCommand>
{
    public UpdateObligationValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveObligationRequestValidator());
}

public sealed class RecordFilingValidator : AbstractValidator<RecordFilingCommand>
{
    public RecordFilingValidator()
    {
        RuleFor(x => x.Data).NotNull();
        RuleFor(x => x.Data.Reference).MaximumLength(100);
        RuleFor(x => x.Data.EvidenceUrl).MaximumLength(1000);
        RuleFor(x => x.Data.Notes).MaximumLength(1000);
    }
}

#endregion

public sealed class ObligationQueryHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<GetObligationsQuery, IReadOnlyList<ObligationDto>>,
    IRequestHandler<GetObligationQuery, ObligationDetailDto>
{
    private readonly ComplianceAccess access = new(db, currentUser);

    public async Task<IReadOnlyList<ObligationDto>> Handle(GetObligationsQuery q, CancellationToken ct)
    {
        access.EnsureCanViewStatutory();
        var list = await db.ComplianceObligations.AsNoTracking().Include(o => o.Filings).ToListAsync(ct);
        var owners = await OwnersAsync(list, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return list.Select(o => ToDto(o, owners, today))
            .OrderBy(o => o.State switch { ObligationState.Overdue => 0, ObligationState.DueSoon => 1, ObligationState.Upcoming => 2, ObligationState.Paused => 3, _ => 4 })
            .ThenBy(o => o.NextDueOn).ThenBy(o => o.Title)
            .ToList();
    }

    public async Task<ObligationDetailDto> Handle(GetObligationQuery q, CancellationToken ct)
    {
        access.EnsureCanViewStatutory();
        var o = await db.ComplianceObligations.AsNoTracking().Include(x => x.Filings).FirstOrDefaultAsync(x => x.Id == q.Id, ct)
            ?? throw new NotFoundException("Obligation", q.Id);
        var owners = await OwnersAsync([o], ct);
        var byIds = o.Filings.Where(f => f.ByUserId != null).Select(f => f.ByUserId!.Value).Distinct().ToList();
        var names = await db.Employees.AsNoTracking().Where(e => e.UserId != null && byIds.Contains(e.UserId.Value))
            .Select(e => new { UserId = e.UserId!.Value, Name = e.FirstName + " " + e.LastName }).ToListAsync(ct);
        var last = o.LastPeriodDueOn;
        var filings = o.Filings.OrderByDescending(f => f.PeriodDueOn)
            .Select(f => new FilingDto(f.Id, f.PeriodDueOn, f.FiledOn, f.IsLate, f.Reference, f.EvidenceUrl, f.Notes,
                names.FirstOrDefault(n => n.UserId == f.ByUserId)?.Name, f.RecordedAt, access.CanManage && f.PeriodDueOn == last))
            .ToList();
        return new ObligationDetailDto(ToDto(o, owners, DateOnly.FromDateTime(DateTime.UtcNow)), filings, access.CanManage);
    }

    private async Task<Dictionary<Guid, string>> OwnersAsync(IReadOnlyCollection<ComplianceObligation> list, CancellationToken ct)
    {
        var ids = list.Where(o => o.OwnerEmployeeId != null).Select(o => o.OwnerEmployeeId!.Value).Distinct().ToList();
        return await db.Employees.AsNoTracking().Where(e => ids.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.FirstName + " " + e.LastName, ct);
    }

    private static ObligationDto ToDto(ComplianceObligation o, Dictionary<Guid, string> owners, DateOnly today) => new(
        o.Id, o.Title, o.Authority, o.Category, o.Frequency, o.Description, o.LinkUrl,
        o.OwnerEmployeeId, o.OwnerEmployeeId is { } id ? owners.GetValueOrDefault(id) : null, o.RemindDaysBefore, o.NextDueOn, o.IsActive,
        o.StateOn(today), o.NextDueOn is { } due ? due.DayNumber - today.DayNumber : null,
        o.Filings.Count == 0 ? null : o.Filings.Max(f => f.FiledOn), o.Filings.Count, o.Filings.Count(f => f.IsLate), o.RowVersion);
}

public sealed class ObligationCommandHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<CreateObligationCommand, Guid>,
    IRequestHandler<UpdateObligationCommand>,
    IRequestHandler<DeleteObligationCommand>,
    IRequestHandler<RecordFilingCommand, Guid>,
    IRequestHandler<UndoFilingCommand>
{
    private readonly ComplianceAccess access = new(db, currentUser);

    public async Task<Guid> Handle(CreateObligationCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var obligation = ComplianceObligation.Create(access.TenantId, await DetailsAsync(c.Data, null, ct));
        db.ComplianceObligations.Add(obligation);
        await SaveAsync(ct);
        return obligation.Id;
    }

    public async Task Handle(UpdateObligationCommand c, CancellationToken ct)
    {
        var obligation = await ObligationAsync(c.Id, ct);
        obligation.Update(await DetailsAsync(c.Data, obligation, ct));
        await SaveAsync(ct);
    }

    public async Task Handle(DeleteObligationCommand c, CancellationToken ct)
    {
        var obligation = await ObligationAsync(c.Id, ct);
        if (obligation.Filings.Count > 0)
            throw new ConflictException("This obligation has filings on record. Pause it instead of deleting it.");
        db.ComplianceObligations.Remove(obligation);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Guid> Handle(RecordFilingCommand c, CancellationToken ct)
    {
        var obligation = await ObligationAsync(c.ObligationId, ct);
        var d = c.Data;
        var filing = obligation.RecordFiling(d.FiledOn, d.Reference, d.EvidenceUrl, d.Notes, currentUser.UserId, DateTime.UtcNow);
        db.ComplianceFilings.Add(filing);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UX_ComplianceFilings_Obligation_Period") == true)
        {
            throw new ConflictException("This due date has already been filed. Refresh the page.");
        }
        return filing.Id;
    }

    public async Task Handle(UndoFilingCommand c, CancellationToken ct)
    {
        var obligation = await ObligationAsync(c.ObligationId, ct);
        var filing = obligation.Filings.FirstOrDefault(f => f.Id == c.FilingId) ?? throw new NotFoundException("Filing", c.FilingId);
        obligation.UndoFiling(filing);
        db.ComplianceFilings.Remove(filing);
        await db.SaveChangesAsync(ct);
    }

    private async Task<ComplianceObligation> ObligationAsync(Guid id, CancellationToken ct)
    {
        access.EnsureCanManage();
        return await db.ComplianceObligations.Include(o => o.Filings).FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new NotFoundException("Obligation", id);
    }

    private async Task<ObligationDetails> DetailsAsync(SaveObligationRequest r, ComplianceObligation? existing, CancellationToken ct)
    {
        if (r.OwnerEmployeeId is { } owner && owner != Guid.Empty && owner != existing?.OwnerEmployeeId
            && !await db.Employees.AnyAsync(e => e.Id == owner && e.EmploymentStatus != EmploymentStatus.Exited, ct))
            throw new DomainException("Pick an active employee as the owner.");
        var title = (r.Title ?? "").Trim().ToLower();
        var existingId = existing?.Id;
        if (await db.ComplianceObligations.AnyAsync(o => o.Id != existingId && o.Title.ToLower() == title, ct))
            throw new ConflictException("An obligation with this title already exists.");
        return new ObligationDetails(r.Title!, r.Authority, r.Category, r.Frequency, r.Description, r.LinkUrl, r.OwnerEmployeeId,
            r.RemindDaysBefore, r.NextDueOn, r.IsActive);
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UX_ComplianceObligations_TenantId_Title") == true)
        {
            throw new ConflictException("An obligation with this title already exists.");
        }
    }
}
