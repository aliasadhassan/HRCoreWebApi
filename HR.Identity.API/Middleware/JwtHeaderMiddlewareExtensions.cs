namespace HR.Identity.API.Middleware
{
    public static class JwtHeaderMiddlewareExtensions
    {
        public static IApplicationBuilder UseJwtHeaderMiddleware(this IApplicationBuilder app)
        {
            return app.UseWhen(
                context =>
                {
                    // 🔍 TEMPORARY DEBUG LOG — isse pata chalega actual incoming path kya hai
                    Console.WriteLine($"[JwtMiddleware Check] Incoming Path: {context.Request.Path}");

                    return !AnonymousEndpoints.Any(path =>
                        context.Request.Path.StartsWithSegments(
                            path,
                            StringComparison.OrdinalIgnoreCase));
                },
                appBuilder =>
                {
                    appBuilder.UseMiddleware<JwtHeaderMiddleware>();
                });
        }

        private static readonly string[] AnonymousEndpoints =
        {
            "/api/auth/login",
            "/api/auth/register",
            "/api/auth/forgot-password",
            "/api/auth/reset-password",
            "/api/auth/sso/callback"
        };
    }
}