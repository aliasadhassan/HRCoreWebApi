namespace HR.Employee.API.Application.Employees.Queries;

using FluentValidation;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Application.Common.Models;
using HR.Employee.API.Domain.Employees;
using MediatR;
using Microsoft.EntityFrameworkCore;

/// <summary>Employees page: paging + search + filters. Default: exited employees nahi dikhte.</summary>
public sealed record GetEmployeesQuery : IRequest<PagedResult<EmployeeListItemDto>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public Guid? DepartmentId { get; init; }
    public Guid? LocationId { get; init; }
    public EmploymentStatus? Status { get; init; }
    public bool IncludeExited { get; init; }
}

public sealed class GetEmployeesValidator : AbstractValidator<GetEmployeesQuery>
{
    public GetEmployeesValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.Status).IsInEnum();
    }
}

public sealed class GetEmployeesHandler(IAppDbContext db)
    : IRequestHandler<GetEmployeesQuery, PagedResult<EmployeeListItemDto>>
{
    public Task<PagedResult<EmployeeListItemDto>> Handle(GetEmployeesQuery q, CancellationToken ct)
    {
        var query = db.Employees.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            query = query.Where(e => e.FirstName.Contains(s) || e.LastName.Contains(s)
                                  || e.WorkEmail.Contains(s) || e.EmployeeCode.Contains(s));
        }

        if (q.DepartmentId is { } departmentId)
            query = query.Where(e => e.DepartmentId == departmentId);
        if (q.LocationId is { } locationId)
            query = query.Where(e => e.LocationId == locationId);

        if (q.Status is { } status)
            query = query.Where(e => e.EmploymentStatus == status);
        else if (!q.IncludeExited)
            query = query.Where(e => e.EmploymentStatus != EmploymentStatus.Exited);

        return query
            .OrderBy(e => e.FirstName).ThenBy(e => e.LastName)
            .Select(e => new EmployeeListItemDto(
                e.Id,
                e.EmployeeCode,
                e.FirstName + " " + e.LastName,
                e.WorkEmail,
                e.Department.Name,
                e.Designation.Title,
                e.Location.Name,
                e.Manager != null ? e.Manager.FirstName + " " + e.Manager.LastName : null,
                e.EmploymentType,
                e.EmploymentStatus,
                e.JoiningDate,
                e.PhotoStorageKey))
            .ToPagedResultAsync(q.Page, q.PageSize, ct);
    }
}
