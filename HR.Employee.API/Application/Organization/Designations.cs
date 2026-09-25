namespace HR.Employee.API.Application.Organization;

using FluentValidation;
using HR.Employee.API.Application.Common.Exceptions;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record DesignationDto(Guid Id, string Title, byte? Level, string? Description, bool IsActive, int EmployeeCount);

public sealed record SaveDesignationRequest(string Title, byte? Level, string? Description);

public sealed record CreateDesignationCommand(SaveDesignationRequest Data) : IRequest<Guid>;
public sealed record UpdateDesignationCommand(Guid Id, SaveDesignationRequest Data) : IRequest;
public sealed record GetDesignationsQuery(bool IncludeInactive = false) : IRequest<IReadOnlyList<DesignationDto>>;

public sealed class SaveDesignationRequestValidator : AbstractValidator<SaveDesignationRequest>
{
    public SaveDesignationRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Level).InclusiveBetween((byte)1, (byte)20).When(x => x.Level.HasValue);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public sealed class CreateDesignationValidator : AbstractValidator<CreateDesignationCommand>
{
    public CreateDesignationValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveDesignationRequestValidator());
}

public sealed class UpdateDesignationValidator : AbstractValidator<UpdateDesignationCommand>
{
    public UpdateDesignationValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveDesignationRequestValidator());
}

public sealed class DesignationHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<CreateDesignationCommand, Guid>,
    IRequestHandler<UpdateDesignationCommand>,
    IRequestHandler<GetDesignationsQuery, IReadOnlyList<DesignationDto>>
{
    public async Task<Guid> Handle(CreateDesignationCommand request, CancellationToken ct)
    {
        var d = request.Data;
        await EnsureTitleIsFreeAsync(d.Title, null, ct);

        var designation = Designation.Create(currentUser.RequireTenantId(), d.Title, d.Level, d.Description);
        db.Designations.Add(designation);
        await db.SaveChangesAsync(ct);
        return designation.Id;
    }

    public async Task Handle(UpdateDesignationCommand request, CancellationToken ct)
    {
        var d = request.Data;
        var designation = await db.Designations.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
                          ?? throw new NotFoundException("Designation", request.Id);

        await EnsureTitleIsFreeAsync(d.Title, designation.Id, ct);
        designation.Update(d.Title, d.Level, d.Description);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<DesignationDto>> Handle(GetDesignationsQuery request, CancellationToken ct)
        => await db.Designations.AsNoTracking()
            .Where(x => request.IncludeInactive || x.IsActive)
            .OrderBy(x => x.Level).ThenBy(x => x.Title)
            .Select(x => new DesignationDto(
                x.Id, x.Title, x.Level, x.Description, x.IsActive,
                db.Employees.Count(e => e.DesignationId == x.Id && e.EmploymentStatus != EmploymentStatus.Exited)))
            .ToListAsync(ct);

    private async Task EnsureTitleIsFreeAsync(string title, Guid? excludeId, CancellationToken ct)
    {
        var normalized = title.Trim();
        if (await db.Designations.AnyAsync(x => x.Title == normalized && x.Id != excludeId, ct))
            throw new ConflictException($"Designation '{normalized}' already exists.");
    }
}
