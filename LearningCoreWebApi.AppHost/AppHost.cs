using Projects;

var builder = DistributedApplication.CreateBuilder(args);

// 1. Teeno Base Microservices ko register karein
var identity = builder.AddProject<HR_Identity_API>("hr-identity");
var employee = builder.AddProject<HR_Employee_API>("hr-employee");
var payroll = builder.AddProject<HR_Payroll_API>("hr-payroll");

// 2. Gateway ko batayein ke wo in teeno se baat kar sakta hai
builder.AddProject<HR_Gateway>("hr-gateway")
       .WithReference(identity)
       .WithReference(employee)
       .WithReference(payroll);

builder.Build().Run();
