using HR.Employee.API;
using FluentValidation;
using HR.Employee.API.Application.Common.Behaviors;
using HR.Employee.API.Consumers;
using HR.Employee.API.Domain.Interfaces;
using HR.Employee.API.Infrastructure.Logging;
using HR.Employee.API.Infrastructure.Persistence;
using HR.Employee.API.Presentation.Filters;
using HR.Shared.Library.Helpers;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// 1. Register Global Exception Filter inside MVC Controllers routing pipeline
builder.Services.AddControllers(options =>
{
    options.Filters.Add<GlobalExceptionFilter>();
});

// 2. Register the modern Core .NET 8 Exception Handler
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails(); // Generates metadata templates automatically

// 3. Domain Interfaces aur Infrastructure Persistence ki registration
builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Define reference assembly where your Handlers, Behaviors, and Validators live.
// If they are in the same project as Program, keep 'typeof(Program).Assembly'.
// If they are in an 'Application' class library, use 'typeof(CreateEmployeeCommand).Assembly'.
var applicationAssembly = typeof(Program).Assembly;

// 4. MediatR Registration with Automated Open Behavior Pipeline
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(applicationAssembly);

    // This hooks up your automated validation interceptor to the pipeline globally!
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});

// 5. Automated FluentValidation Assembly Scanning
// This automatically finds all classes inheriting from AbstractValidator<T>
builder.Services.AddValidatorsFromAssembly(applicationAssembly);


// Add services to the container.

#region RabbitMQ
// Employee API Program.cs
builder.Services.AddMassTransit(x =>
{
    // Consumer ko register karein
    x.AddConsumer<UserCreatedConsumer>();

    x.AddEntityFrameworkOutbox<AppDbContext>(o =>
    {
        o.UseSqlServer();
        o.UseBusOutbox();
    });

    x.UsingRabbitMq((context, cfg) =>
    {
        var configuration = context.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString("messaging");

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            cfg.Host(new Uri(connectionString));
        }
        else
        {
            cfg.Host("localhost", "/");
        }

        cfg.ConfigureEndpoints(context);
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

string connectionString;

// Agar VaultUri configured ha to AKV sy secret uthao
if (!string.IsNullOrWhiteSpace(vaultUri))
{
    var kvHelper = new KeyVaultHelper(vaultUri);
    connectionString = await kvHelper.GetSecretValueAsync("EmployeeDbConn");
}
else
{
    // Local development fallback
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
}

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
app.UseExceptionHandler();
app.MapControllers();

// 2. Phir ye (app ke sath)
app.MapDefaultEndpoints();

app.Run();
