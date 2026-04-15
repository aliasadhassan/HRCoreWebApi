namespace HR.Identity.API.Middleware
{
    public class JwtHeaderMiddleware
    {
        private readonly RequestDelegate _next;

        public JwtHeaderMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Un paths ki list jinko Authorization ki zaroorat nahi
            var allowedPaths = new[] {
                "/api/auth/login",
                "/api/auth/logout",
                "/api/auth/register",
                "/api/auth/refreshToken",
                "/api/auth/forgot-password"
            };

            // Request ke path ko normalize karein
            var requestPath = context.Request.Path.Value?.ToLowerInvariant();

            // Agar request ka path allowed routes mein se kisi se match karta hai
            if (requestPath != null && allowedPaths.Contains(requestPath))
            {
                await _next(context); // Authorization check skip karein
                return;
            }

            if (!context.Request.Headers.ContainsKey("Authorization"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Authorization header missing");
                return;
            }

            await _next(context);
        }
    }
}
