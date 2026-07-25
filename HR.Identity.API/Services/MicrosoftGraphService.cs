using System.Net.Http.Headers;
using System.Text.Json;

namespace HR.Identity.API.Services
{
    public class MicrosoftGraphUser
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class MicrosoftGraphService
    {
        private readonly HttpClient _httpClient;

        public MicrosoftGraphService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<MicrosoftGraphUser?> GetUserFromTokenAsync(string accessToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                return null; // Invalid ya expired token
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new MicrosoftGraphUser
            {
                Id = root.GetProperty("id").GetString() ?? string.Empty,
                DisplayName = root.GetProperty("displayName").GetString() ?? string.Empty,
                Email = root.TryGetProperty("mail", out var mail) && mail.ValueKind != JsonValueKind.Null
                    ? mail.GetString() ?? string.Empty
                    : root.GetProperty("userPrincipalName").GetString() ?? string.Empty
            };
        }
    }
}