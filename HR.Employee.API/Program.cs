using HR.Employee.API;
using HR.Shared.Library.Helpers;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Key Vault integration
builder.Services.AddHRKeyVault(builder.Configuration);

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
