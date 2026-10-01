using HR.Identity.API.Data;
using HR.Identity.API.Models;
using HR.Identity.API.Models.Admin;
using HR.Shared.Library.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HR.Identity.API.Controllers;

/// <summary>
/// Company ki profile (Tenants) aur general settings (TenantSettings) — "Company settings" page.
/// Parhna: settings.view. Badalna: settings.manage.
/// Slug, plan aur SSO yahan se nahi badalte (billing / onboarding ka kaam).
/// </summary>
[ApiController]
[Route("api/company")]
public sealed class CompanyController(AppDbContext db) : ControllerBase
{
    private static readonly HashSet<string> DateFormats = ["dd-MMM-yyyy", "dd/MM/yyyy", "MM/dd/yyyy", "yyyy-MM-dd"];

    private Guid TenantId => User.GetTenantId() ?? throw new UnauthorizedAccessException("Tenant missing in token.");
    private Guid MeId => User.GetUserId() ?? throw new UnauthorizedAccessException("User missing in token.");

    [HttpGet]
    [HasPermission(Permissions.SettingsView)]
    public async Task<ActionResult<CompanyDto>> Get(CancellationToken ct)
    {
        var tenant = await LoadAsync(track: false, ct);
        if (tenant is null) return NotFound();
        return Ok(ToDto(tenant));
    }

    [HttpPut("profile")]
    [HasPermission(Permissions.SettingsManage)]
    public async Task<ActionResult<CompanyDto>> UpdateProfile([FromBody] UpdateCompanyProfileRequest request, CancellationToken ct)
    {
        var tenant = await LoadAsync(track: true, ct);
        if (tenant is null) return NotFound();

        tenant.Name = request.Name.Trim();
        tenant.LegalName = Clean(request.LegalName);
        tenant.LogoUrl = Clean(request.LogoUrl);
        tenant.PrimaryEmail = Clean(request.PrimaryEmail);
        tenant.Phone = Clean(request.Phone);
        tenant.UpdatedBy = MeId;

        await db.SaveChangesAsync(ct);
        return Ok(ToDto(tenant));
    }

    [HttpPut("settings")]
    [HasPermission(Permissions.SettingsManage)]
    public async Task<ActionResult<CompanyDto>> UpdateSettings([FromBody] UpdateCompanySettingsRequest request, CancellationToken ct)
    {
        if (!IsValidTimeZone(request.TimeZone))
            return Problem(statusCode: 400, detail: "Unknown time zone.");
        if (!DateFormats.Contains(request.DateFormat))
            return Problem(statusCode: 400, detail: "Unsupported date format.");
        if (!request.Currency.All(char.IsLetter))
            return Problem(statusCode: 400, detail: "Currency must be a 3-letter ISO code, like PKR or AED.");

        var tenant = await LoadAsync(track: true, ct);
        if (tenant is null) return NotFound();

        // Purani companies jinki settings row kabhi bani hi nahi
        var s = tenant.Settings ??= new TenantSettings { TenantId = tenant.Id };

        s.TimeZone = request.TimeZone;
        s.Currency = request.Currency.ToUpperInvariant();
        s.DateFormat = request.DateFormat;
        s.FiscalYearStartMonth = request.FiscalYearStartMonth;
        s.WorkWeekDays = request.WorkWeekDays;
        s.PasswordMinLength = request.PasswordMinLength;
        s.MaxFailedLoginAttempts = request.MaxFailedLoginAttempts;
        s.UpdatedAt = DateTime.UtcNow;
        s.UpdatedBy = MeId;

        await db.SaveChangesAsync(ct);
        return Ok(ToDto(tenant));
    }

    // ───────────── Helpers ─────────────
    private Task<Tenant?> LoadAsync(bool track, CancellationToken ct)
    {
        var tenantId = TenantId;
        var q = db.Tenants.Include(t => t.Settings).Where(t => t.Id == tenantId);
        return (track ? q : q.AsNoTracking()).FirstOrDefaultAsync(ct);
    }

    private static CompanyDto ToDto(Tenant t)
    {
        var s = t.Settings ?? new TenantSettings();
        return new CompanyDto(
            new CompanyProfileDto(t.Name, t.LegalName, t.Slug, t.LogoUrl, t.PrimaryEmail, t.Phone, t.Plan, t.SsoEnabled),
            new CompanySettingsDto(s.TimeZone, s.Currency, s.DateFormat, s.FiscalYearStartMonth, s.WorkWeekDays,
                                   s.PasswordMinLength, s.MaxFailedLoginAttempts),
            s.UpdatedAt ?? t.UpdatedAt);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>IANA naam ("Asia/Karachi") — .NET 8 Windows/Linux dono pe samajhta hai.</summary>
    private static bool IsValidTimeZone(string id)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return false;
        }
    }
}
