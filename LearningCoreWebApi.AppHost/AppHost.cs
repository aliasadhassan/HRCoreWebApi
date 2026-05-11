using Projects;

var builder = DistributedApplication.CreateBuilder(args);

// kv-hr-project-ali-hassan is the name of the Key Vault that I have created in Azure for this project.
// You should create your own Key Vault in Azure and use its name here.
// Make sure to replace it with the actual name of your Key Vault in Azure.
// The URL should be in the format "https://{your-key-vault-name}.vault.azure.net/".
var vaultUri = "https://kv-hr-project-ali-hassan.vault.azure.net/";

// Redis container register karein
var redis = builder.AddRedis("redis");

// 1. Teeno Base Microservices ko register karein
var identity = builder.AddProject<HR_Identity_API>("hr-identity")
                      .WithEnvironment("VaultUri", vaultUri);
var employee = builder.AddProject<HR_Employee_API>("hr-employee")
                      .WithEnvironment("VaultUri", vaultUri)
                      .WithReference(redis);
var payroll = builder.AddProject<HR_Payroll_API>("hr-payroll")
                      .WithEnvironment("VaultUri", vaultUri)
                      .WithReference(redis);

// 2. Gateway ko batayein ke wo in teeno se baat kar sakta hai
builder.AddProject<HR_Gateway>("hr-gateway")
       .WithReference(identity)
       .WithReference(employee)
       .WithReference(payroll)
       .WithEnvironment("VaultUri", vaultUri); // YEH LINE ZAROORI HAI

builder.Build().Run();

