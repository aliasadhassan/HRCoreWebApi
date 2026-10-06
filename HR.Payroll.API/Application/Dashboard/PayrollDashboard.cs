namespace HR.Payroll.API.Application.Dashboard;

using HR.Payroll.API.Application.Common.Interfaces;
using HR.Payroll.API.Domain.Common;
using HR.Shared.Library.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

// Dashboard (pay side): sab se taaza payroll run ka "banknote", us run ka department-wise kharcha,
// agla payday, aur payroll.approve walon ke liye pending kaam (run approval, loan requests).
// Sirf payroll.view.all / payroll.approve — company ka payroll har kisi ko nahi dikhta.

public sealed record DashboardRunDto(
    Guid Id, string PayGroupName, DateOnly PeriodStart, DateOnly PeriodEnd, DateOnly PayDate, RunStatus Status, string CurrencyCode,
    int Employees, decimal Gross, decimal Deductions, decimal TaxWithheld, decimal EmployerCost, decimal Net);

public sealed record DepartmentCostDto(string Name, int Headcount, decimal MonthlyCost);

public sealed record PayrollTaskDto(Guid Id, string Kind, string? EmployeeName, decimal? Amount, string? CurrencyCode, int? Count);

public sealed record PayrollDashboardDto(
    DashboardRunDto? LatestRun, IReadOnlyList<DepartmentCostDto> Departments, DateOnly? NextPayDate, IReadOnlyList<PayrollTaskDto> Tasks);

public sealed record GetPayrollDashboardQuery : IRequest<PayrollDashboardDto>;

public sealed class PayrollDashboardHandler(IAppDbContext db, ICurrentUser currentUser) : IRequestHandler<GetPayrollDashboardQuery, PayrollDashboardDto>
{
    public async Task<PayrollDashboardDto> Handle(GetPayrollDashboardQuery q, CancellationToken ct)
    {
        var canApprove = currentUser.HasPermission(Permissions.PayrollApprove);
        if (!currentUser.HasPermission(Permissions.PayrollViewAll) && !canApprove)
            throw new UnauthorizedAccessException("You do not have permission to see company payroll.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Regular run, cancel/fail wale nahi; sab se naya period pehle
        var latest = await (
                from r in db.PayrollRuns.AsNoTracking()
                join g in db.PayGroups.AsNoTracking() on r.PayGroupId equals g.Id
                join p in db.PayPeriods.AsNoTracking() on r.PayPeriodId equals p.Id
                where r.RunType == RunType.Regular && r.Status != RunStatus.Cancelled && r.Status != RunStatus.Failed
                      && r.Status != RunStatus.Draft
                orderby p.PeriodStart descending, r.CalculatedAt descending
                select new { r.Id, GroupName = g.Name, p.PeriodStart, p.PeriodEnd, p.PayDate, r.Status, r.CurrencyCode,
                             r.TotalEmployees, r.TotalGross, r.TotalDeductions, r.TotalEmployerCost, r.TotalNet })
            .FirstOrDefaultAsync(ct);

        DashboardRunDto? run = null;
        IReadOnlyList<DepartmentCostDto> departments = [];
        if (latest is not null)
        {
            var slips = db.Payslips.AsNoTracking().Where(s => s.PayrollRunId == latest.Id);
            var tax = await slips.SumAsync(s => s.TaxAmount, ct);

            run = new DashboardRunDto(latest.Id, latest.GroupName, latest.PeriodStart, latest.PeriodEnd, latest.PayDate, latest.Status,
                latest.CurrencyCode, latest.TotalEmployees, latest.TotalGross, latest.TotalDeductions, tax, latest.TotalEmployerCost, latest.TotalNet);

            var byDepartment = await slips
                .GroupBy(s => s.DepartmentName)
                .Select(g => new { Name = g.Key, Count = g.Count(), Gross = g.Sum(s => s.GrossEarnings), Employer = g.Sum(s => s.EmployerContributions) })
                .ToListAsync(ct);
            departments = byDepartment
                .Select(d => new DepartmentCostDto(d.Name ?? "", d.Count, d.Gross + d.Employer))
                .OrderByDescending(d => d.MonthlyCost)
                .ToList();
        }

        var nextPayDate = await db.PayPeriods.AsNoTracking()
            .Where(p => p.PayDate >= today)
            .OrderBy(p => p.PayDate)
            .Select(p => (DateOnly?)p.PayDate)
            .FirstOrDefaultAsync(ct);

        var tasks = new List<PayrollTaskDto>();
        if (canApprove)
        {
            var awaiting = await db.PayrollRuns.AsNoTracking()
                .Where(r => r.Status == RunStatus.Calculated)
                .Select(r => new PayrollTaskDto(r.Id, "runApproval", null, r.TotalNet, r.CurrencyCode, r.TotalEmployees))
                .ToListAsync(ct);
            tasks.AddRange(awaiting);

            var loans = await db.LoanRequests.AsNoTracking()
                .Where(r => r.Status == LoanRequestStatus.Pending)
                .OrderBy(r => r.CreatedAt)
                .Take(20)
                .Join(db.PayrollEmployees, r => r.EmployeeId, e => e.Id, (r, e) => new PayrollTaskDto(
                    r.Id, r.LoanType == LoanType.SalaryAdvance ? "advanceRequest" : "loanRequest",
                    e.FullName, r.RequestedAmount, r.CurrencyCode, null))
                .ToListAsync(ct);
            tasks.AddRange(loans);
        }

        return new PayrollDashboardDto(run, departments, nextPayDate, tasks);
    }
}
