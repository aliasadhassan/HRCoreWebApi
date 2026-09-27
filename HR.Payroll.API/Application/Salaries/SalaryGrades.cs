namespace HR.Payroll.API.Application.Salaries;

using FluentValidation;
using HR.Payroll.API.Application.Common.Exceptions;
using HR.Payroll.API.Application.Common.Interfaces;
using HR.Payroll.API.Domain.Salaries;
using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record SalaryGradeDto(Guid Id, string Code, string Name, string CurrencyCode, decimal? MinAnnual, decimal? MaxAnnual, bool IsActive);

public sealed record SaveSalaryGradeRequest(string Code, string Name, string CurrencyCode, decimal? MinAnnual, decimal? MaxAnnual);

public sealed record CreateSalaryGradeCommand(SaveSalaryGradeRequest Data) : IRequest<Guid>;
public sealed record UpdateSalaryGradeCommand(Guid Id, SaveSalaryGradeRequest Data) : IRequest;
public sealed record GetSalaryGradesQuery(bool IncludeInactive = false) : IRequest<IReadOnlyList<SalaryGradeDto>>;

public sealed class SaveSalaryGradeRequestValidator : AbstractValidator<SaveSalaryGradeRequest>
{
    public SaveSalaryGradeRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CurrencyCode).NotEmpty().Length(3);
        RuleFor(x => x.MinAnnual).GreaterThanOrEqualTo(0).When(x => x.MinAnnual.HasValue);
        RuleFor(x => x.MaxAnnual).GreaterThanOrEqualTo(x => x.MinAnnual ?? 0).When(x => x.MaxAnnual.HasValue);
    }
}

public sealed class CreateSalaryGradeValidator : AbstractValidator<CreateSalaryGradeCommand>
{
    public CreateSalaryGradeValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveSalaryGradeRequestValidator());
}

public sealed class UpdateSalaryGradeValidator : AbstractValidator<UpdateSalaryGradeCommand>
{
    public UpdateSalaryGradeValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveSalaryGradeRequestValidator());
}

public sealed class SalaryGradeHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<CreateSalaryGradeCommand, Guid>,
    IRequestHandler<UpdateSalaryGradeCommand>,
    IRequestHandler<GetSalaryGradesQuery, IReadOnlyList<SalaryGradeDto>>
{
    public async Task<Guid> Handle(CreateSalaryGradeCommand request, CancellationToken ct)
    {
        var d = request.Data;
        await EnsureCodeIsFreeAsync(d.Code, null, ct);

        var grade = SalaryGrade.Create(currentUser.RequireTenantId(), d.Code, d.Name, d.CurrencyCode, d.MinAnnual, d.MaxAnnual);
        db.SalaryGrades.Add(grade);
        await db.SaveChangesAsync(ct);
        return grade.Id;
    }

    public async Task Handle(UpdateSalaryGradeCommand request, CancellationToken ct)
    {
        var d = request.Data;
        var grade = await db.SalaryGrades.FirstOrDefaultAsync(g => g.Id == request.Id, ct)
                    ?? throw new NotFoundException("Salary grade", request.Id);

        await EnsureCodeIsFreeAsync(d.Code, grade.Id, ct);
        grade.Update(d.Code, d.Name, d.CurrencyCode, d.MinAnnual, d.MaxAnnual);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SalaryGradeDto>> Handle(GetSalaryGradesQuery request, CancellationToken ct)
        => await db.SalaryGrades.AsNoTracking()
            .Where(g => request.IncludeInactive || g.IsActive)
            .OrderBy(g => g.Code)
            .Select(g => new SalaryGradeDto(g.Id, g.Code, g.Name, g.CurrencyCode, g.MinAnnual, g.MaxAnnual, g.IsActive))
            .ToListAsync(ct);

    private async Task EnsureCodeIsFreeAsync(string code, Guid? excludeId, CancellationToken ct)
    {
        var normalized = code.Trim().ToUpperInvariant();
        if (await db.SalaryGrades.AnyAsync(g => g.Code == normalized && g.Id != excludeId, ct))
            throw new ConflictException($"Grade code '{normalized}' is already in use.");
    }
}
