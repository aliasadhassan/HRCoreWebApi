namespace HR.Employee.API.Application.Employees.Queries;

using HR.Employee.API.Application.Common.Exceptions;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record GetEmployeeByIdQuery(Guid Id) : IRequest<EmployeeDetailsDto>;

public sealed class GetEmployeeByIdHandler(IAppDbContext db) : IRequestHandler<GetEmployeeByIdQuery, EmployeeDetailsDto>
{
    public async Task<EmployeeDetailsDto> Handle(GetEmployeeByIdQuery q, CancellationToken ct)
    {
        var dto = await db.Employees
            .AsNoTracking()
            .Where(e => e.Id == q.Id)
            .Select(e => new EmployeeDetailsDto(
                e.Id, e.EmployeeCode, e.UserId,
                e.FirstName, e.MiddleName, e.LastName,
                e.Gender, e.DateOfBirth, e.MaritalStatus, e.NationalityCode,
                e.WorkEmail, e.PersonalEmail, e.WorkPhone, e.PersonalPhone,
                new AddressDto(e.Address!.Line1, e.Address.Line2, e.Address.City,
                               e.Address.State, e.Address.PostalCode, e.Address.CountryCode),
                new LookupDto(e.Location.Id, e.Location.Name),
                new LookupDto(e.Department.Id, e.Department.Name),
                new LookupDto(e.Designation.Id, e.Designation.Title),
                e.Manager != null ? new LookupDto(e.Manager.Id, e.Manager.FirstName + " " + e.Manager.LastName) : null,
                e.EmploymentType, e.EmploymentStatus,
                e.JoiningDate, e.ProbationEndDate, e.ConfirmationDate, e.NoticePeriodDays,
                e.ExitDate, e.ExitReason, e.PhotoStorageKey,
                e.EmergencyContacts
                    .OrderByDescending(c => c.IsPrimary)
                    .Select(c => new EmergencyContactDto(c.Id, c.Name, c.Relationship, c.Phone, c.AlternatePhone, c.IsPrimary))
                    .ToList(),
                e.JobHistory
                    .OrderByDescending(h => h.EffectiveDate)
                    .Select(h => new JobHistoryDto(h.Id, h.EffectiveDate, h.ChangeType, h.DepartmentId,
                                                   h.DesignationId, h.LocationId, h.ManagerId, h.EmploymentStatus, h.Remarks))
                    .ToList()))
            .AsSplitQuery()
            .FirstOrDefaultAsync(ct);

        return dto ?? throw new NotFoundException("Employee", q.Id);
    }
}
