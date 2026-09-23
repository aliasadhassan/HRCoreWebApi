namespace HR.Identity.API.Models;

public class TenantSettings
{
    public Guid TenantId { get; set; }
    public string TimeZone { get; set; } = "Asia/Karachi";
    public string Currency { get; set; } = "PKR";
    public string DateFormat { get; set; } = "dd-MMM-yyyy";
    public string Locale { get; set; } = "en-US";
    public byte FiscalYearStartMonth { get; set; } = 7;
    public byte WorkWeekDays { get; set; } = 31;          // bitmask Mon=1 .. Sun=64
    public byte PasswordMinLength { get; set; } = 8;
    public byte MaxFailedLoginAttempts { get; set; } = 5;
    public short SessionTimeoutMinutes { get; set; } = 60;
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public byte[] RowVersion { get; set; } = default!;

    public Tenant Tenant { get; set; } = default!;
}
