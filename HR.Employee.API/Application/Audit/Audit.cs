namespace HR.Employee.API.Application.Audit;

using HR.Employee.API.Application.Common.Interfaces;
using HR.Shared.Library.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

// Audit (Employee service ka hissa): log AppDbContext.SaveChanges khud likhta hai, yahan sirf parhna.
// Gateway: /audit/entries, /audit/summary, /audit/entity-types. Payroll + Identity ke apne /api/.../audit hain;
// Angular teeno ko jod kar ek list dikhata hai.

public sealed record GetAuditEntriesQuery(AuditFilter Filter) : IRequest<AuditPageDto>;

public sealed record GetAuditSummaryQuery(int Days, int OffsetMinutes) : IRequest<AuditSummaryDto>;

public sealed record GetAuditEntityTypesQuery : IRequest<IReadOnlyList<AuditCountDto>>;

public sealed class AuditQueryHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<GetAuditEntriesQuery, AuditPageDto>,
    IRequestHandler<GetAuditSummaryQuery, AuditSummaryDto>,
    IRequestHandler<GetAuditEntityTypesQuery, IReadOnlyList<AuditCountDto>>
{
    private IQueryable<Domain.Audit.AuditLog> Logs()
    {
        var tenantId = currentUser.RequireTenantId();
        return db.AuditLogs.AsNoTracking().Where(x => x.TenantId == tenantId);
    }

    public Task<AuditPageDto> Handle(GetAuditEntriesQuery request, CancellationToken ct)
        => AuditQueries.PageAsync(Logs(), request.Filter, SubjectNamesAsync, ct);

    public Task<AuditSummaryDto> Handle(GetAuditSummaryQuery request, CancellationToken ct)
        => AuditQueries.SummaryAsync(Logs(), request.Days, request.OffsetMinutes, DateTime.UtcNow, ct);

    public Task<IReadOnlyList<AuditCountDto>> Handle(GetAuditEntityTypesQuery request, CancellationToken ct)
        => AuditQueries.EntityTypesAsync(Logs(), ct);

    // Hataye gaye (soft delete) employees ka naam bhi chahiye — audit mein wohi to dikhte hain
    private async Task<Dictionary<Guid, string>> SubjectNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var tenantId = currentUser.RequireTenantId();
        return (await db.Employees.IgnoreQueryFilters().AsNoTracking()
                .Where(e => e.TenantId == tenantId && ids.Contains(e.Id))
                .Select(e => new { e.Id, e.FirstName, e.LastName, e.EmployeeCode })
                .ToListAsync(ct))
            .ToDictionary(e => e.Id, e => $"{e.FirstName} {e.LastName} · {e.EmployeeCode}");
    }
}
