namespace HR.Employee.API.Infrastructure.Logging;

using FluentValidation;
using HR.Employee.API.Application.Common.Exceptions;
using HR.Employee.API.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

/// <summary>Ek hi jagah saare exceptions → ProblemDetails. (GlobalExceptionFilter hata diya — woh isay chalne hi nahi deta tha.)</summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is ValidationException validation)
        {
            var errors = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray());

            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsJsonAsync(new ValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation Error",
                Instance = httpContext.Request.Path
            }, cancellationToken);
            return true;
        }

        var (status, title, detail) = exception switch
        {
            DomainException => (StatusCodes.Status422UnprocessableEntity, "Business rule violation", exception.Message),
            NotFoundException => (StatusCodes.Status404NotFound, "Not found", exception.Message),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict", exception.Message),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Concurrency conflict",
                "This record was changed by someone else. Reload and try again."),
            DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } => (StatusCodes.Status409Conflict,
                "Duplicate", "A record with the same unique value already exists."),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Forbidden", exception.Message),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error",
                environment.IsDevelopment() ? exception.Message : "An unexpected error occurred.")
        };

        if (status >= 500)
            logger.LogError(exception, "Unhandled exception on {Path}", httpContext.Request.Path);
        else
            logger.LogWarning("{Title} on {Path}: {Message}", title, httpContext.Request.Path, exception.Message);

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        }, cancellationToken);
        return true;
    }
}
