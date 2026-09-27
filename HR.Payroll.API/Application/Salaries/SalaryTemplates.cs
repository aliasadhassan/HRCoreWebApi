namespace HR.Payroll.API.Application.Salaries;

using FluentValidation;
using HR.Payroll.API.Application.Common.Exceptions;
using HR.Payroll.API.Application.Common.Interfaces;
using HR.Payroll.API.Domain.Common;
using HR.Payroll.API.Domain.Salaries;
using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record TemplateLineInput(Guid PayComponentId, CalcType CalcType, decimal? Amount, decimal? Percentage, Guid? BaseComponentId);

public sealed record SaveSalaryTemplateRequest(string Name, Guid? SalaryGradeId, IReadOnlyList<TemplateLineInput> Lines);

public sealed record SalaryTemplateListItemDto(Guid Id, string Name, Guid? SalaryGradeId, string? GradeCode, int LineCount, bool IsActive);

public sealed record SalaryTemplateLineDto(
    Guid PayComponentId, string ComponentCode, string ComponentName, ComponentType ComponentType,
    CalcType CalcType, decimal? Amount, decimal? Percentage, Guid? BaseComponentId);

public sealed record SalaryTemplateDto(Guid Id, string Name, Guid? SalaryGradeId, bool IsActive, IReadOnlyList<SalaryTemplateLineDto> Lines);

public sealed record CreateSalaryTemplateCommand(SaveSalaryTemplateRequest Data) : IRequest<Guid>;
public sealed record UpdateSalaryTemplateCommand(Guid Id, SaveSalaryTemplateRequest Data) : IRequest;
public sealed record GetSalaryTemplatesQuery(bool IncludeInactive = false) : IRequest<IReadOnlyList<SalaryTemplateListItemDto>>;
public sealed record GetSalaryTemplateByIdQuery(Guid Id) : IRequest<SalaryTemplateDto>;

public sealed class SaveSalaryTemplateRequestValidator : AbstractValidator<SaveSalaryTemplateRequest>
{
    public SaveSalaryTemplateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Add at least one component.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.PayComponentId).NotEmpty();
            line.RuleFor(l => l.CalcType).IsInEnum().NotEqual(CalcType.Variable)
                .WithMessage("Variable components come from payroll inputs, not templates.");
        });
    }
}

public sealed class CreateSalaryTemplateValidator : AbstractValidator<CreateSalaryTemplateCommand>
{
    public CreateSalaryTemplateValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveSalaryTemplateRequestValidator());
}

public sealed class UpdateSalaryTemplateValidator : AbstractValidator<UpdateSalaryTemplateCommand>
{
    public UpdateSalaryTemplateValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveSalaryTemplateRequestValidator());
}

public sealed class SalaryTemplateHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<CreateSalaryTemplateCommand, Guid>,
    IRequestHandler<UpdateSalaryTemplateCommand>,
    IRequestHandler<GetSalaryTemplatesQuery, IReadOnlyList<SalaryTemplateListItemDto>>,
    IRequestHandler<GetSalaryTemplateByIdQuery, SalaryTemplateDto>
{
    public async Task<Guid> Handle(CreateSalaryTemplateCommand request, CancellationToken ct)
    {
        var d = request.Data;
        await EnsureReferencesAsync(d, ct);

        var template = SalaryTemplate.Create(currentUser.RequireTenantId(), d.Name, d.SalaryGradeId);
        template.ReplaceLines(ToTuples(d.Lines));

        db.SalaryTemplates.Add(template);
        await db.SaveChangesAsync(ct);
        return template.Id;
    }

    public async Task Handle(UpdateSalaryTemplateCommand request, CancellationToken ct)
    {
        var d = request.Data;
        var template = await db.SalaryTemplates.Include(t => t.Lines).FirstOrDefaultAsync(t => t.Id == request.Id, ct)
                       ?? throw new NotFoundException("Salary template", request.Id);

        await EnsureReferencesAsync(d, ct);
        template.Update(d.Name, d.SalaryGradeId);
        template.ReplaceLines(ToTuples(d.Lines));

        // NOTE: template badalne se AGLE run se sab employees pe asar — pichhli payslips snapshot hain, nahi badlengi.
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SalaryTemplateListItemDto>> Handle(GetSalaryTemplatesQuery request, CancellationToken ct)
        => await db.SalaryTemplates.AsNoTracking()
            .Where(t => request.IncludeInactive || t.IsActive)
            .OrderBy(t => t.Name)
            .Select(t => new SalaryTemplateListItemDto(
                t.Id, t.Name, t.SalaryGradeId,
                db.SalaryGrades.Where(g => g.Id == t.SalaryGradeId).Select(g => g.Code).FirstOrDefault(),
                t.Lines.Count, t.IsActive))
            .ToListAsync(ct);

    public async Task<SalaryTemplateDto> Handle(GetSalaryTemplateByIdQuery request, CancellationToken ct)
    {
        var template = await db.SalaryTemplates.AsNoTracking().Include(t => t.Lines)
                           .FirstOrDefaultAsync(t => t.Id == request.Id, ct)
                       ?? throw new NotFoundException("Salary template", request.Id);

        var componentIds = template.Lines.Select(l => l.PayComponentId).ToList();
        var components = await db.PayComponents.AsNoTracking()
            .Where(c => componentIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, ct);

        var lines = template.Lines
            .Select(l => (Line: l, Component: components.GetValueOrDefault(l.PayComponentId)))
            .OrderBy(x => x.Component?.ComponentType).ThenBy(x => x.Component?.SortOrder)
            .Select(x => new SalaryTemplateLineDto(
                x.Line.PayComponentId, x.Component?.Code ?? "?", x.Component?.Name ?? "(deleted)",
                x.Component?.ComponentType ?? ComponentType.Informational,
                x.Line.CalcType, x.Line.Amount, x.Line.Percentage, x.Line.BaseComponentId))
            .ToList();

        return new SalaryTemplateDto(template.Id, template.Name, template.SalaryGradeId, template.IsActive, lines);
    }

    private async Task EnsureReferencesAsync(SaveSalaryTemplateRequest d, CancellationToken ct)
    {
        if (d.SalaryGradeId is { } gradeId && !await db.SalaryGrades.AnyAsync(g => g.Id == gradeId && g.IsActive, ct))
            throw new NotFoundException("Salary grade", gradeId);

        var ids = d.Lines.Select(l => l.PayComponentId).Distinct().ToList();
        var found = await db.PayComponents
            .Where(c => ids.Contains(c.Id) && c.IsActive)
            .Select(c => new { c.Id, c.DefaultCalcType, c.SystemCode })
            .ToListAsync(ct);

        var missing = ids.Except(found.Select(f => f.Id)).FirstOrDefault();
        if (missing != Guid.Empty)
            throw new NotFoundException("Pay component", missing);

        if (found.Any(f => f.SystemCode == Domain.Setup.SystemComponentCodes.IncomeTax))
            throw new ConflictException("Income tax is calculated by the tax engine and cannot be part of a template.");
    }

    private static IEnumerable<(Guid, CalcType, decimal?, decimal?, Guid?)> ToTuples(IEnumerable<TemplateLineInput> lines)
        => lines.Select(l => (l.PayComponentId, l.CalcType, l.Amount, l.Percentage, l.BaseComponentId));
}
