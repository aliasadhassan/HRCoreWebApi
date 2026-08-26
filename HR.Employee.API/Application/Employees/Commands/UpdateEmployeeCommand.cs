using HR.Employee.API.Domain.Interfaces;
using MediatR;

namespace HR.Employee.API.Application.Employees.Commands
{
    public sealed record UpdateEmployeeCommand(
       Guid Id,
       string FirstName,
       string LastName,
       string Email,
       string Department,
       decimal Salary
    ) : IRequest;
    
    // IRequest ka matlab hai ke ye command MediatR ke zariye handle ki jayegi aur is ka koi return value nahi hoga (void).
    public sealed class UpdateEmployeeCommandHandler: IRequestHandler<UpdateEmployeeCommand>
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IUnitOfWork _unitOfWork;

        public UpdateEmployeeCommandHandler(IEmployeeRepository employeeRepository,IUnitOfWork unitOfWork)
        {
            _employeeRepository = employeeRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(UpdateEmployeeCommand request,CancellationToken cancellationToken)
        {
            var employee = await _employeeRepository.GetByIdAsync(request.Id,cancellationToken);

            if (employee is null){
                throw new KeyNotFoundException($"Employee with ID '{request.Id}' was not found.");
            }

            employee.UpdateDetails(
                request.FirstName,
                request.LastName,
                request.Email,
                request.Department,
                request.Salary
            );

            _employeeRepository.Update(employee);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
