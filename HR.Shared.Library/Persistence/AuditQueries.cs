namespace HR.Shared.Library.Persistence;

using Microsoft.EntityFrameworkCore;

public sealed record AuditEntryDto(
    Guid Id, DateTime At, Guid? UserId, string? UserName, AuditAction Action, string EntityType, Guid EntityId,
    string? EntityLabel, Guid? SubjectEmployeeId, string? SubjectName, string? Operation, Guid CorrelationId,
    IReadOnlyList<AuditFieldChange> Changes);

/// <summary>
/// Keyset page. <see cref="Next"/> agle page ka "before" hai (null = aur kuch nahi).
/// Ek SaveChanges ke saare rows ka At ek jaisa hota hai, isliye page kabhi ek timestamp ke beech nahi kat-ta.
/// </summary>
public sealed record AuditPageDto(IReadOnlyList<AuditEntryDto> Items, DateTime? Next);

public sealed record AuditCountDto(string Key, int Count);

public sealed record AuditUserCountDto(Guid? UserId, string? UserName, int Count);

public sealed record AuditDayDto(DateOnly Date, int Created, int Updated, int Deleted);

public sealed record AuditSummaryDto(
    int Days, int Total, int Created, int Updated, int Deleted, int Operations, int ActiveUsers,
    IReadOnlyList<AuditDayDto> ByDay, IReadOnlyList<AuditUserCountDto> TopUsers, IReadOnlyList<AuditCountDto> TopEntities);

public sealed record AuditFilter(
    DateTime? Before = null, DateTime? From = null, DateTime? To = null, Guid? UserId = null, string? EntityType = null,
    Guid? EntityId = null, Guid? SubjectEmployeeId = null, AuditAction? Action = null, string? Search = null, int Limit = 50);

public static class AuditQueries
{
    public const int MaxLimit = 200;
    public const int MaxSummaryDays = 90;

    /// <param name="source">Pehle se tenant pe filter ki hui AuditLogs.</param>
    /// <param name="subjectNames">Employee ids → naam (Employee / PayrollEmployee table se). Null = naam nahi.</param>
    public static async Task<AuditPageDto> PageAsync(
        IQueryable<AuditLog> source, AuditFilter f,
        Func<IReadOnlyCollection<Guid>, CancellationToken, Task<Dictionary<Guid, string>>>? subjectNames,
        CancellationToken ct)
    {
        var limit = Math.Clamp(f.Limit, 1, MaxLimit);
        var q = Filter(source, f);

        var first = await q.OrderByDescending(x => x.At).Select(x => x.At).Take(limit + 1).ToListAsync(ct);
        if (first.Count == 0)
            return new AuditPageDto([], null);

        DateTime? next = null;
        if (first.Count > limit)
        {
            var cut = first[limit - 1];
            q = q.Where(x => x.At >= cut);
            next = cut;
        }

        var rows = await q.OrderByDescending(x => x.At).ThenBy(x => x.EntityType).ThenBy(x => x.Id).ToListAsync(ct);

        if (next is { } n && !await Filter(source, f with { Before = n }).AnyAsync(ct))
            next = null;

        var ids = rows.Where(r => r.SubjectEmployeeId is not null).Select(r => r.SubjectEmployeeId!.Value).Distinct().ToList();
        var names = subjectNames is null || ids.Count == 0 ? [] : await subjectNames(ids, ct);

        return new AuditPageDto(rows.Select(r => new AuditEntryDto(
            r.Id, r.At, r.UserId, r.UserName, r.Action, r.EntityType, r.EntityId, r.EntityLabel, r.SubjectEmployeeId,
            r.SubjectEmployeeId is { } s && names.TryGetValue(s, out var name) ? name : null,
            r.Operation, r.CorrelationId, AuditTrail.Deserialize(r.Changes))).ToList(), next);
    }

