using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SaloonApp.API.Data;
using SaloonApp.API.Repositories;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
// Debugging Type Existence
var types = System.Reflection.Assembly.GetExecutingAssembly().GetTypes().Where(t => t.Name.Contains("ShopSearchResult"));
foreach (var t in types) Console.WriteLine($"[DEBUG-TYPE] Found Type: {t.FullName}");
builder.Services.AddSwaggerGen(c => 
{
    c.MapType<TimeSpan?>(() => new Microsoft.OpenApi.Models.OpenApiSchema { Type = "string", Format = "time" });
    c.MapType<TimeSpan>(() => new Microsoft.OpenApi.Models.OpenApiSchema { Type = "string", Format = "time" });
});

// Database Configuration
builder.Services.AddScoped<DatabaseContext>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<ShopRepository>();
builder.Services.AddScoped<QueueRepository>();
builder.Services.AddScoped<ReviewRepository>();
builder.Services.AddTransient<DatabaseInitializer>();

// Authentication Configuration
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["Secret"] ?? "TemporarySecretKeyMustBeLongEnough";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
// Always enable Swagger for this MVP to ensure visibility
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        Console.WriteLine("CRITICAL EXCEPTION: " + ex.ToString());
        throw;
    }
});

app.UseSwagger();
app.UseSwaggerUI();

if (app.Environment.IsDevelopment())
{
    // app.UseSwagger(); // Moved out
    // app.UseSwaggerUI(); // Moved out
}

app.UseCors("AllowAll");

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Redirect root to Swagger
app.MapGet("/", async context =>
{
    context.Response.Redirect("/swagger");
    await Task.CompletedTask;
});

// Initialize Database
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    initializer.Initialize();
}

app.Run();
