namespace HR.Employee.API.Application.Assets;

using FluentValidation;
using HR.Employee.API.Application.Common.Exceptions;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Domain.Assets;
using HR.Employee.API.Domain.Employees;
using HR.Employee.API.Domain.Lifecycle;
using HR.Shared.Library.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

// Assets page: inventory (register), assignments (kis ke paas kya), returns (wapsi due / leavers), history (log),
// aur "My assets" (har employee apni cheezein dekhe aur receipt confirm kare).
// HR: employees.view dekhna, employees.edit chalana. Categories: employees.edit ya settings.manage.

#region DTOs

public sealed record AssetSummaryDto(
    int Total, int Available, int Assigned, int InRepair, int RetiredOrLost,
    int ReturnsDue, int OverdueReturns, int WithLeavers, int Unacknowledged, int WarrantyExpiring, decimal TotalValue);

public sealed record AssetListItemDto(
    Guid Id, string AssetTag, string Name, Guid CategoryId, string CategoryName, string? CategoryIcon,
    string? Brand, string? Model, string? SerialNumber, Guid? LocationId, string? LocationName,
    AssetStatus Status, AssetCondition Condition, DateOnly? PurchaseDate, decimal? PurchaseCost, DateOnly? WarrantyUntil,
    Guid? HolderEmployeeId, string? HolderName, string? HolderCode, DateOnly? AssignedOn, DateOnly? DueBack);

public sealed record AssetAssignmentDto(
    Guid Id, Guid AssetId, string AssetTag, string AssetName, string CategoryName, string? CategoryIcon,
    Guid EmployeeId, string EmployeeCode, string EmployeeName, string? DepartmentName, EmploymentStatus EmploymentStatus,
    DateOnly? LastWorkingDay, DateOnly AssignedOn, DateOnly? DueBack, AssetCondition ConditionOut, string? AssignNote,
    DateTime? AcknowledgedAt, DateOnly? ReturnedOn, AssetCondition? ConditionIn, string? ReturnNote);

public sealed record AssetEventDto(
    Guid Id, Guid AssetId, string AssetTag, string AssetName, AssetEventType Type, AssetStatus Status,
    Guid? EmployeeId, string? EmployeeName, string? ByName, DateTime At, string? Detail);

public sealed record AssetDto(
    Guid Id, string AssetTag, string Name, Guid CategoryId, string CategoryName, string? CategoryIcon,
    string? Brand, string? Model, string? SerialNumber, Guid? LocationId, string? LocationName,
    DateOnly? PurchaseDate, decimal? PurchaseCost, string? Vendor, DateOnly? WarrantyUntil,
    AssetCondition Condition, AssetStatus Status, string? Notes, DateTime CreatedAt, uint RowVersion,
    IReadOnlyList<AssetAssignmentDto> Assignments, IReadOnlyList<AssetEventDto> Events);

public sealed record MyAssetDto(
    Guid AssignmentId, Guid AssetId, string AssetTag, string Name, string CategoryName, string? CategoryIcon,
    string? Brand, string? Model, string? SerialNumber, DateOnly AssignedOn, DateOnly? DueBack, AssetCondition ConditionOut,
    string? AssignNote, DateTime? AcknowledgedAt, DateOnly? ReturnedOn);

public sealed record AssetCategoryDto(Guid Id, string Name, string? Description, string? Icon, bool IsActive, int AssetCount);

public sealed record AssetHolderOptionDto(Guid Id, string EmployeeCode, string Name, string? DepartmentName);

#endregion

#region Requests

public sealed record GetAssetSummaryQuery : IRequest<AssetSummaryDto>;

public sealed record GetAssetsQuery(AssetStatus? Status, Guid? CategoryId, string? Search) : IRequest<IReadOnlyList<AssetListItemDto>>;

public sealed record GetAssetQuery(Guid Id) : IRequest<AssetDto>;

public enum AssignmentState : byte { Open = 1, Due = 2, Returned = 3 }

public sealed record GetAssetAssignmentsQuery(AssignmentState State, Guid? EmployeeId, string? Search) : IRequest<IReadOnlyList<AssetAssignmentDto>>;

public sealed record GetAssetHistoryQuery(Guid? AssetId, AssetEventType? Type, string? Search, DateOnly? From, DateOnly? To)
    : IRequest<IReadOnlyList<AssetEventDto>>;

public sealed record GetMyAssetsQuery : IRequest<IReadOnlyList<MyAssetDto>>;

public sealed record GetNextAssetTagQuery : IRequest<string>;

public sealed record SaveAssetRequest(
    string? AssetTag, string Name, Guid CategoryId, string? Brand, string? Model, string? SerialNumber, Guid? LocationId,
    DateOnly? PurchaseDate, decimal? PurchaseCost, string? Vendor, DateOnly? WarrantyUntil, AssetCondition Condition, string? Notes);

