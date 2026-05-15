using HR.Shared.Library.Helpers;
using Microsoft.IdentityModel.Tokens; // Ye top par hona chahiye
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Ocelot.Provider.Polly;
using Ocelot.QualityOfService;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var vaultUri = builder.Configuration["VaultUri"];
var kvHelper = new KeyVaultHelper(vaultUri!);
var jwtKey = await kvHelper.GetSecretValueAsync("JwtKey");

builder.AddServiceDefaults();

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin", policy => // Policy ka naam change kiya hai
    {
        policy.WithOrigins("http://localhost:4200") // Yahan apna Angular project ka URL likhein
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Yeh sab se zaroori hai cookies (credentials) ke liye
    });
});
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 1. JWT Authentication Setup (Ocelot ko batana ke token kaise check karna hai)
builder.Services.AddAuthentication()
    .AddJwtBearer("ApiGatewayKey", options => // "ApiGatewayKey" ocelot.json se match karna chahiye
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = "HR.Identity.API", // Wahi jo Identity API mein hai
            ValidateAudience = true,
            ValidAudience = "HR.Microservices",
            ValidateLifetime = true
        };
    });

// 2. Authorization Policy Definition
builder.Services.AddAuthorization(options => {
    // Ye 'AdminOnly' policy tab pass hogi jab token mein Role 'Admin' hoga
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

// 1. Ocelot Configuration standard tarike se load karein
// Environment ke mutabiq ocelot file load karein
builder.Configuration
       .AddJsonFile("ocelot.json", optional: false, reloadOnChange: true)
       .AddJsonFile($"ocelot.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);


// 2. Ocelot ko Polly aur dynamic service discovery features ke sath register karein
builder.Services.AddOcelot(builder.Configuration)
                .AddPolly(); // Yeh provider integration dependencies register karega or internal handler ports ko runtime par khud manage karega

var app = builder.Build();

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
app.UseAuthentication(); // Ye line JWT Authentication ko active kar degi
app.UseAuthorization();
await app.UseOcelot(); // Ye line Ocelot ko active kar degi 

// Ocelot middleware ko sab se last mein rakhna chahiye, kyunki Ocelot apne internal processing ke baad hi request ko aage forward karta hai.
// Pipeline Completion: Ocelot chahta hai ke jab tak uska apna internal system (routes ki checking wagaira)
// poora set na ho jaye, tab tak request processing shuru na ho.

app.MapControllers();
app.MapDefaultEndpoints();

app.Run();
