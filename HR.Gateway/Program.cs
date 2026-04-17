using Microsoft.IdentityModel.Tokens; // Ye top par hona chahiye
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
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
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 1. JWT Authentication Setup (Ocelot ko batana ke token kaise check karna hai)
builder.Services.AddAuthentication()
    .AddJwtBearer("ApiGatewayKey", options => {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "AapkaIssuerName", // Identity API wala hona chahiye
            ValidAudience = "AapkaAudienceName", // Identity API wala hona chahiye
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("Aapki_Secret_Key_Jo_Min_16_Chars_Ho"))
        };
    });

// 2. Authorization Policy Definition
builder.Services.AddAuthorization(options => {
    // Ye 'AdminOnly' policy tab pass hogi jab token mein Role 'Admin' hoga
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);
builder.Services.AddOcelot(builder.Configuration);

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

app.MapControllers();

app.Run();
