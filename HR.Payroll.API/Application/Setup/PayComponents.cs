namespace HR.Payroll.API.Application.Setup;

using FluentValidation;
using HR.Payroll.API.Application.Common.Exceptions;
using HR.Payroll.API.Application.Common.Interfaces;
using HR.Payroll.API.Domain.Common;
using HR.Payroll.API.Domain.Setup;
using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record PayComponentDto(
    Guid Id, string Code, string Name, string? SystemCode, ComponentType ComponentType, CalcType DefaultCalcType,
    Guid? DefaultBaseComponentId, bool IsTaxable, bool IsProrated, bool IsRecurring, bool ShowOnPayslip,
    short SortOrder, bool IsActive);

public sealed record SavePayComponentRequest(
    string Code, string Name, ComponentType ComponentType, CalcType DefaultCalcType, Guid? DefaultBaseComponentId,
    bool IsTaxable, bool IsProrated, bool IsRecurring, bool ShowOnPayslip, short SortOrder);

public sealed record CreatePayComponentCommand(SavePayComponentRequest Data) : IRequest<Guid>;
public sealed record UpdatePayComponentCommand(Guid Id, SavePayComponentRequest Data) : IRequest;
public sealed record GetPayComponentsQuery(bool IncludeInactive = false) : IRequest<IReadOnlyList<PayComponentDto>>;

public sealed class SavePayComponentRequestValidator : AbstractValidator<SavePayComponentRequest>
{
    public SavePayComponentRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30).Matches("^[A-Za-z0-9_]+$")
            .WithMessage("Code can contain only letters, numbers and underscores.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ComponentType).IsInEnum();
        RuleFor(x => x.DefaultCalcType).IsInEnum();
    }
}

public sealed class CreatePayComponentValidator : AbstractValidator<CreatePayComponentCommand>
{
    public CreatePayComponentValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SavePayComponentRequestValidator());
}

public sealed class UpdatePayComponentValidator : AbstractValidator<UpdatePayComponentCommand>
{
    public UpdatePayComponentValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SavePayComponentRequestValidator());
}

public sealed class PayComponentHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<CreatePayComponentCommand, Guid>,
    IRequestHandler<UpdatePayComponentCommand>,
    IRequestHandler<GetPayComponentsQuery, IReadOnlyList<PayComponentDto>>
{
    public async Task<Guid> Handle(CreatePayComponentCommand request, CancellationToken ct)
    {
        var d = request.Data;
        await EnsureCodeIsFreeAsync(d.Code, null, ct);
        await EnsureBaseExistsAsync(d.DefaultBaseComponentId, ct);

        var component = PayComponent.Create(currentUser.RequireTenantId(), d.Code, d.Name, d.ComponentType, d.DefaultCalcType,
            d.DefaultBaseComponentId, d.IsTaxable, d.IsProrated, d.IsRecurring, d.ShowOnPayslip, d.SortOrder);

        db.PayComponents.Add(component);
        await db.SaveChangesAsync(ct);
        return component.Id;
    }

    public async Task Handle(UpdatePayComponentCommand request, CancellationToken ct)
    {
        var d = request.Data;
        var component = await db.PayComponents.FirstOrDefaultAsync(c => c.Id == request.Id, ct)
                        ?? throw new NotFoundException("Pay component", request.Id);

        await EnsureCodeIsFreeAsync(d.Code, component.Id, ct);
        await EnsureBaseExistsAsync(d.DefaultBaseComponentId, ct);

        component.Update(d.Code, d.Name, d.ComponentType, d.DefaultCalcType, d.DefaultBaseComponentId,
                         d.IsTaxable, d.IsProrated, d.IsRecurring, d.ShowOnPayslip, d.SortOrder);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PayComponentDto>> Handle(GetPayComponentsQuery request, CancellationToken ct)
        => await db.PayComponents.AsNoTracking()
            .Where(c => request.IncludeInactive || c.IsActive)
            .OrderBy(c => c.ComponentType).ThenBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new PayComponentDto(
                c.Id, c.Code, c.Name, c.SystemCode, c.ComponentType, c.DefaultCalcType, c.DefaultBaseComponentId,
                c.IsTaxable, c.IsProrated, c.IsRecurring, c.ShowOnPayslip, c.SortOrder, c.IsActive))
            .ToListAsync(ct);

    private async Task EnsureCodeIsFreeAsync(string code, Guid? excludeId, CancellationToken ct)
    {
        var normalized = code.Trim().ToUpperInvariant();
        if (await db.PayComponents.AnyAsync(c => c.Code == normalized && c.Id != excludeId, ct))
            throw new ConflictException($"Component code '{normalized}' is already in use.");
    }

    private async Task EnsureBaseExistsAsync(Guid? baseId, CancellationToken ct)
    {
        if (baseId is { } id && !await db.PayComponents.AnyAsync(c => c.Id == id && c.IsActive, ct))
            throw new NotFoundException("Base component", id);
    }
}
