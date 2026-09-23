using HR.Identity.API.Models;
using Microsoft.EntityFrameworkCore;

namespace HR.Identity.API.Data.Seed;

public static class IdentitySeeder
{
    static readonly (string Code, string Module, string Description)[] PermissionCatalog =
    [
        ("dashboard.view",   "Dashboard", "View dashboard"),
        ("employees.view",   "Employees", "View employee directory"),
        ("employees.create", "Employees", "Add employees"),
        ("employees.edit",   "Employees", "Edit employee records"),
        ("employees.delete", "Employees", "Archive/delete employees"),
        ("leaves.view.own",  "Leaves",    "View own leaves"),
        ("leaves.apply",     "Leaves",    "Apply for leave"),
        ("leaves.view.all",  "Leaves",    "View all leave requests"),
        ("leaves.approve",   "Leaves",    "Approve/reject leave"),
        ("payroll.view.own", "Payroll",   "View own payslips"),
        ("payroll.view.all", "Payroll",   "View all payroll"),
        ("payroll.run",      "Payroll",   "Process payroll runs"),
        ("payroll.approve",  "Payroll",   "Approve/lock payroll runs"),
        ("settings.view",    "Settings",  "View settings"),
        ("settings.manage",  "Settings",  "Change company settings"),
        ("users.manage",     "Settings",  "Invite/deactivate users"),
        ("roles.manage",     "Settings",  "Create roles, assign permissions"),
    ];

    static readonly string[] AdminOnly = ["settings.manage", "users.manage", "roles.manage"];

    static readonly (string Name, string Description, Func<string, bool> Includes)[] SystemRoles =
    [
        ("Tenant Admin", "Full access within the company",        _ => true),
        ("HR Manager",   "Employees, leaves and payroll",         c => !AdminOnly.Contains(c)),
        ("Line Manager", "Team view + leave approvals",           c => c is "dashboard.view" or "employees.view"
                                                                     or "leaves.view.own" or "leaves.apply"
                                                                     or "leaves.view.all" or "leaves.approve"
                                                                     or "payroll.view.own"),
        ("Employee",     "Self-service: own leaves and payslips", c => c is "dashboard.view" or "leaves.view.own"
                                                                     or "leaves.apply" or "payroll.view.own"),
    ];

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        await db.Database.MigrateAsync();

        // 1) Permissions — sirf missing codes add hongay (re-run safe)
        var existingCodes = await db.Permissions.Select(p => p.Code).ToListAsync();
        db.Permissions.AddRange(PermissionCatalog
            .Select((p, i) => new Permission
            {
                Code = p.Code, Module = p.Module, Description = p.Description, SortOrder = (short)((i + 1) * 10)
            })
            .Where(p => !existingCodes.Contains(p.Code)));
        await db.SaveChangesAsync();

        var allPermissions = await db.Permissions.ToListAsync();

        // 2) System roles (TenantId = null)
        foreach (var (name, description, includes) in SystemRoles)
        {
            var normalized = name.ToUpperInvariant();
            if (await db.Roles.AnyAsync(r => r.TenantId == null && r.NormalizedName == normalized))
                continue;

            db.Roles.Add(new Role
            {
                Name = name,
                NormalizedName = normalized,
                Description = description,
                IsSystem = true,
                RolePermissions = allPermissions
                    .Where(p => includes(p.Code))
                    .Select(p => new RolePermission { Permission = p })
                    .ToList()
            });
        }
        await db.SaveChangesAsync();

        // 3) Default tenant + admin user (credentials config/user-secrets se)
        var email = config["Seed:AdminEmail"];
        var password = config["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        var normalizedEmail = email.ToUpperInvariant();
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.NormalizedEmail == normalizedEmail))
            return;

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Slug == "hr-cloud")
                     ?? new Tenant { Name = "HR Cloud", Slug = "hr-cloud", Settings = new TenantSettings() };

        var adminRole = await db.Roles.FirstAsync(r => r.TenantId == null && r.NormalizedName == "TENANT ADMIN");

        var admin = new User
        {
            Tenant = tenant,
            Email = email,
            NormalizedEmail = normalizedEmail,
            DisplayName = "Admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11),
            EmailConfirmed = true
        };
        admin.UserRoles.Add(new UserRole { Role = adminRole });

        db.Users.Add(admin);   // naya tenant bhi isi graph ke saath insert ho jayega
        await db.SaveChangesAsync();
    }
}
