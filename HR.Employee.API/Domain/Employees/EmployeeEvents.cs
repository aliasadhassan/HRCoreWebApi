namespace HR.Employee.API.Domain.Employees;

using HR.Employee.API.Domain.Common;

// Event poori entity le kar jata hai: dispatch SaveChanges mein Add ke baad hota hai, is liye Id tab tak mil chuki hoti hai.
public sealed record EmployeeCreatedDomainEvent(Employee Employee) : IDomainEvent;

public sealed record EmployeeExitedDomainEvent(Employee Employee) : IDomainEvent;
