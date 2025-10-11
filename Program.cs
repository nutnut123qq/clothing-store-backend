using Microsoft.EntityFrameworkCore;
using ClothingStore.API.Data;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Linq;
using ClothingStore.API.Services;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to bind to PORT environment variable for Render
var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(int.Parse(port));
});
Console.WriteLine($"[Startup] Configuring Kestrel to listen on port: {port}");

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

// Helper: sanitize Npgsql-style connection string by removing invalid values
string SanitizeConnectionString(string conn)
{
    if (string.IsNullOrEmpty(conn)) return conn;

    try
    {
        // Split by semicolon into key=value segments
        var parts = conn.Split(';', StringSplitOptions.RemoveEmptyEntries);
        var dict = parts
            .Select(p => p.Split('=', 2))
            .Where(a => a.Length == 2)
            .ToDictionary(a => a[0].Trim(), a => a[1].Trim(), StringComparer.OrdinalIgnoreCase);

        // Always remove any pool-related keys (max pool size, max_pool_size, max-pool-size, etc.)
        string NormalizeKey(string k)
        {
            if (k == null) return string.Empty;
            // URL-decode common encodings and remove non-alphanumeric characters
            var decoded = Uri.UnescapeDataString(k).Replace("+", "");
            return new string(decoded.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        }

        var removedKeys = new List<string>();
        foreach (var kv in dict.ToList())
        {
            var rawKey = kv.Key;
            var normalized = NormalizeKey(rawKey);

            // If key name looks like any variant of max pool size or pool size, remove it
            if (normalized.Contains("max") && normalized.Contains("pool") || normalized.Contains("poolsize") || normalized.Contains("maxpoolsize"))
            {
                dict.Remove(rawKey);
                removedKeys.Add(rawKey);
                continue;
            }

            // If value should be numeric (common for pool sizes/ports) but contains non-digits, remove the key to be safe
            var val = kv.Value?.Trim().Trim('"', '\'') ?? string.Empty;
            if (!string.IsNullOrEmpty(val) && val.Any(c => !char.IsDigit(c)))
            {
                // don't remove common textual values like Host, Database, Username, Password
                var textualKeys = new[] { "host", "database", "username", "password", "sslmode", "trustservercertificate" };
                if (!textualKeys.Contains(normalized))
                {
                    dict.Remove(rawKey);
                    removedKeys.Add(rawKey);
                }
            }
        }

        if (removedKeys.Count > 0)
        {
            // Log removed keys but mask values
            var masked = removedKeys.Select(k => k + "=<removed>").ToArray();
            Console.WriteLine($"[Startup] ✱ Removed problematic connection-string keys: {string.Join(',', masked)}");
        }

        // Rebuild connection string preserving original key order as much as possible
        var rebuilt = parts
            .Select(p => p.Split('=', 2))
            .Where(a => a.Length == 2)
            .Select(a => a[0].Trim())
            .Where(k => dict.ContainsKey(k))
            .Select(k => $"{k}={dict[k]}")
            .ToList();

        // Append any remaining keys that weren't in the original order
        var remaining = dict.Keys.Except(rebuilt.Select(s => s.Split('=', 2)[0]), StringComparer.OrdinalIgnoreCase);
        foreach (var k in remaining)
            rebuilt.Add($"{k}={dict[k]}");

        return string.Join(';', rebuilt) + (rebuilt.Count > 0 ? ";" : string.Empty);
    }
    catch
    {
        // If anything goes wrong, fall back to original string
        return conn;
    }
}

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
            var dbPort = uri.Port > 0 ? uri.Port : 5432;
            var database = uri.AbsolutePath.TrimStart('/');
            
            if (string.IsNullOrEmpty(database))
            {
                throw new InvalidOperationException("Database name is missing in DATABASE_URL");
            }
            
            // URL decode username and password in case they contain special characters
            var username = Uri.UnescapeDataString(userInfo[0]);
            var password = Uri.UnescapeDataString(userInfo[1]);
            
            connectionString = $"Host={uri.Host};Port={dbPort};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true;Timeout=300;Command Timeout=300;Keepalive=30;ConnectionIdleLifetime=300";
            Console.WriteLine($"[Startup] ✓ Converted postgres:// URL to Npgsql format");
            Console.WriteLine($"[Startup] ✓ Host: {uri.Host}, Port: {dbPort}, Database: {database}, Username: {username}");
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
        
        // Only sanitize if it's already in Npgsql format (not postgres:// URL)
        var sanitizedConnectionString = SanitizeConnectionString(connectionString);
        if (sanitizedConnectionString != connectionString)
        {
            Console.WriteLine("[Startup] ✱ DATABASE_URL was sanitized to remove invalid parameters (e.g. Max Pool Size)");
            connectionString = sanitizedConnectionString;
        }
    }
}
else
{
    Console.WriteLine($"[Startup ERROR] No connection string found!");
    throw new InvalidOperationException("Database connection string is not configured. Please set DATABASE_URL environment variable on Render.");
}

builder.Services.AddDbContext<ClothingStoreContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.CommandTimeout(300); // 5 minutes timeout for slow Supabase pooler
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorCodesToAdd: null
        );
    }));

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // Development - allow localhost
            policy.WithOrigins("http://localhost:3000", "https://localhost:3000")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
        else
        {
            // Production - allow all origins for now to fix CORS issue
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .WithExposedHeaders("X-Total-Count", "X-Page", "X-Page-Size");
        }
    });
});

var app = builder.Build();

// IMPORTANT: CORS must be the first middleware
app.UseCors("AllowFrontend");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Don't use HTTPS redirection on Render - it's handled by their proxy
// if (!app.Environment.IsDevelopment())
// {
//     app.UseHttpsRedirection();
// }

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

// Start the app first, then run migrations in background
var runTask = app.RunAsync();

// Run migrations in background task - don't block app startup
_ = Task.Run(async () =>
{
    await Task.Delay(2000); // Wait 2 seconds for app to start
    
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ClothingStoreContext>();
        Console.WriteLine("[Migration] Applying pending migrations (background task)...");

        // Connectivity probe: try opening a plain NpgsqlConnection to capture detailed errors
        try
        {
            using var probe = new NpgsqlConnection(connectionString);
            await probe.OpenAsync();
            Console.WriteLine("[Migration] ✓ Connectivity probe succeeded (able to open a DB connection)");
            await probe.CloseAsync();
        }
        catch (Exception connEx)
        {
            Console.WriteLine("[Migration ERROR] ✗ Connectivity probe failed before running migrations:");
            Console.WriteLine(connEx.ToString());
            return; // Don't run migrations if connectivity fails
        }

        await context.Database.MigrateAsync();
        Console.WriteLine("[Migration] ✓ Migrations applied successfully!");
    }
    catch (Exception ex)
    {
        // Log full details including inner exceptions for easier debugging on the deployment logs
        Console.WriteLine($"[Migration ERROR] ✗ Failed to apply migrations: {ex.Message}");
        if (ex.InnerException != null) Console.WriteLine($"[Migration ERROR] InnerException: {ex.InnerException}");
        Console.WriteLine(ex.ToString());
        // Don't throw - allow app to continue running even if migration fails
    }
});

await runTask;
