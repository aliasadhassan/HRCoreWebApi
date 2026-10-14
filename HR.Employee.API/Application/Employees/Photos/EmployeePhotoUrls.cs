namespace HR.Employee.API.Application.Employees.Photos;

using HR.Employee.API.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

/// <summary>
/// PhotoStorageKey → short-lived signed URL. URL memory cache mein (expiry se 10 min pehle tak) taake
/// list har baar naya token na le aur browser image cache kar sake. Key har upload pe nayi hoti hai,
/// is liye purani photo ka URL kabhi nayi photo nahi dikhata.
/// </summary>
public sealed class EmployeePhotoUrls(IFileStorage storage, IMemoryCache cache, IOptions<StorageOptions> options, ILogger<EmployeePhotoUrls> logger)
{
    private const string CachePrefix = "photo-url:";

    public bool Enabled => storage.IsConfigured;

    public async Task<IReadOnlyDictionary<string, string>> ResolveAsync(IEnumerable<string?> keys, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, string>();
        if (!storage.IsConfigured) return result;

        var missing = new List<string>();
        foreach (var key in keys.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k!).Distinct())
        {
            if (cache.TryGetValue(CachePrefix + key, out string? url) && url is not null)
                result[key] = url;
            else
                missing.Add(key);
        }
        if (missing.Count == 0) return result;

        var lifetime = TimeSpan.FromMinutes(Math.Max(15, options.Value.SignedUrlMinutes));
        try
        {
            var signed = await storage.SignAsync(missing, lifetime, cancellationToken);
            foreach (var (key, url) in signed)
            {
                cache.Set(CachePrefix + key, url, lifetime - TimeSpan.FromMinutes(10));
                result[key] = url;
            }
        }
        catch (FileStorageException ex)
        {
            // Photo na mile to page initials dikhaye — list fail nahi honi chahiye
            logger.LogWarning(ex, "Could not sign {Count} employee photo URLs", missing.Count);
        }
        return result;
    }

    public async Task<string?> ResolveAsync(string? key, CancellationToken cancellationToken)
        => key is null ? null : (await ResolveAsync([key], cancellationToken)).GetValueOrDefault(key);

    public static string NewKey(Guid tenantId, Guid employeeId) => $"{tenantId:D}/{employeeId:D}/{Guid.NewGuid():N}.webp";
}
