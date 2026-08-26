using HR.Employee.API.Domain.Interfaces;
using MediatR;

namespace HR.Employee.API.Application.Employees.Commands
{
    public sealed record DeleteEmployeeCommand(Guid Id) : IRequest;
    public sealed class DeleteEmployeeCommandHandler : IRequestHandler<DeleteEmployeeCommand>
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IUnitOfWork _unitOfWork;

        public DeleteEmployeeCommandHandler(IEmployeeRepository employeeRepository,IUnitOfWork unitOfWork)
        {
            _employeeRepository = employeeRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(DeleteEmployeeCommand request, CancellationToken cancellationToken)
        {
            var employee = await _employeeRepository.GetByIdAsync(request.Id,cancellationToken);

            if (employee is null)
            {
                throw new KeyNotFoundException($"Employee with ID '{request.Id}' was not found.");
            }

            _employeeRepository.Delete(employee);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
