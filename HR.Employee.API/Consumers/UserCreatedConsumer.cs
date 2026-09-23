using HR.Employee.API.Infrastructure.Persistence;
using HR.Employee.API.Application.Employees.DTOs;
using HR.Shared.Library.Events;
using MassTransit;

namespace HR.Employee.API.Consumers
{
    public class UserCreatedConsumer : IConsumer<UserCreatedEvent>
    {
        private readonly AppDbContext _context;

        public UserCreatedConsumer(AppDbContext context)
        {
            _context = context;
        }

        public async Task Consume(ConsumeContext<UserCreatedEvent> context)
        {
            var data = context.Message;

            // Sahi class name instantiation
            var newEmployee = new EmployeeDto
            {
                Username = data.DisplayName,
                Email = data.Email,
                Cnic = "PENDING",
                Country = "Pakistan",
                City = "TBD",
                Address = "TBD",
                ContactNo = "000",
                CreatedDate = DateTime.UtcNow
            };

            _context.Employees.Add(newEmployee);
            await _context.SaveChangesAsync();

            Console.WriteLine($"[RabbitMQ] User Created Event Received: {data.DisplayName}");
        }
    }
}
