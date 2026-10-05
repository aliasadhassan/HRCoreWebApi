namespace HR.Employee.API.Application.Leaves;

using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Leaves;
using Microsoft.EntityFrameworkCore;
using EmployeeEntity = HR.Employee.API.Domain.Employees.Employee;

/// <summary>
/// Policy se balance: har eligible leave type ka saal ka balance bana/refresh karta hai.
///   - Upfront: saal ke shuru mein poori entitlement (beech saal join kiya to baqi mahino ke hisaab se)
///   - Monthly: har guzra mahina (current samait) 1/12
///   - Carry forward: pichle saal ka Available, MaxCarryForward tak (naya balance bante waqt ek dafa)
/// Entitled hamesha policy se aata hai; HR ki manual tabdeeli Adjusted mein rehti hai.
/// </summary>
public sealed class LeaveEntitlements(IAppDbContext db)
{
    private static readonly DateOnly FarFuture = new(9000, 1, 1);

    /// <summary>Location ki active policy, warna tenant default (LocationId null).</summary>
    public async Task<LeavePolicy?> PolicyForAsync(Guid locationId, CancellationToken ct)
    {
        var policies = await db.LeavePolicies.AsNoTracking()
            .Include(p => p.Rules)
            .Where(p => p.IsActive && (p.LocationId == locationId || p.LocationId == null))
            .ToListAsync(ct);
        return policies.FirstOrDefault(p => p.LocationId == locationId) ?? policies.FirstOrDefault(p => p.LocationId == null);
    }

    /// <summary>Tracked balances (SaveChanges caller karta hai).</summary>
    public async Task<List<LeaveBalance>> EnsureBalancesAsync(EmployeeEntity employee, short year, DateOnly today, CancellationToken ct)
    {
        var balances = await db.LeaveBalances
            .Where(b => b.EmployeeId == employee.Id && b.LeaveYear == year)
            .ToListAsync(ct);

        // Join se pehle ya agle saal ke baad ka balance banane ka faida nahi
        if (year < employee.JoiningDate.Year || year > today.Year + 1)
            return balances;

        var policy = await PolicyForAsync(employee.LocationId, ct);
        if (policy is null)
            return balances;

        var previous = await db.LeaveBalances.AsNoTracking()
            .Where(b => b.EmployeeId == employee.Id && b.LeaveYear == year - 1)
            .ToListAsync(ct);

        foreach (var rule in policy.Rules)
        {
            // Sirf gender / employment type yahan (bohat aage ki date = min service guzar chuki);
            // min service submit pe start date ke hisaab se check hoti hai
            if (!rule.IsEligible(employee, FarFuture))
                continue;

            var entitled = Entitlement(rule, employee.JoiningDate, year, today);
            var balance = balances.FirstOrDefault(b => b.LeaveTypeId == rule.LeaveTypeId);
            if (balance is null)
            {
                var prev = previous.FirstOrDefault(b => b.LeaveTypeId == rule.LeaveTypeId);
                var carried = prev is null ? 0 : Math.Clamp(prev.Available, 0, rule.MaxCarryForward);
                balance = LeaveBalance.Create(employee.TenantId, employee.Id, rule.LeaveTypeId, year, entitled, carried);
                db.LeaveBalances.Add(balance);
                balances.Add(balance);
            }
            else if (balance.Entitled != entitled)
            {
                balance.SetEntitlement(entitled);
            }
        }

        return balances;
    }

    public static decimal Entitlement(LeavePolicyRule rule, DateOnly joiningDate, short year, DateOnly today)
    {
        // Pehla mahina jis ka haq banta hai (beech saal join = us mahine se)
        var firstMonth = joiningDate.Year == year ? joiningDate.Month : 1;
        if (joiningDate.Year > year)
            return 0;

        int months;
        if (rule.AccrualMethod == AccrualMethod.Monthly)
        {
            var lastMonth = year < today.Year ? 12 : year > today.Year ? 0 : today.Month;
            months = Math.Max(0, lastMonth - firstMonth + 1);
        }
        else
        {
            months = 12 - firstMonth + 1;
        }

        return RoundToHalf(rule.AnnualEntitlement * months / 12m);
    }

    private static decimal RoundToHalf(decimal value) => Math.Round(value * 2, MidpointRounding.AwayFromZero) / 2;
}