    /// <param name="offsetMinutes">Browser ka UTC offset (Pakistan = 300) — din ki bucketing local din pe ho.</param>
    public static async Task<AuditSummaryDto> SummaryAsync(
        IQueryable<AuditLog> source, int days, int offsetMinutes, DateTime utcNow, CancellationToken ct)
    {
        days = Math.Clamp(days, 1, MaxSummaryDays);
        offsetMinutes = Math.Clamp(offsetMinutes, -14 * 60, 14 * 60);
        var offset = TimeSpan.FromMinutes(offsetMinutes);
        var today = DateOnly.FromDateTime(utcNow + offset);
        var start = DateTime.SpecifyKind(today.AddDays(1 - days).ToDateTime(TimeOnly.MinValue) - offset, DateTimeKind.Utc);

        var q = source.Where(x => x.At >= start);

        // Halki projection, phir memory mein — 90 din ka audit bhi chand hazaar rows
        var rows = await q
            .GroupBy(x => new { x.At, x.Action, x.UserId, x.UserName, x.EntityType, x.CorrelationId })
            .Select(g => new { g.Key.At, g.Key.Action, g.Key.UserId, g.Key.UserName, g.Key.EntityType, g.Key.CorrelationId, Count = g.Count() })
            .ToListAsync(ct);

        int Count(AuditAction a) => rows.Where(r => r.Action == a).Sum(r => r.Count);

        var byDay = rows
            .GroupBy(r => DateOnly.FromDateTime(r.At + offset))
            .ToDictionary(g => g.Key, g => g.ToList());
        var dayList = Enumerable.Range(0, days).Select(i => today.AddDays(i - days + 1)).Select(d =>
        {
            var list = byDay.GetValueOrDefault(d) ?? [];
            return new AuditDayDto(d,
                list.Where(r => r.Action == AuditAction.Created).Sum(r => r.Count),
                list.Where(r => r.Action == AuditAction.Updated).Sum(r => r.Count),
                list.Where(r => r.Action == AuditAction.Deleted).Sum(r => r.Count));
        }).ToList();

        var users = rows.Where(r => r.UserId is not null)
            .GroupBy(r => r.UserId)
            .Select(g => new AuditUserCountDto(g.Key, g.OrderByDescending(r => r.At).First().UserName, g.Sum(r => r.Count)))
            .OrderByDescending(u => u.Count).ToList();

        var entities = rows.GroupBy(r => r.EntityType)
            .Select(g => new AuditCountDto(g.Key, g.Sum(r => r.Count)))
            .OrderByDescending(e => e.Count).ThenBy(e => e.Key).Take(10).ToList();

        return new AuditSummaryDto(
            days, rows.Sum(r => r.Count), Count(AuditAction.Created), Count(AuditAction.Updated), Count(AuditAction.Deleted),
            rows.Select(r => r.CorrelationId).Distinct().Count(), users.Count, dayList, users.Take(10).ToList(), entities);
    }

    /// <summary>Filter dropdown: is tenant mein kaun kaun si cheezen audit mein aayi hain.</summary>
    public static async Task<IReadOnlyList<AuditCountDto>> EntityTypesAsync(IQueryable<AuditLog> source, CancellationToken ct)
       
        => (await source.GroupBy(x => x.EntityType).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct))
            .Select(x => new AuditCountDto(x.Key, x.Count)).OrderBy(x => x.Key).ToList();

    private static IQueryable<AuditLog> Filter(IQueryable<AuditLog> q, AuditFilter f)
    {
        if (f.Before is { } before) q = q.Where(x => x.At < before);
        if (f.From is { } from) q = q.Where(x => x.At >= from);
        if (f.To is { } to) q = q.Where(x => x.At < to);
        if (f.UserId is { } user) q = q.Where(x => x.UserId == user);
        if (!string.IsNullOrWhiteSpace(f.EntityType)) q = q.Where(x => x.EntityType == f.EntityType);
        if (f.EntityId is { } entity) q = q.Where(x => x.EntityId == entity);
        if (f.SubjectEmployeeId is { } subject) q = q.Where(x => x.SubjectEmployeeId == subject);
        if (f.Action is { } action) q = q.Where(x => x.Action == action);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var pattern = "%" + f.Search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            q = q.Where(x => EF.Functions.ILike(x.EntityLabel ?? "", pattern)
                || EF.Functions.ILike(x.UserName ?? "", pattern)
                || EF.Functions.ILike(x.EntityType, pattern)
                || EF.Functions.ILike(x.Operation ?? "", pattern));
        }
        return q;
    }
}
