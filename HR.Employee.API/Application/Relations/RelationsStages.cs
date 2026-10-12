namespace HR.Employee.API.Application.Relations;

using FluentValidation;
using HR.Employee.API.Application.Common.Exceptions;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Common;
using HR.Employee.API.Domain.Relations;
using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record SaveStageRequest(string Name, string? Description, CasePhase Phase, short SortOrder, short? TargetDays, bool IsActive);
public sealed record CreateStageCommand(SaveStageRequest Data) : IRequest<Guid>;
public sealed record UpdateStageCommand(Guid Id, SaveStageRequest Data) : IRequest;
public sealed record DeleteStageCommand(Guid Id) : IRequest;
/// <summary>Default stages jo company ke paas na hon. Wapas = kitne bane.</summary>
public sealed record AddDefaultStagesCommand : IRequest<int>;

public sealed class SaveStageRequestValidator : AbstractValidator<SaveStageRequest>
{
    public SaveStageRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Description).MaximumLength(300);
        RuleFor(x => x.Phase).IsInEnum();
        RuleFor(x => x.SortOrder).InclusiveBetween((short)0, (short)999);
        RuleFor(x => x.TargetDays).InclusiveBetween((short)1, (short)CaseStage.MaxTargetDays).When(x => x.TargetDays is not null);
    }
}

public sealed class CreateStageValidator : AbstractValidator<CreateStageCommand>
{
    public CreateStageValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveStageRequestValidator());
}

public sealed class UpdateStageValidator : AbstractValidator<UpdateStageCommand>
{
    public UpdateStageValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveStageRequestValidator());
}

public sealed class RelationsStageCommandHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<CreateStageCommand, Guid>,
    IRequestHandler<UpdateStageCommand>,
    IRequestHandler<DeleteStageCommand>,
    IRequestHandler<AddDefaultStagesCommand, int>
{
    private readonly RelationsAccess access = new(db, currentUser);

    public async Task<Guid> Handle(CreateStageCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        await EnsureUniqueAsync(c.Data.Name, null, ct);
        var stage = CaseStage.Create(access.TenantId, Details(c.Data));
        db.CaseStages.Add(stage);
        await SaveAsync(ct);
        return stage.Id;
    }

    public async Task Handle(UpdateStageCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var stage = await db.CaseStages.FirstOrDefaultAsync(s => s.Id == c.Id, ct) ?? throw new NotFoundException("Stage", c.Id);
        await EnsureUniqueAsync(c.Data.Name, stage.Id, ct);
        if (c.Data.Phase != stage.Phase && await db.ErCases.AnyAsync(x => x.StageId == stage.Id, ct))
            throw new DomainException("Cases use this stage, so its phase cannot change. Add a new stage instead.");
        if (!c.Data.IsActive && stage.IsActive && await db.ErCases.AnyAsync(x => x.StageId == stage.Id && x.Phase != CasePhase.Closed, ct))
            throw new DomainException("Open cases are in this stage. Move them first, then switch it off.");
        await EnsureStillUsableAsync(stage.Id, c.Data.IsActive ? c.Data.Phase : null, ct);
        stage.Update(Details(c.Data));
        await SaveAsync(ct);
    }

    public async Task Handle(DeleteStageCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var stage = await db.CaseStages.FirstOrDefaultAsync(s => s.Id == c.Id, ct) ?? throw new NotFoundException("Stage", c.Id);
        if (await db.ErCases.IgnoreQueryFilters().AnyAsync(x => x.StageId == stage.Id && x.TenantId == access.TenantId, ct))
            throw new DomainException("Cases have used this stage, so it cannot be deleted. Switch it off instead.");
        await EnsureStillUsableAsync(stage.Id, null, ct);
        db.CaseStages.Remove(stage);
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> Handle(AddDefaultStagesCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        return await RelationsStageDefaults.EnsureAsync(db, access.TenantId, ct, addMissing: true);
    }

    /// <summary>Hamesha kam az kam ek khula aur ek band stage chalu rahe, warna naye case / band karna atak jayega.</summary>
    private async Task EnsureStillUsableAsync(Guid changedId, CasePhase? newPhase, CancellationToken ct)
    {
        var others = await db.CaseStages.Where(s => s.Id != changedId && s.IsActive).Select(s => s.Phase).ToListAsync(ct);
        if (newPhase is { } p)
            others.Add(p);
        if (!others.Any(p => p != CasePhase.Closed))
            throw new DomainException("Keep at least one active open stage.");
        if (!others.Contains(CasePhase.Closed))
            throw new DomainException("Keep at least one active closed stage.");
    }

    private async Task EnsureUniqueAsync(string name, Guid? exceptId, CancellationToken ct)
    {
        var n = name.Trim();
        if (await db.CaseStages.AnyAsync(s => s.Id != exceptId && EF.Functions.ILike(s.Name, n), ct))
            throw new ConflictException("A stage with this name already exists.");
    }

    private static StageDetails Details(SaveStageRequest d) => new(d.Name, d.Description, d.Phase, d.SortOrder, d.TargetDays, d.IsActive);

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UX_ErCaseStages_TenantId_Name") == true)
        {
            throw new ConflictException("A stage with this name already exists.");
        }
    }
}
