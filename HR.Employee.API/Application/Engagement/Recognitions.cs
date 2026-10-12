namespace HR.Employee.API.Application.Engagement;

using FluentValidation;
using HR.Employee.API.Application.Common.Exceptions;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Engagement;
using MediatR;
using Microsoft.EntityFrameworkCore;

// Recognition: shabash feed, cheers, leaderboard.

#region DTOs

public sealed record RecognitionDto(
    Guid Id, Guid FromEmployeeId, string FromName, Guid ToEmployeeId, string ToName, string ToDetail,
    RecognitionValue Value, string Message, DateTime CreatedAt, int Cheers, bool ICheered, bool IsMine, bool CanDelete);

public sealed record RecognitionLeaderDto(Guid EmployeeId, string Name, string Detail, int Count);
public sealed record RecognitionValueCountDto(RecognitionValue Value, int Count);

public sealed record RecognitionStatsDto(
    string Period, DateOnly From, int Total, int PreviousTotal, int People,
    IReadOnlyList<RecognitionLeaderDto> TopReceivers, IReadOnlyList<RecognitionLeaderDto> TopGivers,
    IReadOnlyList<RecognitionValueCountDto> ByValue);

#endregion

#region Requests

/// <summary>Scope: all | received | given (mere). EmployeeId = kisi aur ki di/mili (all scope ke saath).</summary>
public sealed record GetRecognitionsQuery(
    string? Scope, Guid? EmployeeId, RecognitionValue? Value, Guid? DepartmentId, DateTime? Before, int Take = 50)
    : IRequest<IReadOnlyList<RecognitionDto>>;

/// <summary>Period: month | quarter | year.</summary>
public sealed record GetRecognitionStatsQuery(string? Period) : IRequest<RecognitionStatsDto>;

public sealed record GiveRecognitionRequest(IReadOnlyList<Guid> ToEmployeeIds, RecognitionValue Value, string Message);
public sealed record GiveRecognitionCommand(GiveRecognitionRequest Data) : IRequest<int>;
public sealed record DeleteRecognitionCommand(Guid Id) : IRequest;
public sealed record CheerRecognitionCommand(Guid Id, bool On) : IRequest<int>;

public sealed class GiveRecognitionValidator : AbstractValidator<GiveRecognitionCommand>
{
    public GiveRecognitionValidator()
    {
        RuleFor(x => x.Data).NotNull();
        RuleFor(x => x.Data.ToEmployeeIds).NotEmpty().WithMessage("Pick who you are recognising.")
            .Must(ids => ids.Count <= 10).WithMessage("Recognise at most 10 people at a time.");
        RuleFor(x => x.Data.Value).IsInEnum();
        RuleFor(x => x.Data.Message).NotEmpty().MinimumLength(3).MaximumLength(500);
    }
}

#endregion

