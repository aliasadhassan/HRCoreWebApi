namespace HR.Employee.API.Infrastructure.Storage;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using HR.Employee.API.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

/// <summary>Supabase Storage REST API (/storage/v1). Bucket private hai; RLS bypass sirf is server key se.</summary>
public sealed class SupabaseStorage(HttpClient http, IOptions<StorageOptions> options, ILogger<SupabaseStorage> logger) : IFileStorage
{
    private readonly StorageOptions _o = options.Value;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_o.ServiceKey) && !string.IsNullOrWhiteSpace(_o.SupabaseUrl);

    private string Base => _o.SupabaseUrl.TrimEnd('/') + "/storage/v1";

    public async Task UploadAsync(string path, byte[] content, string contentType, CancellationToken cancellationToken)
    {
        using var body = new ByteArrayContent(content);
        body.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        using var request = Request(HttpMethod.Post, $"/object/{_o.Bucket}/{EscapePath(path)}", body);
        request.Headers.Add("x-upsert", "false");
        request.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromDays(7) };   // path har upload pe naya

        using var response = await http.SendAsync(request, cancellationToken);
        await EnsureOkAsync(response, "upload", cancellationToken);
    }

    public async Task DeleteAsync(IReadOnlyCollection<string> paths, CancellationToken cancellationToken)
    {
        if (paths.Count == 0) return;
        using var request = Request(HttpMethod.Delete, $"/object/{_o.Bucket}", JsonContent.Create(new { prefixes = paths }));
        using var response = await http.SendAsync(request, cancellationToken);
        await EnsureOkAsync(response, "delete", cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, string>> SignAsync(
        IReadOnlyCollection<string> paths, TimeSpan expiresIn, CancellationToken cancellationToken)
    {
        if (paths.Count == 0) return new Dictionary<string, string>();

        var payload = new { expiresIn = (int)expiresIn.TotalSeconds, paths };
        using var request = Request(HttpMethod.Post, $"/object/sign/{_o.Bucket}", JsonContent.Create(payload));
        using var response = await http.SendAsync(request, cancellationToken);
        await EnsureOkAsync(response, "sign", cancellationToken);

        var rows = await response.Content.ReadFromJsonAsync<List<SignedRow>>(cancellationToken) ?? [];
        return rows
            .Where(r => r.Error is null && !string.IsNullOrEmpty(r.Path) && !string.IsNullOrEmpty(r.SignedUrl))
            .GroupBy(r => r.Path!)
            .ToDictionary(g => g.Key, g => Base + g.First().SignedUrl);
    }

    private HttpRequestMessage Request(HttpMethod method, string relative, HttpContent? content)
    {
        if (!IsConfigured)
            throw new FileStorageException("Photo storage is not set up yet.", notConfigured: true);

        var request = new HttpRequestMessage(method, Base + relative) { Content = content };
        // Legacy service_role JWT aur naye sb_secret_ dono: apikey + same Bearer
        request.Headers.Add("apikey", _o.ServiceKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _o.ServiceKey);
        return request;
    }

    private async Task EnsureOkAsync(HttpResponseMessage response, string action, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("Supabase storage {Action} failed: {Status} {Body}", action, (int)response.StatusCode, text);
        throw new FileStorageException("Photo storage did not accept the request. Try again.");
    }

    private static string EscapePath(string path) => string.Join('/', path.Split('/').Select(Uri.EscapeDataString));

    private sealed record SignedRow(
        [property: JsonPropertyName("path")] string? Path,
        [property: JsonPropertyName("signedURL")] string? SignedUrl,
        [property: JsonPropertyName("error")] string? Error);
}
