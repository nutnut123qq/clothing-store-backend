using Microsoft.EntityFrameworkCore;
using ClothingStore.API.Data;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using ClothingStore.API.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register JwtService
builder.Services.AddSingleton<JwtService>();

// Configure Authentication (JWT)
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? builder.Configuration.GetValue<string>("Jwt:Secret");
if (!string.IsNullOrEmpty(jwtSecret))
{
    var key = Encoding.UTF8.GetBytes(jwtSecret);
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };
    });
}

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ClothingStoreContext>();

// Add Security Headers
builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});

// Add Entity Framework
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL") 
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

Console.WriteLine($"[Startup] Environment: {builder.Environment.EnvironmentName}");
Console.WriteLine($"[Startup] DATABASE_URL exists: {!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DATABASE_URL"))}");

// Debug: Show partial connection string (hide password)
if (!string.IsNullOrEmpty(connectionString))
{
    var debugConnStr = connectionString.Length > 50 
        ? connectionString.Substring(0, 30) + "..." 
        : connectionString;
    Console.WriteLine($"[Startup] Connection string preview: {debugConnStr}");
}

// Handle Render PostgreSQL URL format (postgres://...) and convert to Npgsql format
if (!string.IsNullOrEmpty(connectionString))
{
    if (connectionString.StartsWith("postgres://") || connectionString.StartsWith("postgresql://"))
    {
        try
        {
            var uri = new Uri(connectionString);
            
            // Validate and extract components
            if (string.IsNullOrEmpty(uri.Host))
            {
                throw new InvalidOperationException("Host is missing in DATABASE_URL");
            }
            
            var userInfo = uri.UserInfo?.Split(':') ?? Array.Empty<string>();
            if (userInfo.Length < 2 || string.IsNullOrEmpty(userInfo[0]) || string.IsNullOrEmpty(userInfo[1]))
            {
                throw new InvalidOperationException("Username or password is missing in DATABASE_URL. Format: postgres://username:password@host:port/database");
            }
            
            // Default port to 5432 if not specified
            var port = uri.Port > 0 ? uri.Port : 5432;
            var database = uri.AbsolutePath.TrimStart('/');
            
            if (string.IsNullOrEmpty(database))
            {
                throw new InvalidOperationException("Database name is missing in DATABASE_URL");
            }
            
            // URL decode username and password in case they contain special characters
            var username = Uri.UnescapeDataString(userInfo[0]);
            var password = Uri.UnescapeDataString(userInfo[1]);
            
            connectionString = $"Host={uri.Host};Port={port};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true";
            Console.WriteLine($"[Startup] ✓ Converted postgres:// URL to Npgsql format");
            Console.WriteLine($"[Startup] ✓ Host: {uri.Host}, Port: {port}, Database: {database}, Username: {username}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Startup ERROR] ✗ Failed to parse DATABASE_URL: {ex.Message}");
            Console.WriteLine($"[Startup ERROR] Expected format: postgres://username:password@host:port/database");
            Console.WriteLine($"[Startup ERROR] Example: postgres://myuser:mypass@dpg-abc123.oregon-postgres.render.com:5432/mydb");
            throw new InvalidOperationException("Invalid DATABASE_URL format. " + ex.Message, ex);
        }
    }
    else
    {
        Console.WriteLine($"[Startup] Using direct Npgsql connection string format");
    }
}
else
{
    Console.WriteLine($"[Startup ERROR] No connection string found!");
    throw new InvalidOperationException("Database connection string is not configured. Please set DATABASE_URL environment variable on Render.");
}

builder.Services.AddDbContext<ClothingStoreContext>(options =>
    options.UseNpgsql(connectionString));

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
            ?? new[] { 
                "http://localhost:3000", 
                "https://localhost:3000",
                "https://clothing-store-frontend-six.vercel.app"
            };
        
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .WithExposedHeaders("*");
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Configure HTTPS redirection based on environment
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowFrontend");

// Add Security Headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});

app.UseAuthentication();
app.UseAuthorization();

// Map Health Check endpoint
app.MapHealthChecks("/health");

app.MapControllers();

// Auto-run migrations on startup (for production deployment)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ClothingStoreContext>();
        Console.WriteLine("[Migration] Applying pending migrations...");
        context.Database.Migrate();
        Console.WriteLine("[Migration] ✓ Migrations applied successfully!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Migration ERROR] ✗ Failed to apply migrations: {ex.Message}");
        // Don't throw - allow app to start even if migration fails
        // You can change this behavior if you want app to fail on migration error
    }
}

app.Run();
