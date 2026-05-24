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
        // 1. Transaction shuru karein
        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var domainEntities = _context.ChangeTracker
                .Entries<IHasDomainEvents>()
                .Where(x => x.Entity.DomainEvents.Any()).ToList();

            var domainEvents = domainEntities.SelectMany(x => x.Entity.DomainEvents).ToList();

            foreach (var entry in domainEntities) entry.Entity.ClearDomainEvents();

            // 2. Pehle DB mein save karein (abhi save nahi hua, pipeline mein hai)
            var result = await _context.SaveChangesAsync(cancellationToken);

            // 3. MediatR events chalayein (In-Memory)
            foreach (var domainEvent in domainEvents)
            {
                await _publisher.Publish(domainEvent, cancellationToken);
            }

            // 4. Agar sab theek raha to database aur events dono ko ek sath commit karein
            await transaction.CommitAsync(cancellationToken);

            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
