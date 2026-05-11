using HR.Payroll.API.Models;
using HR.Shared.Library.Helpers;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults(); // isse hum apne microservice ke liye kuch default configurations set kar sakte hain (jaise ki CORS, logging, etc.)

// Key Vault integration
builder.Services.AddHRKeyVault(builder.Configuration);

builder.AddRedisClient("redis");

// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 1. Pehle KeyVaultHelper ka instance banayein (Ya DI se nikaalein)
var vaultUri = builder.Configuration["VaultUri"];
var kvHelper = new KeyVaultHelper(vaultUri!);

// 2. Startup ke waqt hi Connection String fetch karein
var connectionString = await kvHelper.GetSecretValueAsync("PayrollDbConn");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString,
    sqlServerOptionsAction: sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure();
    }));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();      // Endpoints map karna
app.MapDefaultEndpoints(); // Health checks wagaira ke liye

app.Run();
