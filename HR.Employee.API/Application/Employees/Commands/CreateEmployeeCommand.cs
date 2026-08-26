using EmployeeEntity = HR.Employee.API.Domain.Entities.Employee;
using HR.Employee.API.Domain.Interfaces;
using MediatR;

namespace HR.Employee.API.Application.Employees.Commands;

// .NET 8 Record - Requests ke liye data transfer object (DTO) ka kaam karega

public sealed record CreateEmployeeCommand(
    string FirstName,
    string LastName,
    string Email,
    string Department,
    decimal Salary
) : IRequest<Guid>;


public sealed class CreateEmployeeCommandHandler: IRequestHandler<CreateEmployeeCommand, Guid>
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateEmployeeCommandHandler(IEmployeeRepository employeeRepository,IUnitOfWork unitOfWork)
    {
        _employeeRepository = employeeRepository;
        _unitOfWork = unitOfWork;
    }
    public async Task<Guid> Handle(CreateEmployeeCommand request,CancellationToken cancellationToken)
    {
        var employee = EmployeeEntity.Create(
            request.FirstName,
            request.LastName,
            request.Email,
            request.Department,
            request.Salary
        );

        await _employeeRepository.AddAsync(employee,cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return employee.Id;
    }
}

