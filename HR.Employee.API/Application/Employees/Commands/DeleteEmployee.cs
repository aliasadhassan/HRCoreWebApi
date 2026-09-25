namespace HR.Employee.API.Application.Employees.Commands;

using HR.Employee.API.Application.Common.Exceptions;
using HR.Employee.API.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Sirf ghalti se bana record hatane ke liye (soft delete).
/// Jis employee ki history ho (leaves, reports, department head), usay Exit karo — delete nahi.
/// </summary>
public sealed record DeleteEmployeeCommand(Guid EmployeeId) : IRequest;

public sealed class DeleteEmployeeHandler(IAppDbContext db) : IRequestHandler<DeleteEmployeeCommand>
{
    public async Task Handle(DeleteEmployeeCommand request, CancellationToken ct)
    {
        var employee = await EmployeeReferenceChecks.GetEmployeeAsync(db, request.EmployeeId, ct);

        if (await db.LeaveRequests.AnyAsync(r => r.EmployeeId == employee.Id, ct))
            throw new ConflictException("This employee has leave history. Use Exit instead of Delete.");
        if (await db.Employees.AnyAsync(e => e.ManagerId == employee.Id, ct))
            throw new ConflictException("This employee manages other employees. Reassign them first.");
        if (await db.Departments.AnyAsync(d => d.HeadEmployeeId == employee.Id, ct))
            throw new ConflictException("This employee is a department head. Assign a new head first.");

        db.Employees.Remove(employee);   // AppDbContext isay soft delete bana deta hai
        await db.SaveChangesAsync(ct);
    }
}
