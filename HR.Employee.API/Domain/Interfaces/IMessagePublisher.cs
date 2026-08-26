namespace HR.Employee.API.Domain.Interfaces
{
    public interface IMessagePublisher
    {
        Task PublishAsync<T>(T message, string queueName, CancellationToken cancellationToken = default);
    }
}
