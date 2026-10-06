using System.Text;
using System.Text.Json.Serialization;
using FluentValidation;
using HR.Payroll.API.Application.Common.Behaviors;
using HR.Payroll.API.Application.Common.Interfaces;
using HR.Payroll.API.Consumers;
using HR.Payroll.API.Infrastructure.Identity;
using HR.Payroll.API.Infrastructure.Logging;
using HR.Payroll.API.Infrastructure.Persistence;
using HR.Shared.Library.Authorization;
using HR.Shared.Library.Helpers;
using HR.Shared.Library.Persistence;
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

var connectionString = await kvHelper.GetDbConnectionStringAsync("PayrollDbConnectionString");
#endregion

#region API + exception handling
builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
#endregion

#region Current user + persistence
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

TenantSession.EnsureSessionPooling(connectionString);   // RLS app role + transaction pooler = tenant leak
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString, sql =>
    {
        sql.MigrationsHistoryTable("__EFMigrationsHistory", "payroll");
        sql.EnableRetryOnFailure();
    }));
builder.Services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
#endregion

#region MediatR + FluentValidation
var assembly = typeof(Program).Assembly;
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(assembly);
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});
builder.Services.AddValidatorsFromAssembly(assembly);
builder.Services.AddScoped<HR.Payroll.API.Application.Runs.PayrollRunCalculator>();
#endregion

#region RabbitMQ (MassTransit + EF Outbox/Inbox)
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<EmployeeCreatedConsumer>();
    x.AddConsumer<EmployeeExitedConsumer>();

    x.AddEntityFrameworkOutbox<AppDbContext>(o =>
    {
        o.UsePostgres(); // Supabase Postgres: SQL Server lock syntax (SELECT TOP 1 ... WITH (UPDLOCK)) yahan fail hota hai
        o.UseBusOutbox();
    });

    x.AddConfigureEndpointsCallback((context, _, cfg) =>
    {
        // Out-of-order events (Exited pehle, Created baad mein) — thori der baad dobara try
        cfg.UseMessageRetry(r => r.Intervals(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30)));
        cfg.UseEntityFrameworkOutbox<AppDbContext>(context);
    });

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

#region Redis (aage: tax regimes / components cache)
builder.AddRedisClient("redis");
#endregion

#region JWT
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
builder.Services.AddPermissionAuthorization();   // [HasPermission(...)] — token ke "perm" claims
#endregion

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    //using var scope = app.Services.CreateScope();
    //await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapDefaultEndpoints();

app.Run();
