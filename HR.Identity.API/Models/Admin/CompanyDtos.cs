using System.ComponentModel.DataAnnotations;
using HR.Identity.API.Models.Common;

namespace HR.Identity.API.Models.Admin;

public sealed record CompanyProfileDto(
    string Name,
    string? LegalName,
    string Slug,
    string? LogoUrl,
    string? PrimaryEmail,
    string? Phone,
    string Plan,
    bool SsoEnabled);

public sealed record CompanySettingsDto(
    string TimeZone,
    string Currency,
    string DateFormat,
    byte FiscalYearStartMonth,
    byte WorkWeekDays,
    byte PasswordMinLength,
    byte MaxFailedLoginAttempts);

public sealed record SubscriptionDto(
    string PlanCode,
    SubscriptionStatus Status,
    DateOnly StartDate,
    DateOnly? EndDate,
    DateOnly? GraceUntil,
    int? DaysLeft,
    bool InGrace,
    int? SeatLimit,
    int SeatsUsed,
    BillingCycle BillingCycle,
    decimal Amount,
    string CurrencyCode,
    bool AutoRenew);

public sealed record CompanyDto(CompanyProfileDto Profile, CompanySettingsDto Settings, DateTime? UpdatedAt);

public sealed class UpdateCompanyProfileRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? LegalName { get; set; }

    [MaxLength(500), Url]
    public string? LogoUrl { get; set; }

    [MaxLength(256), EmailAddress]
    public string? PrimaryEmail { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }
}

public sealed class UpdateCompanySettingsRequest
{
    [Required, MaxLength(64)]
    public string TimeZone { get; set; } = "Asia/Karachi";

    [Required, StringLength(3, MinimumLength = 3)]
    public string Currency { get; set; } = "PKR";

    [Required, MaxLength(20)]
    public string DateFormat { get; set; } = "dd-MMM-yyyy";

    [Range(1, 12)]
    public byte FiscalYearStartMonth { get; set; } = 7;

    /// <summary>Bitmask: Mon=1, Tue=2, Wed=4, Thu=8, Fri=16, Sat=32, Sun=64</summary>
    [Range(1, 127)]
    public byte WorkWeekDays { get; set; } = 31;

    [Range(6, 64)]
    public byte PasswordMinLength { get; set; } = 8;

    [Range(3, 20)]
    public byte MaxFailedLoginAttempts { get; set; } = 5;
}
