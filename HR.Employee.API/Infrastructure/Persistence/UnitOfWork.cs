using EmployeeEntity = HR.Employee.API.Domain.Entities.Employee;
using HR.Employee.API.Domain.Interfaces;
using MediatR;
using MassTransit; // RabbitMQ publish karne ke liye

namespace HR.Employee.API.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private readonly IPublisher _publisher; // MediatR publisher internal notify ke liye
    private readonly IPublishEndpoint _publishEndpoint; // MassTransit RabbitMQ integration

    public UnitOfWork(AppDbContext context, IPublisher publisher, IPublishEndpoint publishEndpoint)
    {
        _context = context;
        _publisher = publisher;
        _publishEndpoint = publishEndpoint;
    }
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // 1. Database mein real data save karna
        var result = await _context.SaveChangesAsync(cancellationToken);

        // 2. Un sab Entities ko dhoondna jin mein Domain Events majood hain
        var domainEntities = _context.ChangeTracker
            .Entries()
            .Where(x => x.Entity.GetType() == typeof(EmployeeEntity))
            .ToList();

        var domainEvents = domainEntities
            .Select(x => (EmployeeEntity)x.Entity)
            .Where(e => e.DomainEvents != null && e.DomainEvents.Any())
            .SelectMany(e => e.DomainEvents)
            .ToList();

        // 3. Events ko iterate kar ke internal aur external broadcast karna
        foreach (var domainEvent in domainEvents)
        {
            // Internal application handlers ko notify karna
            await _publisher.Publish(domainEvent, cancellationToken);

            // External Microservices (RabbitMQ) par push karna taake Payroll microservice ko mil sakay
            await _publishEndpoint.Publish(domainEvent, cancellationToken);
        }

        // 4. Events ko clear karna taake dubara duplicate trigger na hon
        foreach (var entry in domainEntities)
        {
            if (entry.Entity is EmployeeEntity employee)
            {
                employee.ClearDomainEvents();
            }
        }

        return result;
    }
}
