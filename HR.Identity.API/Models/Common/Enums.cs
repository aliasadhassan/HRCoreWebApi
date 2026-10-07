namespace HR.Identity.API.Models.Common;

public enum TenantStatus : byte { Active = 1, Suspended = 2, Cancelled = 3 }

public enum TokenPurpose : byte { PasswordReset = 1, EmailConfirm = 2, Invite = 3 }

public enum LoginMethod { Password, Microsoft, Refresh }

/// <summary>Trial/Active/PastDue = "current" (har tenant ki sirf ek). Expired/Cancelled = history.</summary>
public enum SubscriptionStatus : short { Trial = 1, Active = 2, PastDue = 3, Expired = 4, Cancelled = 5 }

public enum BillingCycle : short { None = 0, Monthly = 1, Yearly = 2 }
