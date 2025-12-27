using Microsoft.AspNetCore.Http;
using Xunit;
using LearningCoreWebApi.Middleware;

namespace LearningCoreWebApi.Tests.Middleware
{
    public class JwtHeaderMiddlewareTests
    {
        [Fact]
        public async Task Should_Return_401_When_Authorization_Header_Is_Missing()
        {
            // Arrange
            var context = new DefaultHttpContext();

            bool wasCalled = false;

            RequestDelegate next = (ctx) =>
            {
                wasCalled = true;
                return Task.CompletedTask;
            };

            var middleware = new JwtHeaderMiddleware(next);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
            Assert.False(wasCalled);
        }
    }
}
