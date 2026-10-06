namespace HR.Shared.Library.Helpers;

/// <summary>Key Vault secret names. Service DB secrets: RLS app role (hr_*_app); missing = shared SupabaseConnectionString.</summary>
public static class KeyVaultSecrets
{
    public const string SharedDb = "SupabaseConnectionString";
    public const string EmployeeDb = "EmployeeDbConnectionString";
    public const string PayrollDb = "PayrollDbConnectionString";
}