public sealed record CreateAssetCommand(SaveAssetRequest Data) : IRequest<Guid>;
public sealed record UpdateAssetCommand(Guid Id, SaveAssetRequest Data) : IRequest;
public sealed record DeleteAssetCommand(Guid Id) : IRequest;

public sealed record AssignAssetRequest(Guid EmployeeId, DateOnly AssignedOn, DateOnly? DueBack, string? Note);
public sealed record AssignAssetCommand(Guid AssetId, AssignAssetRequest Data) : IRequest<Guid>;

public sealed record ReturnAssetRequest(DateOnly ReturnedOn, AssetCondition Condition, AssetStatus NextStatus, string? Note);
public sealed record ReturnAssetCommand(Guid AssetId, ReturnAssetRequest Data) : IRequest;

public sealed record ChangeDueBackCommand(Guid AssetId, DateOnly? DueBack) : IRequest;

public sealed record ChangeAssetStatusCommand(Guid AssetId, AssetStatus Status, string? Note) : IRequest;

public sealed record AcknowledgeAssetCommand(Guid AssignmentId) : IRequest;

public sealed record SaveAssetCategoryRequest(string Name, string? Description, string? Icon, bool IsActive);
public sealed record GetAssetCategoriesQuery(bool IncludeInactive = true) : IRequest<IReadOnlyList<AssetCategoryDto>>;
public sealed record CreateAssetCategoryCommand(SaveAssetCategoryRequest Data) : IRequest<Guid>;
public sealed record UpdateAssetCategoryCommand(Guid Id, SaveAssetCategoryRequest Data) : IRequest;
public sealed record DeleteAssetCategoryCommand(Guid Id) : IRequest;

/// <summary>Koi category na ho to aam categories bana do (Laptop, Monitor, Phone...).</summary>
public sealed record CreateStarterAssetCategoriesCommand : IRequest<int>;

#endregion

#region Validators

public sealed class SaveAssetRequestValidator : AbstractValidator<SaveAssetRequest>
{
    public SaveAssetRequestValidator()
    {
        RuleFor(x => x.AssetTag).MaximumLength(40);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Brand).MaximumLength(100);
        RuleFor(x => x.Model).MaximumLength(100);
        RuleFor(x => x.SerialNumber).MaximumLength(100);
        RuleFor(x => x.Vendor).MaximumLength(150);
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.Condition).IsInEnum();
        RuleFor(x => x.PurchaseCost).GreaterThanOrEqualTo(0).LessThan(10_000_000_000m).When(x => x.PurchaseCost is not null);
        RuleFor(x => x.WarrantyUntil).GreaterThanOrEqualTo(x => x.PurchaseDate)
            .When(x => x.WarrantyUntil is not null && x.PurchaseDate is not null)
            .WithMessage("Warranty end cannot be before the purchase date.");
    }
}

public sealed class CreateAssetValidator : AbstractValidator<CreateAssetCommand>
{
    public CreateAssetValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveAssetRequestValidator());
}

public sealed class UpdateAssetValidator : AbstractValidator<UpdateAssetCommand>
{
    public UpdateAssetValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveAssetRequestValidator());
}

public sealed class AssignAssetValidator : AbstractValidator<AssignAssetCommand>
{
    public AssignAssetValidator()
    {
        RuleFor(x => x.Data).NotNull();
        RuleFor(x => x.Data.EmployeeId).NotEmpty();
        RuleFor(x => x.Data.Note).MaximumLength(500);
        RuleFor(x => x.Data.DueBack).GreaterThanOrEqualTo(x => x.Data.AssignedOn).When(x => x.Data.DueBack is not null)
            .WithMessage("Return date cannot be before the assigned date.");
    }
}

public sealed class ReturnAssetValidator : AbstractValidator<ReturnAssetCommand>
{
    public ReturnAssetValidator()
    {
        RuleFor(x => x.Data).NotNull();
        RuleFor(x => x.Data.Condition).IsInEnum();
        RuleFor(x => x.Data.NextStatus).Must(s => s is AssetStatus.Available or AssetStatus.InRepair or AssetStatus.Lost)
            .WithMessage("After a return the asset is available, in repair or lost.");
        RuleFor(x => x.Data.Note).MaximumLength(500);
        RuleFor(x => x.Data.Note).NotEmpty().When(x => x.Data.NextStatus == AssetStatus.Lost)
            .WithMessage("Add a note when an asset is lost.");
    }
}

public sealed class ChangeAssetStatusValidator : AbstractValidator<ChangeAssetStatusCommand>
{
    public ChangeAssetStatusValidator()
    {
        RuleFor(x => x.Status).IsInEnum().NotEqual(AssetStatus.Assigned).WithMessage("Use assign to give an asset to an employee.");
        RuleFor(x => x.Note).MaximumLength(500);
        RuleFor(x => x.Note).NotEmpty().When(x => x.Status is AssetStatus.Retired or AssetStatus.Lost)
            .WithMessage("Add a note when retiring or losing an asset.");
    }
}

