using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var vaultUri = "https://kv-hr-project-ali-hassan.vault.azure.net/";

var rabbitUser = builder.AddParameter("rabbit-user");
var rabbitPass = builder.AddParameter("rabbit-password", secret: true);

var messaging = builder.AddRabbitMQ("messaging", userName: rabbitUser, password: rabbitPass)
                       .WithManagementPlugin(port: 15672);

var redis = builder.AddRedis("redis");

var identity = builder.AddProject<HR_Identity_API>("hr-identity")
                      .WithEnvironment("VaultUri", vaultUri)
                      .WithReference(messaging)
                      .WaitFor(messaging);

var employee = builder.AddProject<HR_Employee_API>("hr-employee")
                      .WithEnvironment("VaultUri", vaultUri)
                      .WithReference(redis)
                      .WithReference(messaging)
                      .WaitFor(redis)
                      .WaitFor(messaging);

var payroll = builder.AddProject<HR_Payroll_API>("hr-payroll")
                     .WithEnvironment("VaultUri", vaultUri)
                     .WithReference(redis)
                     .WithReference(messaging)
                     .WaitFor(redis)
                     .WaitFor(messaging);

builder.AddProject<HR_Gateway>("hr-gateway")
       .WithEnvironment("VaultUri", vaultUri)
       .WithReference(identity)
       .WithReference(employee)
       .WithReference(payroll)
       .WaitFor(identity)
       .WaitFor(employee)
       .WaitFor(payroll);

builder.Build().Run();