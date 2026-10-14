namespace HR.Employee.API.Application.Common.Interfaces;

/// <summary>
/// Private object storage (Supabase Storage). DB mein sirf path ("tenantId/employeeId/x.webp"),
/// file yahan. Browser ko kabhi permanent URL nahi — sirf short-lived signed URL.
/// </summary>
public interface IFileStorage
{
    /// <summary>Key Vault mein storage key na ho to false — photo upload band, URLs null.</summary>
    bool IsConfigured { get; }

    Task UploadAsync(string path, byte[] content, string contentType, CancellationToken cancellationToken);

    Task DeleteAsync(IReadOnlyCollection<string> paths, CancellationToken cancellationToken);

    /// <summary>path → signed URL. Jo path sign na ho sake woh result mein nahi hoga.</summary>
    Task<IReadOnlyDictionary<string, string>> SignAsync(IReadOnlyCollection<string> paths, TimeSpan expiresIn, CancellationToken cancellationToken);
}

/// <summary>Storage band ho (503) ya Supabase ne mana kar diya (502).</summary>
public sealed class FileStorageException(string message, bool notConfigured = false) : Exception(message)
{
    public bool NotConfigured { get; } = notConfigured;
}

public sealed class StorageOptions
{
    public const string Section = "Storage";

    /// <summary>https://{project-ref}.supabase.co</summary>
    public string SupabaseUrl { get; set; } = "";
    public string Bucket { get; set; } = "employee-photos";
    public int SignedUrlMinutes { get; set; } = 60;

    /// <summary>Key Vault "SupabaseStorageKey" (secret / service_role key) — appsettings mein kabhi nahi.</summary>
    public string? ServiceKey { get; set; }
}
