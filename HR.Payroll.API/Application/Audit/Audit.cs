namespace HR.Payroll.API.Application.Audit;

using HR.Payroll.API.Application.Common.Interfaces;
using HR.Shared.Library.Authorization;
using HR.Shared.Library.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

// Audit (Payroll ka hissa): log AppDbContext.SaveChanges khud likhta hai, yahan sirf parhna.
// Gateway: /payroll/audit/... Tankhwah ka itihaas hai, isliye payroll.view.all ya payroll.approve.

public sealed record GetPayrollAuditEntriesQuery(AuditFilter Filter) : IRequest<AuditPageDto>;

public sealed record GetPayrollAuditSummaryQuery(int Days, int OffsetMinutes) : IRequest<AuditSummaryDto>;

public sealed record GetPayrollAuditEntityTypesQuery : IRequest<IReadOnlyList<AuditCountDto>>;

public sealed class PayrollAuditQueryHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<GetPayrollAuditEntriesQuery, AuditPageDto>,
    IRequestHandler<GetPayrollAuditSummaryQuery, AuditSummaryDto>,
    IRequestHandler<GetPayrollAuditEntityTypesQuery, IReadOnlyList<AuditCountDto>>
{
    private IQueryable<Domain.Audit.AuditLog> Logs()
    {
        if (!currentUser.HasPermission(Permissions.PayrollViewAll) && !currentUser.HasPermission(Permissions.PayrollApprove))
            throw new UnauthorizedAccessException("You do not have permission to see the payroll audit trail.");
        var tenantId = currentUser.RequireTenantId();
        return db.AuditLogs.AsNoTracking().Where(x => x.TenantId == tenantId);
    }

    public Task<AuditPageDto> Handle(GetPayrollAuditEntriesQuery request, CancellationToken ct)
        => AuditQueries.PageAsync(Logs(), request.Filter, SubjectNamesAsync, ct);

    public Task<AuditSummaryDto> Handle(GetPayrollAuditSummaryQuery request, CancellationToken ct)
        => AuditQueries.SummaryAsync(Logs(), request.Days, request.OffsetMinutes, DateTime.UtcNow, ct);

    public Task<IReadOnlyList<AuditCountDto>> Handle(GetPayrollAuditEntityTypesQuery request, CancellationToken ct)
        => AuditQueries.EntityTypesAsync(Logs(), ct);

    private async Task<Dictionary<Guid, string>> SubjectNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var tenantId = currentUser.RequireTenantId();
        return (await db.PayrollEmployees.IgnoreQueryFilters().AsNoTracking()
                .Where(e => e.TenantId == tenantId && ids.Contains(e.Id))
                .Select(e => new { e.Id, e.FullName, e.EmployeeCode })
                .ToListAsync(ct))
            .ToDictionary(e => e.Id, e => $"{e.FullName} · {e.EmployeeCode}");
    }
}
