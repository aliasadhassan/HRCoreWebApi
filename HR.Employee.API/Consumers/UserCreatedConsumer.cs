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

            // Yahan hum Employee DB mein naya record dal rahe hain
            var newEmployee = new Models.Employees
            {
                Username = data.Username,
                Email = data.Email,
                // Baqi fields default ya empty rakh saktay hain filhal
                Cnic = "PENDING",
                Country = "Pakistan",
                City = "TBD",
                Address = "TBD",
                ContactNo = "000",
                CreatedDate = DateTime.UtcNow
            };

            _context.Employees.Add(newEmployee);
            await _context.SaveChangesAsync();

            // Console par print karein taake confirm ho jaye
            Console.WriteLine($"[RabbitMQ] User Created Event Received: {data.Username}");
        }
    }
}
