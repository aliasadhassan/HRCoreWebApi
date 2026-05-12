using HR.Employee.API;
using HR.Shared.Library.Helpers;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using HR.Employee.API.Consumers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

#region RabbitMQ
// Employee API Program.cs
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<UserCreatedConsumer>(); // Consumer ko register rakhen

    x.UsingRabbitMq((context, cfg) =>
    {
        var configuration = context.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString("messaging");

        cfg.Host(connectionString); // Aspire ki port yahan use hogi

        // Queue configuration
        cfg.ReceiveEndpoint("user-created-queue", e =>
        {
            e.ConfigureConsumer<UserCreatedConsumer>(context);
        });
    });
});

#endregion

#region redis
builder.Services.AddMemoryCache(); // add memory cache redis k liye isko use krna xruri ha i.e. L1 cache
#endregion

// Key Vault integration
builder.Services.AddHRKeyVault(builder.Configuration);

builder.AddRedisClient("redis");
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

#region Azure Key Vault (AKV)
// 1. Pehle KeyVaultHelper ka instance banayein (Ya DI se nikaalein)
var vaultUri = builder.Configuration["VaultUri"];
var kvHelper = new KeyVaultHelper(vaultUri!);

// 2. Startup ke waqt hi Connection String fetch karein
var connectionString = await kvHelper.GetSecretValueAsync("EmployeeDbConn");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString,
    sqlServerOptionsAction: sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure();
    }));
#endregion

// 1. Pehle ye (builder ke sath)
builder.AddServiceDefaults();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// 2. Phir ye (app ke sath)
app.MapDefaultEndpoints();

app.Run();