public sealed class RecognitionHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<GetRecognitionsQuery, IReadOnlyList<RecognitionDto>>,
    IRequestHandler<GetRecognitionStatsQuery, RecognitionStatsDto>,
    IRequestHandler<GiveRecognitionCommand, int>,
    IRequestHandler<DeleteRecognitionCommand>,
    IRequestHandler<CheerRecognitionCommand, int>
{
    private readonly EngagementAccess access = new(db, currentUser);

    public async Task<IReadOnlyList<RecognitionDto>> Handle(GetRecognitionsQuery q, CancellationToken ct)
    {
        var me = (await access.MeAsync(ct))?.Id;
        // Neeche Employees.IgnoreQueryFilters() poori query ke filters hata deta hai (exited log bhi dikhen), is liye tenant + delete khud check
        var tenant = access.TenantId;
        var rows = db.Recognitions.AsNoTracking().Where(x => x.TenantId == tenant && !x.IsDeleted);
        switch (q.Scope?.ToLowerInvariant())
        {
            case "received":
                rows = me is { } r ? rows.Where(x => x.ToEmployeeId == r) : rows.Where(_ => false);
                break;
            case "given":
                rows = me is { } g ? rows.Where(x => x.FromEmployeeId == g) : rows.Where(_ => false);
                break;
        }
        if (q.EmployeeId is { } emp)
            rows = rows.Where(x => x.ToEmployeeId == emp || x.FromEmployeeId == emp);
        if (q.Value is { } value)
            rows = rows.Where(x => x.Value == value);
        if (q.DepartmentId is { } dept)
            rows = rows.Where(x => db.Employees.Any(e => e.Id == x.ToEmployeeId && e.DepartmentId == dept));
        if (q.Before is { } before)
            rows = rows.Where(x => x.CreatedAt < before);

        var take = Math.Clamp(q.Take, 1, 200);
        var data = await rows.OrderByDescending(x => x.CreatedAt).Take(take)
            .Join(db.Employees.IgnoreQueryFilters(), x => x.FromEmployeeId, e => e.Id, (x, f) => new { x, FromName = f.FirstName + " " + f.LastName })
            .Join(db.Employees.IgnoreQueryFilters(), y => y.x.ToEmployeeId, e => e.Id, (y, t) => new
            {
                y.x, y.FromName, ToName = t.FirstName + " " + t.LastName, ToDetail = t.Designation.Title + " · " + t.Department.Name
            })
            .OrderByDescending(z => z.x.CreatedAt)
            .ToListAsync(ct);

        var ids = data.Select(d => d.x.Id).ToList();
        var cheers = await db.RecognitionCheers.AsNoTracking().Where(c => ids.Contains(c.RecognitionId))
            .GroupBy(c => c.RecognitionId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var mine = me is { } my
            ? (await db.RecognitionCheers.AsNoTracking().Where(c => c.EmployeeId == my && ids.Contains(c.RecognitionId))
                .Select(c => c.RecognitionId).ToListAsync(ct)).ToHashSet()
            : [];

        return data.Select(d => new RecognitionDto(
            d.x.Id, d.x.FromEmployeeId, d.FromName, d.x.ToEmployeeId, d.ToName, d.ToDetail, d.x.Value, d.x.Message, d.x.CreatedAt,
            cheers.GetValueOrDefault(d.x.Id), mine.Contains(d.x.Id), d.x.FromEmployeeId == me,
            access.CanManage || d.x.FromEmployeeId == me)).ToList();
    }

    public async Task<RecognitionStatsDto> Handle(GetRecognitionStatsQuery q, CancellationToken ct)
    {
        var today = EngagementClock.Today;
        var period = q.Period?.ToLowerInvariant() is "quarter" or "year" ? q.Period.ToLowerInvariant() : "month";
        var from = period switch
        {
            "quarter" => new DateOnly(today.Year, (today.Month - 1) / 3 * 3 + 1, 1),
            "year" => new DateOnly(today.Year, 1, 1),
            _ => new DateOnly(today.Year, today.Month, 1)
        };
        var previous = period switch { "quarter" => from.AddMonths(-3), "year" => from.AddYears(-1), _ => from.AddMonths(-1) };
        var start = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var prevStart = previous.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var rows = await db.Recognitions.AsNoTracking().Where(r => r.CreatedAt >= start)
            .Select(r => new { r.FromEmployeeId, r.ToEmployeeId, r.Value }).ToListAsync(ct);
        var previousTotal = await db.Recognitions.CountAsync(r => r.CreatedAt >= prevStart && r.CreatedAt < start, ct);

        var top = rows.GroupBy(r => r.ToEmployeeId).Select(g => (Id: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count).Take(5).ToList();
        var givers = rows.GroupBy(r => r.FromEmployeeId).Select(g => (Id: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count).Take(5).ToList();
        var ids = top.Concat(givers).Select(x => x.Id).Distinct().ToList();
        var people = await db.Employees.AsNoTracking().IgnoreQueryFilters().Where(e => ids.Contains(e.Id))
            .Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName, Detail = e.Designation.Title + " · " + e.Department.Name })
            .ToDictionaryAsync(e => e.Id, ct);

        List<RecognitionLeaderDto> Leaders(List<(Guid Id, int Count)> list) => list
            .Where(x => people.ContainsKey(x.Id))
            .Select(x => new RecognitionLeaderDto(x.Id, people[x.Id].Name, people[x.Id].Detail, x.Count))
            .OrderByDescending(x => x.Count).ThenBy(x => x.Name).ToList();

        var byValue = Enum.GetValues<RecognitionValue>()
            .Select(v => new RecognitionValueCountDto(v, rows.Count(r => r.Value == v))).ToList();
        return new RecognitionStatsDto(period, from, rows.Count, previousTotal, rows.Select(r => r.ToEmployeeId).Distinct().Count(),
            Leaders(top), Leaders(givers), byValue);
    }

    public async Task<int> Handle(GiveRecognitionCommand c, CancellationToken ct)
    {
        var me = await access.RequireMeAsync(ct);
        var ids = c.Data.ToEmployeeIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Contains(me.Id))
            throw new Domain.Common.DomainException("You cannot recognise yourself.");
        var active = await db.Employees.Where(e => ids.Contains(e.Id) && e.EmploymentStatus != EmploymentStatus.Exited).Select(e => e.Id).ToListAsync(ct);
        if (active.Count != ids.Count)
            throw new Domain.Common.DomainException("Pick people who currently work here.");
        foreach (var id in active)
            db.Recognitions.Add(Recognition.Create(access.TenantId, me.Id, id, c.Data.Value, c.Data.Message));
        await db.SaveChangesAsync(ct);
        return active.Count;
    }

    public async Task Handle(DeleteRecognitionCommand c, CancellationToken ct)
    {
        var recognition = await db.Recognitions.FirstOrDefaultAsync(r => r.Id == c.Id, ct) ?? throw new NotFoundException("Recognition", c.Id);
        var me = (await access.MeAsync(ct))?.Id;
        if (!access.CanManage && recognition.FromEmployeeId != me)
            throw new UnauthorizedAccessException("You can only remove recognition you gave.");
        db.Recognitions.Remove(recognition);
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> Handle(CheerRecognitionCommand c, CancellationToken ct)
    {
        var me = await access.RequireMeAsync(ct);
        if (!await db.Recognitions.AnyAsync(r => r.Id == c.Id, ct))
            throw new NotFoundException("Recognition", c.Id);
        var existing = await db.RecognitionCheers.FirstOrDefaultAsync(x => x.RecognitionId == c.Id && x.EmployeeId == me.Id, ct);
        if (c.On && existing is null)
            db.RecognitionCheers.Add(RecognitionCheer.Create(c.Id, me.Id, DateTime.UtcNow));
        else if (!c.On && existing is not null)
            db.RecognitionCheers.Remove(existing);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UX_RecognitionCheers_Recognition_Employee") == true)
        {
            // Doosri tab se pehle hi cheer ho gaya: theek hai
        }
        return await db.RecognitionCheers.CountAsync(x => x.RecognitionId == c.Id, ct);
    }
}
