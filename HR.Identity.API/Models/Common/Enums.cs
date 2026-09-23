namespace HR.Identity.API.Models.Common;

public enum TenantStatus : byte { Active = 1, Suspended = 2, Cancelled = 3 }

public enum TokenPurpose : byte { PasswordReset = 1, EmailConfirm = 2, Invite = 3 }

public enum LoginMethod { Password, Microsoft, Refresh }
