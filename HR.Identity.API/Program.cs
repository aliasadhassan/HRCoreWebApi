using HR.Identity.API.Configuration;
using HR.Identity.API.Data;
using HR.Shared.Library.Helpers;
using HR.Identity.API.Middleware;
using HR.Identity.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddMassTransit(x =>
{
    // Program.cs mein MassTransit configuration update karen
    x.UsingRabbitMq((context, cfg) =>
    {
        // Aspire se connection string uthane ke liye ye best tareeqa hai
        var configuration = context.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString("messaging");

        if (!string.IsNullOrEmpty(connectionString))
        {
            cfg.Host(connectionString);
        }
        else
        {
            // Fallback agar Aspire ke bahar chala rahe hon
            cfg.Host("localhost", "/");
        }

        cfg.ConfigureEndpoints(context);
    });
});


// Key Vault integration
builder.Services.AddHRKeyVault(builder.Configuration);

// 1. Pehle KeyVaultHelper ka instance banayein (Ya DI se nikaalein)
var vaultUri = builder.Configuration["VaultUri"];
var kvHelper = new KeyVaultHelper(vaultUri!);

// 2. Startup ke waqt hi Connection String fetch karein
var connectionString = await kvHelper.GetSecretValueAsync("IdentityDbConn");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString,
    sqlServerOptionsAction: sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure();
    }));

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin", policy => // Policy ka naam change kiya hai
    {
        policy.WithOrigins("http://localhost:4200") // Yahan apna Angular project ka URL likhein
              .AllowAnyHeader()
              .AllowAnyMethod();
        //.AllowCredentials(); // Yeh sab se zaroori hai cookies (credentials) ke liye
    });
});

#region Email and Auth Settings Configuration
// 1. Email Settings ko inject karein (Agar aap EmailService ko IOptions pattern pe migrate karte hain)
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

// 2. Auth Settings configure karein
builder.Services.Configure<AuthSettings>(builder.Configuration.GetSection("AuthSettings"));

// 3. EmailService ko register karein (Zaroori hai)
builder.Services.AddScoped<IEmailService, EmailService>();

// 4. Settings ko read karein
var authSettings = builder.Configuration.GetSection("AuthSettings").Get<AuthSettings>()!;

// 5. Identity ki token lifespan configure karein
builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    // Yahan hum aapki class se hours le kar TimeSpan mein convert kar rahe hain
    options.TokenLifespan = TimeSpan.FromHours(authSettings.ResetPasswordTokenLifespanHours);
});

builder.Services.AddTransient<EmailTemplatesHelper>();
#endregion

#region Serilog Configuration
// Configure Serilog
builder.Host.UseSerilog((context, services, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration)
                 .ReadFrom.Services(services)
                 .Enrich.FromLogContext()
);
#endregion

#region JWT Authentication Configuration (Powered by Azure Key Vault)

// 1. Key Vault se JWT Secret fetch karein
var jwtKeyFromVault = await kvHelper.GetSecretValueAsync("JwtKey");

if (string.IsNullOrEmpty(jwtKeyFromVault))
{
    throw new Exception("JWT Key 'JwtKey' not found in Azure Key Vault.");
}

// 2. JWT Authentication setup
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            // Issuer aur Audience abhi bhi appsettings se aa sakte hain (kyunke ye sensitive nahi hain)
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],

            // Ab hum Key Vault wali key use kar rahe hain
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKeyFromVault)
            ),

            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddScoped<JwtTokenHelper>();

#endregion

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();



builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails(); // Default error structure ke liye

var app = builder.Build();

app.UseExceptionHandler();          //  Global exception handler

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseRouting();                   // 1. Routing sab se pehle
app.UseCors("AllowSpecificOrigin"); // 2. CORS Routing ke baad
app.UseAuthentication();            // 3. Pehle Authentication (Identity check karna)
app.UseAuthorization();             // 4. Phir Authorization (Policies/Roles check karna, [AllowAnonymous] ko handle karta hai)
app.UseJwtHeaderMiddleware();       // 5. Custom Middleware ko Authorization ke BAAD rakhein,Taa ke woh standard auth checks ke baad chale aur [AllowAnonymous] routes ko disturb na kare.
app.UseSerilogRequestLogging();     // 6. Logging middleware (position theek hai)
app.MapControllers();               // 7. Endpoints map karna
app.MapDefaultEndpoints();          // 8. Default endpoints map karna (Health checks wagaira ke liye)
app.Run();
