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
              .AllowAnyMethod()
              .AllowCredentials(); // ye cookies k liye xruri ha cross domain travel k liye
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
builder.Services.AddHttpClient<MicrosoftGraphService>();
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

// 1. Logging ko upar rakh sakte hain taake saari requests track hon
app.UseSerilogRequestLogging();

// 2. Routing hamesha CORS se PEHLE aani chahiye
app.UseRouting();

// 3. CORS ko Routing ke BAAD aur Authentication se PEHLE lagayein
app.UseCors("AllowSpecificOrigin");

// 4. Pehle Identity check karein ke Token ya Cookie valid hai ya nahi
app.UseAuthentication();

// 5. Phir check karein ke user ko permission hai ya nahi
app.UseAuthorization();

// 6. Custom Middleware authorization ke baad
app.UseJwtHeaderMiddleware();

// 7. Endpoints map karein
app.MapControllers();
app.MapDefaultEndpoints();

app.Run();


