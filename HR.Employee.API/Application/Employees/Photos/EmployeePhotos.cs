namespace HR.Employee.API.Application.Employees.Photos;

using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Common;
using HR.Employee.API.Domain.Employees;
using HR.Shared.Library.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record EmployeePhotoDto(string? PhotoUrl);

/// <summary>Topbar/sidebar/My settings: login wale user ki apni photo (Employee.UserId link).</summary>
public sealed record MyPhotoDto(bool Enabled, Guid? EmployeeId, string? PhotoUrl);

/// <summary>HR (employees.edit) kisi ki bhi photo badal sakta hai; employee sirf apni.</summary>
internal static class EmployeePhotoAccess
{
    public static async Task<Employee> GetEditableAsync(IAppDbContext db, ICurrentUser user, Guid employeeId, CancellationToken cancellationToken)
    {
        var employee = await EmployeeReferenceChecks.GetEmployeeAsync(db, employeeId, cancellationToken);
        var isSelf = user.UserId is { } uid && employee.UserId == uid;
        if (!isSelf && !user.HasPermission(Permissions.EmployeesEdit))
            throw new UnauthorizedAccessException("You can only change your own photo.");
        return employee;
    }
}

// ─────────────────────────────── Upload ───────────────────────────────
public sealed record UploadEmployeePhotoCommand(Guid EmployeeId, byte[] Content) : IRequest<EmployeePhotoDto>;

public sealed class UploadEmployeePhotoHandler(
    IAppDbContext db, ICurrentUser user, IFileStorage storage, EmployeePhotoUrls urls, ILogger<UploadEmployeePhotoHandler> logger)
    : IRequestHandler<UploadEmployeePhotoCommand, EmployeePhotoDto>
{
    public async Task<EmployeePhotoDto> Handle(UploadEmployeePhotoCommand request, CancellationToken cancellationToken)
    {
        if (!storage.IsConfigured)
            throw new FileStorageException("Photo storage is not set up yet.", notConfigured: true);

        var employee = await EmployeePhotoAccess.GetEditableAsync(db, user, request.EmployeeId, cancellationToken);
        if (employee.HasExited)
            throw new DomainException("This employee has exited. Their photo can no longer be changed.");

        var webp = PhotoImage.ToWebpSquare(request.Content);
        var key = EmployeePhotoUrls.NewKey(user.RequireTenantId(), employee.Id);
        await storage.UploadAsync(key, webp, PhotoImage.ContentType, cancellationToken);

        var oldKey = employee.PhotoStorageKey;
        employee.SetPhoto(key);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await PhotoCleanup.TryDeleteAsync(storage, logger, key);   // DB fail = nayi file orphan na rahe
            throw;
        }

        await PhotoCleanup.TryDeleteAsync(storage, logger, oldKey);
        return new EmployeePhotoDto(await urls.ResolveAsync(key, cancellationToken));
    }
}

// ─────────────────────────────── Remove ───────────────────────────────
public sealed record RemoveEmployeePhotoCommand(Guid EmployeeId) : IRequest;

public sealed class RemoveEmployeePhotoHandler(
    IAppDbContext db, ICurrentUser user, IFileStorage storage, ILogger<RemoveEmployeePhotoHandler> logger)
    : IRequestHandler<RemoveEmployeePhotoCommand>
{
    public async Task Handle(RemoveEmployeePhotoCommand request, CancellationToken cancellationToken)
    {
        var employee = await EmployeePhotoAccess.GetEditableAsync(db, user, request.EmployeeId, cancellationToken);
        var oldKey = employee.PhotoStorageKey;
        if (oldKey is null) return;

        employee.SetPhoto(null);
        await db.SaveChangesAsync(cancellationToken);
        await PhotoCleanup.TryDeleteAsync(storage, logger, oldKey);
    }
}

// ─────────────────────────────── Mine ───────────────────────────────
public sealed record GetMyPhotoQuery : IRequest<MyPhotoDto>;

public sealed class GetMyPhotoHandler(IAppDbContext db, ICurrentUser user, EmployeePhotoUrls urls) : IRequestHandler<GetMyPhotoQuery, MyPhotoDto>
{
    public async Task<MyPhotoDto> Handle(GetMyPhotoQuery request, CancellationToken cancellationToken)
    {
        var userId = user.UserId;
        var me = userId is null ? null : await db.Employees.AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderBy(e => e.EmploymentStatus == EmploymentStatus.Exited)
            .Select(e => new { e.Id, e.PhotoStorageKey })
            .FirstOrDefaultAsync(cancellationToken);

        return new MyPhotoDto(urls.Enabled, me?.Id, await urls.ResolveAsync(me?.PhotoStorageKey, cancellationToken));
    }
}

internal static class PhotoCleanup
{
    /// <summary>Purani file hatana best-effort hai — fail ho to sirf log (orphan file, koi data leak nahi).</summary>
    public static async Task TryDeleteAsync(IFileStorage storage, ILogger logger, string? key)
    {
        if (key is null) return;
        try
        {
            await storage.DeleteAsync([key], CancellationToken.None);
        }
        catch (Exception ex) when (ex is FileStorageException or HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Could not delete old employee photo {Key}", key);
        }
    }
}
