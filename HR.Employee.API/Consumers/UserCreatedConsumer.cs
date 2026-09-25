namespace HR.Employee.API.Consumers;

using HR.Employee.API.Infrastructure.Persistence;
using HR.Shared.Library.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Flow: HR employee banata hai → invite → user register/SSO → UserCreatedEvent.
/// Yahan naya employee NAHI banta (fake data ke saath) — existing employee ko user se LINK karte hain.
/// EF Inbox (Program.cs) duplicate message ko dobara process nahi hone deta.
/// </summary>
public sealed class UserCreatedConsumer(AppDbContext db, ILogger<UserCreatedConsumer> logger) : IConsumer<UserCreatedEvent>
{
    public async Task Consume(ConsumeContext<UserCreatedEvent> context)
    {
        var message = context.Message;
        var email = message.Email.Trim();

        // Background consumer mein HTTP user/tenant nahi hota → filter bypass, tenant khud check
        var employee = await db.Employees
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.TenantId == message.TenantId && !e.IsDeleted && e.WorkEmail == email,
                                 context.CancellationToken);

        if (employee is null)
        {
            logger.LogInformation("No employee record for {Email} in tenant {TenantId}; HR can link later.", email, message.TenantId);
            return;
        }

        if (employee.UserId == message.UserId)
            return;   // already linked

        employee.LinkUser(message.UserId);
        await db.SaveChangesAsync(context.CancellationToken);

        logger.LogInformation("Employee {EmployeeCode} linked to user {UserId}.", employee.EmployeeCode, message.UserId);
    }
}