public sealed class SaveAssetCategoryRequestValidator : AbstractValidator<SaveAssetCategoryRequest>
{
    public SaveAssetCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(300);
        RuleFor(x => x.Icon).MaximumLength(40).Matches("^[a-z0-9_]*$").When(x => x.Icon is not null);
    }
}

public sealed class CreateAssetCategoryValidator : AbstractValidator<CreateAssetCategoryCommand>
{
    public CreateAssetCategoryValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveAssetCategoryRequestValidator());
}

public sealed class UpdateAssetCategoryValidator : AbstractValidator<UpdateAssetCategoryCommand>
{
    public UpdateAssetCategoryValidator() => RuleFor(x => x.Data).NotNull().SetValidator(new SaveAssetCategoryRequestValidator());
}

#endregion

#region Access

public sealed class AssetAccess(IAppDbContext db, ICurrentUser currentUser)
{
    public bool CanView => currentUser.HasPermission(Permissions.EmployeesView);
    public bool CanManage => currentUser.HasPermission(Permissions.EmployeesEdit);
    public bool CanManageCategories => CanManage || currentUser.HasPermission(Permissions.SettingsManage);

    public void EnsureCanView()
    {
        if (!CanView)
            throw new UnauthorizedAccessException("You do not have permission to see company assets.");
    }

    public void EnsureCanManage()
    {
        if (!CanManage)
            throw new UnauthorizedAccessException("You do not have permission to manage company assets.");
    }

    public void EnsureCanManageCategories()
    {
        if (!CanManageCategories)
            throw new UnauthorizedAccessException("You do not have permission to change asset categories.");
    }

    public async Task<Guid?> MyEmployeeIdAsync(CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
            return null;
        var id = await db.Employees.AsNoTracking().Where(e => e.UserId == userId).Select(e => e.Id).FirstOrDefaultAsync(ct);
        return id == Guid.Empty ? null : id;
    }
}

#endregion

#region Queries

