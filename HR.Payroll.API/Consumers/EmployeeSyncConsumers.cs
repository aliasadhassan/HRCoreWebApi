namespace HR.Payroll.API.Consumers;

using HR.Payroll.API.Domain.Employees;
using HR.Payroll.API.Infrastructure.Persistence;
using HR.Shared.Library.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Employee API → Payroll ki local copy. Background consumer mein HTTP user/tenant nahi hota,
/// is liye IgnoreQueryFilters + tenant khud message se.
/// EF Inbox (Program.cs) same message dobara process nahi hone deta.
/// SentTime se out-of-order events pakde jate hain (PayrollEmployee.IsStale).
/// </summary>
public sealed class EmployeeCreatedConsumer(AppDbContext db, ILogger<EmployeeCreatedConsumer> logger)
    : IConsumer<EmployeeCreatedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<EmployeeCreatedIntegrationEvent> context)
    {
        var m = context.Message;
        var occurredAt = context.SentTime ?? DateTime.UtcNow;

        var employee = await db.PayrollEmployees.IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == m.EmployeeId, context.CancellationToken);

        if (employee is not null)
        {
            if (employee.TenantId != m.TenantId)
            {
                logger.LogError("Employee {EmployeeId} exists under a different tenant; message ignored.", m.EmployeeId);
                return;
            }

            employee.SyncProfile(m.EmployeeCode, m.FullName, m.WorkEmail, occurredAt);
        }
        else
        {
            db.PayrollEmployees.Add(PayrollEmployee.CreateFromSync(
                m.EmployeeId, m.TenantId, m.EmployeeCode, m.FullName, m.WorkEmail,
                m.DepartmentId, m.LocationId, m.EmploymentType, m.JoiningDate, occurredAt));
        }

        await db.SaveChangesAsync(context.CancellationToken);
        logger.LogInformation("Payroll employee {EmployeeCode} synced.", m.EmployeeCode);
    }
}

public sealed class EmployeeExitedConsumer(AppDbContext db, ILogger<EmployeeExitedConsumer> logger)
    : IConsumer<EmployeeExitedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<EmployeeExitedIntegrationEvent> context)
    {
        var m = context.Message;

        var employee = await db.PayrollEmployees.IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == m.EmployeeId && e.TenantId == m.TenantId, context.CancellationToken);

        if (employee is null)
        {
            // Created event abhi nahi aaya (out of order) — throw karo, MassTransit retry karega
            throw new InvalidOperationException($"Payroll employee {m.EmployeeId} not found yet; will retry.");
        }

        employee.SyncExit(m.ExitDate, context.SentTime ?? DateTime.UtcNow);
        await db.SaveChangesAsync(context.CancellationToken);
        logger.LogInformation("Payroll employee {EmployeeId} marked as exited on {ExitDate}.", m.EmployeeId, m.ExitDate);
    }
}
