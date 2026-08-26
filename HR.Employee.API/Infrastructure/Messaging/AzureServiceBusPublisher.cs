using Azure.Messaging.ServiceBus;
using System.Text.Json;
using HR.Employee.API.Domain.Interfaces;

namespace HR.Employee.API.Infrastructure.Messaging
{
    public sealed class AzureServiceBusPublisher : IMessagePublisher
    {
        private readonly ServiceBusClient _serviceBusClient;

        public AzureServiceBusPublisher(ServiceBusClient serviceBusClient)
        {
            _serviceBusClient = serviceBusClient;
        }

        public async Task PublishAsync<T>(T message,string queueName,CancellationToken cancellationToken = default)
        {
            var sender = _serviceBusClient.CreateSender(queueName);
            var payload = JsonSerializer.Serialize(message);
            var serviceBusMessage = new ServiceBusMessage(payload);

            await sender.SendMessageAsync(serviceBusMessage,cancellationToken);
        }
    }
}
