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