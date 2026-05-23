using HR.Employee.API.Domain.Common;
using HR.Employee.API.Domain.Events;
using MediatR;

namespace HR.Employee.API.Domain.Entities
{
    public sealed class Employee : IHasDomainEvents
    {
        // Properties ke setters 'private' ya 'init' honge taake bahar se koi direct change na kar sake
        public Guid Id { get; private set; }
        public string FirstName { get; private set; } = string.Empty;
        public string LastName { get; private set; } = string.Empty;
        public string Email { get; private set; } = string.Empty;
        public string Department { get; private set; } = string.Empty;
        public decimal Salary { get; private set; }
        public bool IsActive { get; private set; }
        public DateTime CreatedAt { get; private set; }

        // EF Core ke liye private constructor zaroori hai
        private Employee() { }

        private readonly List<INotification> _domainEvents = new();
        public IReadOnlyCollection<INotification> DomainEvents => _domainEvents.AsReadOnly();
        public void AddDomainEvent(INotification domainEvent)
        {
            _domainEvents.Add(domainEvent);
        }
        public void ClearDomainEvents()
        {
            _domainEvents.Clear();
        }

        // Factory Method: Naya employee hamesha is method ke ziye create hoga
        public static Employee Create(string firstName, string lastName, string email, string department, decimal salary)
        {
            if (string.IsNullOrWhiteSpace(firstName)) throw new ArgumentException("First name cannot be empty.");
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) throw new ArgumentException("Invalid email address.");
            if (salary < 0) throw new ArgumentException("Salary cannot be negative.");

            var employee = new Employee
            {
                Id = Guid.NewGuid(),
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                Department = department,
                Salary = salary,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            // Event register karna
            employee.AddDomainEvent(new EmployeeCreatedEvent(employee.Id, employee.Email, employee.Department, employee.Salary));

            return employee;
        }

        // Business Logic Method: Agar salary barhani ho, to sirf ye method call hoga
        public void UpdateSalary(decimal newSalary)
        {
            if (newSalary < 0) throw new ArgumentException("Salary cannot be negative.");
            Salary = newSalary;
        }

        // Business Logic Method: Employee ko terminate karne ke liye
        public void Deactivate()
        {
            IsActive = false;
        }
    }

}
