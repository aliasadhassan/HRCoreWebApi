namespace LearningCoreWebApi.Middleware
{
    public static class JwtHeaderMiddlewareExtensions
    {
        public static IApplicationBuilder UseJwtHeaderMiddleware(this IApplicationBuilder app)
        {
            return app.UseWhen(
                context =>
                    !AnonymousEndpoints.Any(path =>
                        context.Request.Path.StartsWithSegments(
                            path,
                            StringComparison.OrdinalIgnoreCase)),
                appBuilder =>
                {
                    appBuilder.UseMiddleware<JwtHeaderMiddleware>();
                });
        }
        private static readonly string[] AnonymousEndpoints =
        {
            "/api/values/login",
            "/api/values/register",
            "/health",
            "/swagger"
        };
    }
}
