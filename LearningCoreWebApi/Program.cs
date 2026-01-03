using LearningCoreWebApi.Configuration;
using LearningCoreWebApi.Data;
using LearningCoreWebApi.Helpers;
using LearningCoreWebApi.Middleware;
using LearningCoreWebApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

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
#endregion

#region Serilog Configuration
// Configure Serilog
builder.Host.UseSerilog((context, services, configuration) => 
    configuration.ReadFrom.Configuration(context.Configuration)
                 .ReadFrom.Services(services)
                 .Enrich.FromLogContext()
);
#endregion

#region JWT Authentication Configuration

var jwtKey = builder.Configuration["Jwt:Key"];

if (string.IsNullOrEmpty(jwtKey))
{
    throw new Exception("JWT Key is missing in appsettings.json");
}

// adding JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)
            ),

            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddScoped<JwtTokenHelper>();
#endregion

builder.Services.AddTransient<EmailTemplatesHelper>();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));

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

app.UseHttpsRedirection();
app.UseRouting();                   //  Pehle Routing
app.UseCors("AllowSpecificOrigin"); //  Phir CORS
app.UseAuthentication();            //  JWT validation
app.UseJwtHeaderMiddleware();       //  custom guard
app.UseSerilogRequestLogging();     //  Serilog request logging
app.UseAuthorization();             //  role/claims
app.MapControllers();

app.Run();
