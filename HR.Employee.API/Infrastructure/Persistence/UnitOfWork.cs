using EmployeeEntity = HR.Employee.API.Domain.Entities.Employee;
using HR.Employee.API.Domain.Interfaces;
using MediatR;
using MassTransit;
using HR.Employee.API.Domain.Common;

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
        // 1. Un sab Entities ko dhoondna jin mein Domain Events majood hain
        var domainEntities = _context.ChangeTracker
            .Entries<IHasDomainEvents>()
            .Where(x => x.Entity.DomainEvents.Any())
            .ToList();

        // 2. Extract all accumulated events into a flat list
        var domainEvents = domainEntities
            .SelectMany(x => x.Entity.DomainEvents)
            .ToList();

        // 3. Clear events immediately to guarantee idempotency and prevent infinite loops
        foreach (var entry in domainEntities)
        {
            entry.Entity.ClearDomainEvents();
        }

        // 4. Pehle Data DB mein commit karein
        var result = await _context.SaveChangesAsync(cancellationToken);

        // 5. Database save success hone ke BAAD events publish karein
        foreach (var domainEvent in domainEvents)
        {
            await _publisher.Publish(domainEvent, cancellationToken);
            await _publishEndpoint.Publish(domainEvent, cancellationToken);
        }

        // 6. Total affected rows return karein
        return result;
    }
}
