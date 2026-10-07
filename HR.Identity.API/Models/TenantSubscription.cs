using HR.Identity.API.Models.Common;

namespace HR.Identity.API.Models;

/// <summary>
/// Company ka license (audit M4). Har period ek row — renew pe nayi row (RenewedFromId), purani history rehti hai.
/// EndDate null = koi end nahi. GraceUntil tak login chalta hai, uske baad band (data kabhi delete nahi hota).
/// Concurrency = Postgres xmin (bytea RowVersion nahi).
/// </summary>
public class TenantSubscription
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string PlanCode { get; set; } = default!;
    public SubscriptionStatus Status { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateOnly? GraceUntil { get; set; }
    public int? SeatLimit { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = default!;
    public BillingCycle BillingCycle { get; set; }
    public bool AutoRenew { get; set; }
    public Guid? RenewedFromId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public uint RowVersion { get; private set; }

    public Tenant Tenant { get; set; } = default!;

    public static readonly SubscriptionStatus[] CurrentStatuses =
        [SubscriptionStatus.Trial, SubscriptionStatus.Active, SubscriptionStatus.PastDue];

    /// <summary>Aakhri din jab tak login allowed hai (grace ke saath). null = koi limit nahi.</summary>
    public DateOnly? AccessUntil => GraceUntil ?? EndDate;

    public bool AllowsAccessOn(DateOnly today) => AccessUntil is null || today <= AccessUntil;
}
