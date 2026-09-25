namespace HR.Employee.API.Application.Organization;

using FluentValidation;
using HR.Employee.API.Application.Common.Exceptions;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

// Simple CRUD — yahan aggregates/events ki zaroorat nahi.

public sealed record LocationDto(
    Guid Id, string Name, string Code, string CountryCode, string? City, string? AddressLine,
    string TimeZone, byte WorkWeekDays, bool IsHeadOffice, bool IsActive, int EmployeeCount);

public sealed record SaveLocationRequest(
    string Name, string Code, string CountryCode, string TimeZone,
    string? City, string? AddressLine, byte WorkWeekDays = Location.MondayToFriday, bool IsHeadOffice = false);

public sealed record CreateLocationCommand(SaveLocationRequest Data) : IRequest<Guid>;
public sealed record UpdateLocationCommand(Guid Id, SaveLocationRequest Data) : IRequest;
public sealed record GetLocationsQuery(bool IncludeInactive = false) : IRequest<IReadOnlyList<LocationDto>>;

public sealed class SaveLocationRequestValidator : AbstractValidator<SaveLocationRequest>
{
    public SaveLocationRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.CountryCode).NotEmpty().Length(2);
        RuleFor(x => x.TimeZone).NotEmpty().MaximumLength(64)
            .Must(tz => TimeZoneInfo.TryFindSystemTimeZoneById(tz, out _))
            .WithMessage("Unknown time zone. Use an IANA id like 'Asia/Karachi'.");
        RuleFor(x => x.City).MaximumLength(100);
        RuleFor(x => x.AddressLine).MaximumLength(300);
        RuleFor(x => x.WorkWeekDays).InclusiveBetween((byte)1, (byte)127);
    }
}

public sealed class CreateLocationValidator : AbstractValidator<CreateLocationCommand>
{
    public CreateLocationValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveLocationRequestValidator());
}

public sealed class UpdateLocationValidator : AbstractValidator<UpdateLocationCommand>
{
    public UpdateLocationValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveLocationRequestValidator());
}

public sealed class LocationHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<CreateLocationCommand, Guid>,
    IRequestHandler<UpdateLocationCommand>,
    IRequestHandler<GetLocationsQuery, IReadOnlyList<LocationDto>>
{
    public async Task<Guid> Handle(CreateLocationCommand request, CancellationToken ct)
    {
        var d = request.Data;
        await EnsureCodeIsFreeAsync(d.Code, null, ct);

        var location = Location.Create(currentUser.RequireTenantId(), d.Name, d.Code, d.CountryCode, d.TimeZone,
                                       d.City, d.AddressLine, d.WorkWeekDays, d.IsHeadOffice);
        if (d.IsHeadOffice)
            await ClearOtherHeadOfficesAsync(null, ct);

        db.Locations.Add(location);
        await db.SaveChangesAsync(ct);
        return location.Id;
    }

    public async Task Handle(UpdateLocationCommand request, CancellationToken ct)
    {
        var d = request.Data;
        var location = await db.Locations.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
                       ?? throw new NotFoundException("Location", request.Id);

        await EnsureCodeIsFreeAsync(d.Code, location.Id, ct);
        location.Update(d.Name, d.Code, d.CountryCode, d.TimeZone, d.City, d.AddressLine, d.WorkWeekDays, d.IsHeadOffice);
        if (d.IsHeadOffice)
            await ClearOtherHeadOfficesAsync(location.Id, ct);

        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<LocationDto>> Handle(GetLocationsQuery request, CancellationToken ct)
        => await db.Locations.AsNoTracking()
            .Where(x => request.IncludeInactive || x.IsActive)
            .OrderByDescending(x => x.IsHeadOffice).ThenBy(x => x.Name)
            .Select(x => new LocationDto(
                x.Id, x.Name, x.Code, x.CountryCode, x.City, x.AddressLine, x.TimeZone, x.WorkWeekDays,
                x.IsHeadOffice, x.IsActive,
                db.Employees.Count(e => e.LocationId == x.Id && e.EmploymentStatus != EmploymentStatus.Exited)))
            .ToListAsync(ct);

    private async Task EnsureCodeIsFreeAsync(string code, Guid? excludeId, CancellationToken ct)
    {
        var normalized = code.Trim().ToUpperInvariant();
        if (await db.Locations.AnyAsync(x => x.Code == normalized && x.Id != excludeId, ct))
            throw new ConflictException($"Location code '{normalized}' is already in use.");
    }

    /// <summary>Head office sirf ek.</summary>
    private async Task ClearOtherHeadOfficesAsync(Guid? keepId, CancellationToken ct)
    {
        var others = await db.Locations.Where(x => x.IsHeadOffice && x.Id != keepId).ToListAsync(ct);
        foreach (var other in others)
            other.SetHeadOffice(false);
    }
}
