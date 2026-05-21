using EmployeeEntity = HR.Employee.API.Domain.Entities.Employee;

namespace HR.Employee.API.Domain.Interfaces;

public interface IEmployeeRepository
{
    Task<EmployeeEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmployeeEntity>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(EmployeeEntity employee, CancellationToken cancellationToken = default);
    void Update(EmployeeEntity employee);
    void Delete(EmployeeEntity employee);
}
