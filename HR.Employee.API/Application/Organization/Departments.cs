namespace HR.Employee.API.Application.Organization;

using FluentValidation;
using HR.Employee.API.Application.Common.Exceptions;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record DepartmentDto(
    Guid Id, string Name, string Code, string? Description,
    Guid? ParentDepartmentId, string? ParentName,
    Guid? HeadEmployeeId, string? HeadName,
    bool IsActive, int EmployeeCount);

public sealed record SaveDepartmentRequest(
    string Name, string Code, string? Description, Guid? ParentDepartmentId, Guid? HeadEmployeeId);

public sealed record CreateDepartmentCommand(SaveDepartmentRequest Data) : IRequest<Guid>;
public sealed record UpdateDepartmentCommand(Guid Id, SaveDepartmentRequest Data) : IRequest;
public sealed record GetDepartmentsQuery(bool IncludeInactive = false) : IRequest<IReadOnlyList<DepartmentDto>>;

public sealed class SaveDepartmentRequestValidator : AbstractValidator<SaveDepartmentRequest>
{
    public SaveDepartmentRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public sealed class CreateDepartmentValidator : AbstractValidator<CreateDepartmentCommand>
{
    public CreateDepartmentValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveDepartmentRequestValidator());
}

public sealed class UpdateDepartmentValidator : AbstractValidator<UpdateDepartmentCommand>
{
    public UpdateDepartmentValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveDepartmentRequestValidator());
}

public sealed class DepartmentHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<CreateDepartmentCommand, Guid>,
    IRequestHandler<UpdateDepartmentCommand>,
    IRequestHandler<GetDepartmentsQuery, IReadOnlyList<DepartmentDto>>
{
    public async Task<Guid> Handle(CreateDepartmentCommand request, CancellationToken ct)
    {
        var d = request.Data;
        await EnsureCodeIsFreeAsync(d.Code, null, ct);
        await EnsureParentAsync(d.ParentDepartmentId, null, ct);
        await EnsureHeadAsync(d.HeadEmployeeId, ct);

        var department = Department.Create(currentUser.RequireTenantId(), d.Name, d.Code, d.Description, d.ParentDepartmentId);
        department.AssignHead(d.HeadEmployeeId);

        db.Departments.Add(department);
        await db.SaveChangesAsync(ct);
        return department.Id;
    }

    public async Task Handle(UpdateDepartmentCommand request, CancellationToken ct)
    {
        var d = request.Data;
        var department = await db.Departments.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
                         ?? throw new NotFoundException("Department", request.Id);

        await EnsureCodeIsFreeAsync(d.Code, department.Id, ct);
        await EnsureParentAsync(d.ParentDepartmentId, department.Id, ct);
        await EnsureHeadAsync(d.HeadEmployeeId, ct);

        department.Update(d.Name, d.Code, d.Description, d.ParentDepartmentId);
        department.AssignHead(d.HeadEmployeeId);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<DepartmentDto>> Handle(GetDepartmentsQuery request, CancellationToken ct)
        => await db.Departments.AsNoTracking()
            .Where(x => request.IncludeInactive || x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new DepartmentDto(
                x.Id, x.Name, x.Code, x.Description,
                x.ParentDepartmentId,
                db.Departments.Where(p => p.Id == x.ParentDepartmentId).Select(p => p.Name).FirstOrDefault(),
                x.HeadEmployeeId,
                db.Employees.Where(e => e.Id == x.HeadEmployeeId).Select(e => e.FirstName + " " + e.LastName).FirstOrDefault(),
                x.IsActive,
                db.Employees.Count(e => e.DepartmentId == x.Id && e.EmploymentStatus != EmploymentStatus.Exited)))
            .ToListAsync(ct);

    private async Task EnsureCodeIsFreeAsync(string code, Guid? excludeId, CancellationToken ct)
    {
        var normalized = code.Trim().ToUpperInvariant();
        if (await db.Departments.AnyAsync(x => x.Code == normalized && x.Id != excludeId, ct))
            throw new ConflictException($"Department code '{normalized}' is already in use.");
    }

    /// <summary>Parent exist kare aur hierarchy mein loop na bane (A → B → A).</summary>
    private async Task EnsureParentAsync(Guid? parentId, Guid? departmentId, CancellationToken ct)
    {
        if (parentId is null)
            return;
        if (!await db.Departments.AnyAsync(x => x.Id == parentId, ct))
            throw new NotFoundException("Parent department", parentId);
        if (departmentId is null)
            return;

        var current = parentId;
        for (var depth = 0; current is not null && depth < 50; depth++)
        {
            if (current == departmentId)
                throw new ConflictException("This parent would create a loop in the department hierarchy.");

            var id = current.Value;
            current = await db.Departments.Where(x => x.Id == id).Select(x => x.ParentDepartmentId).FirstOrDefaultAsync(ct);
        }
    }

    private async Task EnsureHeadAsync(Guid? headEmployeeId, CancellationToken ct)
    {
        if (headEmployeeId is { } id &&
            !await db.Employees.AnyAsync(e => e.Id == id && e.EmploymentStatus != EmploymentStatus.Exited, ct))
            throw new NotFoundException("Head employee", id);
    }
}