public sealed class AssetQueryHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<GetAssetSummaryQuery, AssetSummaryDto>,
    IRequestHandler<GetAssetsQuery, IReadOnlyList<AssetListItemDto>>,
    IRequestHandler<GetAssetQuery, AssetDto>,
    IRequestHandler<GetAssetAssignmentsQuery, IReadOnlyList<AssetAssignmentDto>>,
    IRequestHandler<GetAssetHistoryQuery, IReadOnlyList<AssetEventDto>>,
    IRequestHandler<GetMyAssetsQuery, IReadOnlyList<MyAssetDto>>,
    IRequestHandler<GetNextAssetTagQuery, string>,
    IRequestHandler<GetAssetCategoriesQuery, IReadOnlyList<AssetCategoryDto>>
{
    /// <summary>Returns tab: is se pehle wapsi ki date aa rahi ho to "due".</summary>
    public const int DueWindowDays = 14;
    public const int WarrantyWindowDays = 30;
    public const string TagPrefix = "AST-";

    private readonly AssetAccess access = new(db, currentUser);

    public async Task<AssetSummaryDto> Handle(GetAssetSummaryQuery q, CancellationToken ct)
    {
        access.EnsureCanView();
        var today = Today;

        var counts = await db.Assets.AsNoTracking()
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Value = g.Sum(a => a.PurchaseCost ?? 0) })
            .ToListAsync(ct);
        int Count(AssetStatus s) => counts.Where(c => c.Status == s).Sum(c => c.Count);

        var open = OpenRows();
        var returnsDue = await DueRows(open, today).CountAsync(ct);
        var overdue = await open.CountAsync(x => x.a.DueBack < today, ct);
        var leavers = await open.CountAsync(x => x.e.EmploymentStatus == EmploymentStatus.OnNotice || x.e.EmploymentStatus == EmploymentStatus.Exited, ct);
        var unack = await open.CountAsync(x => x.a.AcknowledgedAt == null, ct);
        var warrantyEnd = today.AddDays(WarrantyWindowDays);
        var warranty = await db.Assets.AsNoTracking().CountAsync(a =>
            a.Status != AssetStatus.Retired && a.Status != AssetStatus.Lost &&
            a.WarrantyUntil >= today && a.WarrantyUntil <= warrantyEnd, ct);

        return new AssetSummaryDto(
            counts.Sum(c => c.Count), Count(AssetStatus.Available), Count(AssetStatus.Assigned), Count(AssetStatus.InRepair),
            Count(AssetStatus.Retired) + Count(AssetStatus.Lost), returnsDue, overdue, leavers, unack, warranty,
            counts.Where(c => c.Status is not (AssetStatus.Retired or AssetStatus.Lost)).Sum(c => c.Value));
    }

    public async Task<IReadOnlyList<AssetListItemDto>> Handle(GetAssetsQuery q, CancellationToken ct)
    {
        access.EnsureCanView();

        var assets = db.Assets.AsNoTracking();
        if (q.Status is { } status) assets = assets.Where(a => a.Status == status);
        if (q.CategoryId is { } categoryId) assets = assets.Where(a => a.AssetCategoryId == categoryId);

        var rows = from a in assets
                   join c in db.AssetCategories on a.AssetCategoryId equals c.Id
                   join l in db.Locations on a.LocationId equals l.Id into ls
                   from l in ls.DefaultIfEmpty()
                   from asg in db.AssetAssignments.Where(x => x.AssetId == a.Id && x.ReturnedOn == null).DefaultIfEmpty()
                   join e in db.Employees on asg.EmployeeId equals e.Id into es
                   from e in es.DefaultIfEmpty()
                   select new { a, c, l, asg, e };

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = $"%{q.Search.Trim()}%";
            rows = rows.Where(x => EF.Functions.ILike(x.a.AssetTag, term) || EF.Functions.ILike(x.a.Name, term)
                                   || EF.Functions.ILike(x.a.SerialNumber ?? "", term) || EF.Functions.ILike(x.a.Model ?? "", term)
                                   || (x.e != null && EF.Functions.ILike(x.e.FirstName + " " + x.e.LastName, term)));
        }

        return await rows
            .OrderBy(x => x.a.AssetTag)
            .Take(1000)
            .Select(x => new AssetListItemDto(
                x.a.Id, x.a.AssetTag, x.a.Name, x.c.Id, x.c.Name, x.c.Icon, x.a.Brand, x.a.Model, x.a.SerialNumber,
                x.a.LocationId, x.l == null ? null : x.l.Name, x.a.Status, x.a.Condition, x.a.PurchaseDate, x.a.PurchaseCost, x.a.WarrantyUntil,
                x.e == null ? null : x.e.Id, x.e == null ? null : x.e.FirstName + " " + x.e.LastName, x.e == null ? null : x.e.EmployeeCode,
                x.asg == null ? null : x.asg.AssignedOn, x.asg == null ? null : x.asg.DueBack))
            .ToListAsync(ct);
    }

    public async Task<AssetDto> Handle(GetAssetQuery q, CancellationToken ct)
    {
        access.EnsureCanView();

        var a = await db.Assets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == q.Id, ct) ?? throw new NotFoundException("Asset", q.Id);
        var category = await db.AssetCategories.AsNoTracking().IgnoreQueryFilters()
            .Where(c => c.Id == a.AssetCategoryId && c.TenantId == a.TenantId).Select(c => new { c.Name, c.Icon }).FirstAsync(ct);
        string? location = a.LocationId is { } locationId
            ? await db.Locations.AsNoTracking().IgnoreQueryFilters().Where(l => l.Id == locationId && l.TenantId == a.TenantId).Select(l => l.Name).FirstOrDefaultAsync(ct)
            : null;

        var assignments = await AssignmentRows(db.Assets.AsNoTracking().Where(x => x.Id == a.Id))
            .OrderByDescending(x => x.a.ReturnedOn == null).ThenByDescending(x => x.a.AssignedOn)
            .Take(200)
            .Select(ToAssignmentDto())
            .ToListAsync(ct);
        var lastWorking = await LastWorkingDaysAsync(assignments.Where(x => x.ReturnedOn == null).Select(x => x.EmployeeId), ct);
        assignments = assignments.Select(x => x with { LastWorkingDay = x.LastWorkingDay ?? lastWorking.GetValueOrDefault(x.EmployeeId) }).ToList();

        var events = await EventRows(db.Assets.AsNoTracking().Where(x => x.Id == a.Id))
            .OrderByDescending(x => x.At).Take(200).Select(ToEventDto()).ToListAsync(ct);

        return new AssetDto(a.Id, a.AssetTag, a.Name, a.AssetCategoryId, category.Name, category.Icon, a.Brand, a.Model, a.SerialNumber,
            a.LocationId, location, a.PurchaseDate, a.PurchaseCost, a.Vendor, a.WarrantyUntil, a.Condition, a.Status, a.Notes,
            a.CreatedAt, a.RowVersion, assignments, events);
    }

    public async Task<IReadOnlyList<AssetAssignmentDto>> Handle(GetAssetAssignmentsQuery q, CancellationToken ct)
    {
        access.EnsureCanView();
        var today = Today;

        var rows = AssignmentRows(db.Assets.AsNoTracking());
        rows = q.State switch
        {
            AssignmentState.Returned => rows.Where(x => x.a.ReturnedOn != null),
            AssignmentState.Due => DueRows(rows.Where(x => x.a.ReturnedOn == null), today),
            _ => rows.Where(x => x.a.ReturnedOn == null)
        };
        if (q.EmployeeId is { } employeeId) rows = rows.Where(x => x.e.Id == employeeId);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = $"%{q.Search.Trim()}%";
            rows = rows.Where(x => EF.Functions.ILike(x.e.FirstName + " " + x.e.LastName, term) || EF.Functions.ILike(x.e.EmployeeCode, term)
                                   || EF.Functions.ILike(x.asset.AssetTag, term) || EF.Functions.ILike(x.asset.Name, term));
        }

        var ordered = q.State == AssignmentState.Returned
            ? rows.OrderByDescending(x => x.a.ReturnedOn).ThenBy(x => x.asset.AssetTag)
            : rows.OrderBy(x => x.a.DueBack ?? DateOnly.MaxValue).ThenBy(x => x.e.FirstName).ThenBy(x => x.asset.AssetTag);

        var list = await ordered.Take(q.State == AssignmentState.Returned ? 300 : 1000).Select(ToAssignmentDto()).ToListAsync(ct);

        var lastWorking = await LastWorkingDaysAsync(list.Where(x => x.ReturnedOn == null).Select(x => x.EmployeeId), ct);
        return list.Select(x => x with { LastWorkingDay = x.LastWorkingDay ?? lastWorking.GetValueOrDefault(x.EmployeeId) }).ToList();
    }

    public async Task<IReadOnlyList<AssetEventDto>> Handle(GetAssetHistoryQuery q, CancellationToken ct)
    {
        access.EnsureCanView();

        var assets = db.Assets.AsNoTracking();
        if (q.AssetId is { } assetId) assets = assets.Where(a => a.Id == assetId);

        var rows = EventRows(assets);
        if (q.Type is { } type) rows = rows.Where(x => x.Type == type);
        if (q.From is { } from) rows = rows.Where(x => x.At >= from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (q.To is { } to) rows = rows.Where(x => x.At < to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = $"%{q.Search.Trim()}%";
            rows = rows.Where(x => EF.Functions.ILike(x.AssetTag, term) || EF.Functions.ILike(x.AssetName, term)
                                   || EF.Functions.ILike(x.EmployeeName ?? "", term) || EF.Functions.ILike(x.Detail ?? "", term));
        }

        return await rows.OrderByDescending(x => x.At).Take(500).Select(ToEventDto()).ToListAsync(ct);
    }

    /// <summary>Mere paas abhi jo hai + pichhle 12 mahine mein lautaya hua.</summary>
    public async Task<IReadOnlyList<MyAssetDto>> Handle(GetMyAssetsQuery q, CancellationToken ct)
    {
        if (await access.MyEmployeeIdAsync(ct) is not { } me)
            return [];
        var since = Today.AddMonths(-12);

        return await (from a in db.Assets.AsNoTracking()
                      join c in db.AssetCategories on a.AssetCategoryId equals c.Id
                      from x in a.Assignments
                      where x.EmployeeId == me && (x.ReturnedOn == null || x.ReturnedOn >= since)
                      orderby x.ReturnedOn == null descending, x.AssignedOn descending
                      select new MyAssetDto(x.Id, a.Id, a.AssetTag, a.Name, c.Name, c.Icon, a.Brand, a.Model, a.SerialNumber,
                          x.AssignedOn, x.DueBack, x.ConditionOut, x.AssignNote, x.AcknowledgedAt, x.ReturnedOn))
            .Take(200).ToListAsync(ct);
    }

    public async Task<string> Handle(GetNextAssetTagQuery q, CancellationToken ct)
    {
        access.EnsureCanManage();
        return await NextTagAsync(db, ct);
    }

    public async Task<IReadOnlyList<AssetCategoryDto>> Handle(GetAssetCategoriesQuery q, CancellationToken ct)
    {
        // Category list form ke liye bhi chahiye (view wale bhi filter karte hain)
        access.EnsureCanView();
        return await db.AssetCategories.AsNoTracking()
            .Where(c => q.IncludeInactive || c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new AssetCategoryDto(c.Id, c.Name, c.Description, c.Icon, c.IsActive,
                db.Assets.Count(a => a.AssetCategoryId == c.Id)))
            .ToListAsync(ct);
    }

    /// <summary>AST-0001, AST-0002 ... (sab se bada number + 1). Hand-typed tags is mein shamil nahi hote.</summary>
    internal static async Task<string> NextTagAsync(IAppDbContext db, CancellationToken ct)
    {
        var tags = await db.Assets.AsNoTracking()
            .Where(a => a.AssetTag.StartsWith(TagPrefix))
            .Select(a => a.AssetTag).ToListAsync(ct);
        var max = tags.Select(t => int.TryParse(t[TagPrefix.Length..], out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return $"{TagPrefix}{max + 1:D4}";
    }

    /// <summary>Init properties (constructor record nahi) — EF baad ke Where/OrderBy mein in members ko SQL bana sake.</summary>
    private sealed class AssignmentRow
    {
        public AssetAssignment a { get; init; } = default!;
        public Asset asset { get; init; } = default!;
        public AssetCategory c { get; init; } = default!;
        public Employee e { get; init; } = default!;
    }

    private IQueryable<AssignmentRow> AssignmentRows(IQueryable<Asset> assets)
        => from asset in assets
           join c in db.AssetCategories on asset.AssetCategoryId equals c.Id
           from a in asset.Assignments
           join e in db.Employees on a.EmployeeId equals e.Id
           select new AssignmentRow { a = a, asset = asset, c = c, e = e };

    private IQueryable<AssignmentRow> OpenRows() => AssignmentRows(db.Assets.AsNoTracking()).Where(x => x.a.ReturnedOn == null);

    /// <summary>Wapsi due: date aa gayi/aane wali, ya employee ja raha hai / ja chuka.</summary>
    private static IQueryable<AssignmentRow> DueRows(IQueryable<AssignmentRow> open, DateOnly today)
    {
        var dueBy = today.AddDays(DueWindowDays);
        return open.Where(x => x.a.DueBack <= dueBy
                               || x.e.EmploymentStatus == EmploymentStatus.OnNotice
                               || x.e.EmploymentStatus == EmploymentStatus.Exited);
    }

    private static System.Linq.Expressions.Expression<Func<AssignmentRow, AssetAssignmentDto>> ToAssignmentDto()
        => x => new AssetAssignmentDto(
            x.a.Id, x.asset.Id, x.asset.AssetTag, x.asset.Name, x.c.Name, x.c.Icon,
            x.e.Id, x.e.EmployeeCode, x.e.FirstName + " " + x.e.LastName, x.e.Department.Name, x.e.EmploymentStatus,
            x.e.ExitDate, x.a.AssignedOn, x.a.DueBack, x.a.ConditionOut, x.a.AssignNote,
            x.a.AcknowledgedAt, x.a.ReturnedOn, x.a.ConditionIn, x.a.ReturnNote);

    /// <summary>Notice par employee ka last working day = khule exit case ki AnchorDate.</summary>
    private async Task<Dictionary<Guid, DateOnly?>> LastWorkingDaysAsync(IEnumerable<Guid> employeeIds, CancellationToken ct)
    {
        var ids = employeeIds.Distinct().ToList();
        if (ids.Count == 0)
            return [];
        return await db.LifecycleCases.AsNoTracking()
            .Where(c => ids.Contains(c.EmployeeId) && c.Kind == LifecycleKind.Exit && c.Status == CaseStatus.InProgress)
            .ToDictionaryAsync(c => c.EmployeeId, c => (DateOnly?)c.AnchorDate, ct);
    }

    private sealed class EventRow
    {
        public Guid Id { get; init; }
        public Guid AssetId { get; init; }
        public string AssetTag { get; init; } = default!;
        public string AssetName { get; init; } = default!;
        public AssetEventType Type { get; init; }
        public AssetStatus Status { get; init; }
        public Guid? EmployeeId { get; init; }
        public string? EmployeeName { get; init; }
        public string? ByName { get; init; }
        public DateTime At { get; init; }
        public string? Detail { get; init; }
    }

    private IQueryable<EventRow> EventRows(IQueryable<Asset> assets)
        => from asset in assets
           from ev in asset.Events
           join e in db.Employees on ev.EmployeeId equals e.Id into es
           from e in es.DefaultIfEmpty()
           select new EventRow
           {
               Id = ev.Id, AssetId = asset.Id, AssetTag = asset.AssetTag, AssetName = asset.Name, Type = ev.Type, Status = ev.Status,
               EmployeeId = ev.EmployeeId, EmployeeName = e == null ? null : e.FirstName + " " + e.LastName,
               ByName = ev.ByUserId == null ? null
                   : db.Employees.Where(x => x.UserId == ev.ByUserId).Select(x => x.FirstName + " " + x.LastName).FirstOrDefault(), At = ev.At, Detail = ev.Detail
           };

    private static System.Linq.Expressions.Expression<Func<EventRow, AssetEventDto>> ToEventDto()
        => x => new AssetEventDto(x.Id, x.AssetId, x.AssetTag, x.AssetName, x.Type, x.Status, x.EmployeeId, x.EmployeeName, x.ByName, x.At, x.Detail);

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
}

#endregion

#region Commands

public sealed class AssetCommandHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<CreateAssetCommand, Guid>,
    IRequestHandler<UpdateAssetCommand>,
    IRequestHandler<DeleteAssetCommand>,
    IRequestHandler<AssignAssetCommand, Guid>,
    IRequestHandler<ReturnAssetCommand>,
    IRequestHandler<ChangeDueBackCommand>,
    IRequestHandler<ChangeAssetStatusCommand>,
    IRequestHandler<AcknowledgeAssetCommand>
{
    private readonly AssetAccess access = new(db, currentUser);

    public async Task<Guid> Handle(CreateAssetCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var d = c.Data;
        await EnsureReferencesAsync(d, ct);

        var tag = string.IsNullOrWhiteSpace(d.AssetTag) ? await AssetQueryHandlers.NextTagAsync(db, ct) : d.AssetTag;
        var asset = Asset.Create(currentUser.RequireTenantId(), tag, ToDetails(d), currentUser.UserId, DateTime.UtcNow);
        await EnsureUniqueAsync(asset.AssetTag, asset.SerialNumber, null, ct);

        db.Assets.Add(asset);
        await SaveAsync(ct);
        return asset.Id;
    }

    public async Task Handle(UpdateAssetCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var asset = await AssetAsync(c.Id, ct);
        var d = c.Data;
        await EnsureReferencesAsync(d, ct, asset.AssetCategoryId);

        asset.Update(string.IsNullOrWhiteSpace(d.AssetTag) ? asset.AssetTag : d.AssetTag, ToDetails(d), currentUser.UserId, DateTime.UtcNow);
        await EnsureUniqueAsync(asset.AssetTag, asset.SerialNumber, asset.Id, ct);
        await SaveAsync(ct);
    }

    /// <summary>Kabhi assign nahi hua (galti se bana) to delete; warna history bachao — retire karo.</summary>
    public async Task Handle(DeleteAssetCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var asset = await AssetAsync(c.Id, ct);
        if (await db.AssetAssignments.AnyAsync(a => a.AssetId == asset.Id, ct))
            throw new ConflictException("This asset has assignment history. Retire it instead of deleting.");

        db.Assets.Remove(asset);   // soft delete (AppDbContext)
        await SaveAsync(ct);
    }

    public async Task<Guid> Handle(AssignAssetCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var asset = await AssetAsync(c.AssetId, ct);
        var d = c.Data;

        var employee = await db.Employees.AsNoTracking().Where(e => e.Id == d.EmployeeId)
                           .Select(e => new { e.Id, e.EmploymentStatus, e.JoiningDate }).FirstOrDefaultAsync(ct)
                       ?? throw new NotFoundException("Employee", d.EmployeeId);
        if (employee.EmploymentStatus == EmploymentStatus.Exited)
            throw new ConflictException("This employee has exited.");

        var assignment = asset.Assign(employee.Id, d.AssignedOn, d.DueBack, d.Note, currentUser.UserId, DateTime.UtcNow);
        await SaveAsync(ct);
        return assignment.Id;
    }

    public async Task Handle(ReturnAssetCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var asset = await AssetAsync(c.AssetId, ct);
        var d = c.Data;
        if (d.ReturnedOn > DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1))
            throw new ConflictException("Return date cannot be in the future.");

        asset.Return(d.ReturnedOn, d.Condition, d.NextStatus, d.Note, currentUser.UserId, DateTime.UtcNow);
        await SaveAsync(ct);
    }

    public async Task Handle(ChangeDueBackCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var asset = await AssetAsync(c.AssetId, ct);
        asset.ChangeDueBack(c.DueBack);
        await SaveAsync(ct);
    }

    public async Task Handle(ChangeAssetStatusCommand c, CancellationToken ct)
    {
        access.EnsureCanManage();
        var asset = await AssetAsync(c.AssetId, ct);
        asset.ChangeStatus(c.Status, c.Note, currentUser.UserId, DateTime.UtcNow);
        await SaveAsync(ct);
    }

    /// <summary>Employee khud "mil gaya" confirm karta hai (sirf apni khuli assignment).</summary>
    public async Task Handle(AcknowledgeAssetCommand c, CancellationToken ct)
    {
        var me = await access.MyEmployeeIdAsync(ct)
                 ?? throw new UnauthorizedAccessException("Your login is not linked to an employee record.");
        var assetId = await db.AssetAssignments.Where(a => a.Id == c.AssignmentId).Select(a => (Guid?)a.AssetId).FirstOrDefaultAsync(ct)
                      ?? throw new NotFoundException("Assignment", c.AssignmentId);
        var asset = await AssetAsync(assetId, ct);
        if (asset.OpenAssignment?.EmployeeId != me)
            throw new UnauthorizedAccessException("This asset is not assigned to you.");

        asset.Acknowledge(c.AssignmentId, me, currentUser.UserId, DateTime.UtcNow);
        await SaveAsync(ct);
    }

    private async Task<Asset> AssetAsync(Guid id, CancellationToken ct)
        => await db.Assets.Include(a => a.Assignments.Where(x => x.ReturnedOn == null)).FirstOrDefaultAsync(a => a.Id == id, ct)
           ?? throw new NotFoundException("Asset", id);

    private async Task EnsureReferencesAsync(SaveAssetRequest d, CancellationToken ct, Guid? currentCategoryId = null)
    {
        var category = await db.AssetCategories.AsNoTracking().Where(c => c.Id == d.CategoryId).Select(c => new { c.IsActive }).FirstOrDefaultAsync(ct)
                       ?? throw new NotFoundException("Asset category", d.CategoryId);
        if (!category.IsActive && d.CategoryId != currentCategoryId)
            throw new ConflictException("This category is inactive.");
        if (d.LocationId is { } locationId && locationId != Guid.Empty && !await db.Locations.AnyAsync(l => l.Id == locationId, ct))
            throw new NotFoundException("Location", locationId);
    }

    private async Task EnsureUniqueAsync(string tag, string? serial, Guid? exceptId, CancellationToken ct)
    {
        if (await db.Assets.AnyAsync(a => a.AssetTag == tag && a.Id != exceptId, ct))
            throw new ConflictException($"Asset tag {tag} is already used.");
        if (serial is not null && await db.Assets.AnyAsync(a => a.SerialNumber == serial && a.Id != exceptId, ct))
            throw new ConflictException($"Another asset already has serial number {serial}.");
    }

    private static AssetDetails ToDetails(SaveAssetRequest d)
        => new(d.Name, d.CategoryId, d.Brand, d.Model, d.SerialNumber, d.LocationId, d.PurchaseDate, d.PurchaseCost, d.Vendor,
            d.WarrantyUntil, d.Condition, d.Notes);

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UX_AssetAssignments_AssetId_Open") == true)
        {
            throw new ConflictException("This asset was just assigned by someone else. Refresh and try again.");
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_Assets_TenantId_") == true)
        {
            throw new ConflictException("The asset tag or serial number is already used.");
        }
    }
}

#endregion

#region Categories

public sealed class AssetCategoryHandlers(IAppDbContext db, ICurrentUser currentUser) :
    IRequestHandler<CreateAssetCategoryCommand, Guid>,
    IRequestHandler<UpdateAssetCategoryCommand>,
    IRequestHandler<DeleteAssetCategoryCommand>,
    IRequestHandler<CreateStarterAssetCategoriesCommand, int>
{
    private readonly AssetAccess access = new(db, currentUser);

    public async Task<Guid> Handle(CreateAssetCategoryCommand c, CancellationToken ct)
    {
        access.EnsureCanManageCategories();
        var d = c.Data;
        await EnsureNameIsFreeAsync(d.Name, null, ct);

        var category = AssetCategory.Create(currentUser.RequireTenantId(), d.Name, d.Description, d.Icon);
        if (!d.IsActive) category.Update(d.Name, d.Description, d.Icon, false);
        db.AssetCategories.Add(category);
        await db.SaveChangesAsync(ct);
        return category.Id;
    }

    public async Task Handle(UpdateAssetCategoryCommand c, CancellationToken ct)
    {
        access.EnsureCanManageCategories();
        var category = await db.AssetCategories.FirstOrDefaultAsync(x => x.Id == c.Id, ct) ?? throw new NotFoundException("Asset category", c.Id);
        var d = c.Data;
        await EnsureNameIsFreeAsync(d.Name, category.Id, ct);

        category.Update(d.Name, d.Description, d.Icon, d.IsActive);
        await db.SaveChangesAsync(ct);
    }

    public async Task Handle(DeleteAssetCategoryCommand c, CancellationToken ct)
    {
        access.EnsureCanManageCategories();
        var category = await db.AssetCategories.FirstOrDefaultAsync(x => x.Id == c.Id, ct) ?? throw new NotFoundException("Asset category", c.Id);
        if (await db.Assets.IgnoreQueryFilters().AnyAsync(a => a.AssetCategoryId == category.Id && a.TenantId == category.TenantId, ct))
            throw new ConflictException("Assets use this category. Make it inactive instead.");

        db.AssetCategories.Remove(category);
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> Handle(CreateStarterAssetCategoriesCommand c, CancellationToken ct)
    {
        access.EnsureCanManageCategories();
        if (await db.AssetCategories.AnyAsync(ct))
            return 0;

        var tenantId = currentUser.RequireTenantId();
        foreach (var (name, icon, description) in Starter)
            db.AssetCategories.Add(AssetCategory.Create(tenantId, name, description, icon));
        await db.SaveChangesAsync(ct);
        return Starter.Length;
    }

    private static readonly (string Name, string Icon, string Description)[] Starter =
    [
        ("Laptop", "laptop_mac", "Laptops and notebooks"),
        ("Monitor", "desktop_windows", "Screens and displays"),
        ("Mobile phone", "smartphone", "Company phones"),
        ("Accessories", "keyboard", "Keyboard, mouse, headset, docking station"),
        ("SIM card", "sim_card", "Company SIM and data cards"),
        ("Access card", "badge", "ID and door access cards"),
        ("Furniture", "chair", "Chairs, desks, cabinets"),
        ("Vehicle", "directions_car", "Pool and assigned vehicles")
    ];

    private async Task EnsureNameIsFreeAsync(string name, Guid? exceptId, CancellationToken ct)
    {
        var trimmed = name.Trim();
        if (await db.AssetCategories.AnyAsync(x => x.Name.ToLower() == trimmed.ToLower() && x.Id != exceptId, ct))
            throw new ConflictException($"A category called {trimmed} already exists.");
    }
}

#endregion
