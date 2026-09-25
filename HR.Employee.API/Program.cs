using System.Text;
using System.Text.Json.Serialization;
using FluentValidation;
using HR.Employee.API.Application.Common.Behaviors;
using HR.Employee.API.Application.Common.Interfaces;
using HR.Employee.API.Consumers;
using HR.Employee.API.Infrastructure.Identity;
using HR.Employee.API.Infrastructure.Logging;
using HR.Employee.API.Infrastructure.Persistence;
using HR.Shared.Library.Helpers;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

#region Azure Key Vault
var kvHelper = new KeyVaultHelper(builder.Configuration["VaultUri"]!);
builder.Services.AddHRKeyVault(builder.Configuration);

var jwtKey = await kvHelper.GetSecretValueAsync("JwtKey");
if (string.IsNullOrEmpty(jwtKey))
    throw new Exception("JWT Key 'JwtKey' not found in Azure Key Vault.");

var connectionString = await kvHelper.GetSecretValueAsync("EmployeeDbConn");
#endregion

#region API + exception handling
builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));   // Angular ko "Active", 1 nahi

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
#endregion

#region Current user + persistence
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));
builder.Services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
#endregion

#region MediatR + FluentValidation
var applicationAssembly = typeof(Program).Assembly;
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(applicationAssembly);
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});
builder.Services.AddValidatorsFromAssembly(applicationAssembly);
#endregion

#region RabbitMQ (MassTransit + EF Outbox/Inbox)
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<UserCreatedConsumer>();

    // Outbox: publish DB ke saath ek hi transaction mein (dual-write khatam)
    x.AddEntityFrameworkOutbox<AppDbContext>(o =>
    {
        o.UseSqlServer();
        o.UseBusOutbox();
    });

    // Inbox: same message dobara aaye to consumer dobara nahi chalega
    x.AddConfigureEndpointsCallback((context, _, cfg) => cfg.UseEntityFrameworkOutbox<AppDbContext>(context));

    x.UsingRabbitMq((context, cfg) =>
    {
        var messaging = context.GetRequiredService<IConfiguration>().GetConnectionString("messaging");
        if (!string.IsNullOrWhiteSpace(messaging))
            cfg.Host(new Uri(messaging));
        else
            cfg.Host("localhost", "/");

        cfg.ConfigureEndpoints(context);
    });
});
#endregion

#region Redis (reference-data cache ke liye aage use hoga — tenant-prefixed keys)
builder.AddRedisClient("redis");
#endregion

#region JWT (defense in depth: Gateway ke baad service bhi token check kare)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();
#endregion

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Dev: pending migrations khud apply
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapDefaultEndpoints();

app.Run();
