namespace HR.Employee.API.Infrastructure.Identity;

using HR.Employee.API.Application.Ai;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

/// <summary>
/// AI tools user ke apne token se chalte hain. Employee API ko apne aap ko call karna hai — bahar ka pata (gateway / host header)
/// har jagah nahi milta, is liye isi process ka sunne wala port (localhost) istemal hota hai.
/// </summary>
public sealed class AiCallContextProvider(IHttpContextAccessor accessor, IServer server) : IAiCallContextProvider
{
    public AiCallContext Get()
    {
        var header = accessor.HttpContext?.Request.Headers.Authorization.ToString() ?? "";
        var token = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header["Bearer ".Length..].Trim() : "";
        if (token.Length == 0)
            throw new UnauthorizedAccessException("Sign in again.");
        return new AiCallContext(token, SelfUrl());
    }

    private string SelfUrl()
    {
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses ?? [];
        // http pehle (andar ki call par TLS ki zaroorat nahi), warna https
        var address = addresses.FirstOrDefault(a => a.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                      ?? addresses.FirstOrDefault()
                      ?? "http://localhost:8080";
        var uri = new Uri(address.Replace("://+", "://localhost").Replace("://*", "://localhost")
            .Replace("://[::]", "://localhost").Replace("://0.0.0.0", "://localhost"));
        return $"{uri.Scheme}://localhost:{uri.Port}";
    }
}
