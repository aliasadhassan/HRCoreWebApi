using EmployeeEntity = HR.Employee.API.Domain.Entities.Employee;
using HR.Employee.API.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HR.Employee.API.Infrastructure.Persistence;

public sealed class EmployeeRepository : IEmployeeRepository
{
    private readonly AppDbContext _context; // Aap ka existing DbContext

    public EmployeeRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<EmployeeEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<EmployeeEntity>()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<EmployeeEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Set<EmployeeEntity>()
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(EmployeeEntity employee, CancellationToken cancellationToken = default)
    {
        await _context.Set<EmployeeEntity>().AddAsync(employee, cancellationToken);
        // Note: SaveChanges yahan nahi call hoga, wo Unit of Work ya direct Controller/Handler level par sync hota hai
    }

    public void Update(EmployeeEntity employee)
    {
        _context.Set<EmployeeEntity>().Update(employee);
    }

    public void Delete(EmployeeEntity employee)
    {
        _context.Set<EmployeeEntity>().Remove(employee);
    }
}
