using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ShareSafely.API.Authentication;
using ShareSafely.API.Data;
using ShareSafely.API.Middleware;
using ShareSafely.API.Services;
using ShareSafely.API.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// CONFIGURACIÓN DE AZURE KEY VAULT (solo si está configurado)
// ============================================================
var keyVaultUrl = builder.Configuration["KeyVault:Url"];
if (!string.IsNullOrEmpty(keyVaultUrl) &&
    !keyVaultUrl.Contains("<") &&
    Uri.TryCreate(keyVaultUrl, UriKind.Absolute, out var keyVaultUri))
{
    try
    {
        builder.Configuration.AddAzureKeyVault(keyVaultUri, new DefaultAzureCredential());
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Warning: Could not connect to Key Vault: {ex.Message}");
    }
}

// ============================================================
// CONFIGURACIÓN DE SERVICIOS
// ============================================================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "ShareSafely API",
        Version = "v1",
        Description = "API para gestión segura de archivos con enlaces temporales"
    });

    // Add API key authentication to Swagger
    c.AddSecurityDefinition("ApiKey", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "API Key authentication. Enter your API key in the header.",
        Name = "X-API-Key",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "ApiKey"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ============================================================
// CONFIGURACIÓN DE AUTENTICACIÓN
// ============================================================
builder.Services.AddAuthentication(ApiKeyAuthenticationOptions.DefaultScheme)
    .AddApiKeyAuthentication();

builder.Services.AddAuthorization(options =>
{
    // Default policy requires authentication
    options.FallbackPolicy = options.DefaultPolicy;

    // Admin policy for destructive operations
    options.AddPolicy("Admin", policy =>
        policy.RequireRole("Admin"));

    // ReadOnly policy for public endpoints
    options.AddPolicy("ReadOnly", policy =>
        policy.RequireRole("User", "Admin", "ReadOnly"));
});

// ============================================================
// INYECCIÓN DE DEPENDENCIAS - SERVICIOS DE LA APLICACIÓN
// ============================================================
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IFileMetadataService, FileMetadataService>();

// Usar Azure Storage o almacenamiento local según configuración
var storageProvider = builder.Configuration["Storage:Provider"] ?? "Azure";
if (storageProvider.Equals("Local", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
    builder.Services.AddScoped<ISasLinkService, LocalSasLinkService>();
}
else
{
    builder.Services.AddScoped<IFileStorageService, AzureBlobStorageService>();
    builder.Services.AddScoped<ISasLinkService, SasLinkService>();
}

// ============================================================
// CONFIGURACIÓN DE APPLICATION INSIGHTS (MONITOREO)
// ============================================================
var appInsightsConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
if (!string.IsNullOrEmpty(appInsightsConnectionString) &&
    !appInsightsConnectionString.StartsWith("<"))
{
    builder.Services.AddApplicationInsightsTelemetry(options =>
    {
        options.ConnectionString = appInsightsConnectionString;
    });
}

// ============================================================
// CONFIGURACIÓN DE CORS
// ============================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? new[] { "http://localhost:3000", "https://localhost:3000" };

        // If "*" is in allowed origins, allow any origin
        if (allowedOrigins.Contains("*"))
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
        else
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
    });
});

// ============================================================
// CONFIGURACIÓN DE BASE DE DATOS (SQL Server o PostgreSQL)
// ============================================================
var databaseProvider = builder.Configuration["Database:Provider"] ?? "SqlServer";
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Railway PostgreSQL connection - try multiple formats
Console.WriteLine($"[DB] Provider configured: {databaseProvider}");

if (databaseProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
{
    // Try DATABASE_URL first (Railway standard)
    var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

    // Try individual PostgreSQL variables (Railway alternative)
    var pgHost = Environment.GetEnvironmentVariable("PGHOST");
    var pgPort = Environment.GetEnvironmentVariable("PGPORT") ?? "5432";
    var pgDatabase = Environment.GetEnvironmentVariable("PGDATABASE");
    var pgUser = Environment.GetEnvironmentVariable("PGUSER");
    var pgPassword = Environment.GetEnvironmentVariable("PGPASSWORD");

    Console.WriteLine($"[DB] DATABASE_URL present: {!string.IsNullOrEmpty(databaseUrl)}");
    Console.WriteLine($"[DB] PGHOST present: {!string.IsNullOrEmpty(pgHost)}");

    if (!string.IsNullOrEmpty(databaseUrl))
    {
        connectionString = ConvertPostgresUrl(databaseUrl);
        Console.WriteLine($"[DB] Using DATABASE_URL");
    }
    else if (!string.IsNullOrEmpty(pgHost))
    {
        connectionString = $"Host={pgHost};Port={pgPort};Database={pgDatabase};Username={pgUser};Password={pgPassword};SSL Mode=Require;Trust Server Certificate=true";
        Console.WriteLine($"[DB] Using PG* variables - Host: {pgHost}");
    }
    else
    {
        Console.WriteLine($"[DB] No Railway PostgreSQL variables found, using appsettings");
        Console.WriteLine($"[DB] ConnectionString from config: {!string.IsNullOrEmpty(connectionString)}");
    }
}
else
{
    Console.WriteLine($"[DB] Using SQL Server");
}

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (databaseProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
    {
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorCodesToAdd: null);
            npgsqlOptions.CommandTimeout(30);
        });
    }
    else
    {
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
            sqlOptions.CommandTimeout(30);
        });
    }
});

// ============================================================
// CONFIGURACIÓN DE HEALTH CHECKS
// ============================================================
builder.Services.AddHealthChecks();

var app = builder.Build();

// ============================================================
// INICIALIZACIÓN DE BASE DE DATOS
// ============================================================
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // Retry logic for database initialization
    var maxRetries = 5;
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            logger.LogInformation("[DB] Attempt {Attempt}/{Max} - Connecting to database...", i + 1, maxRetries);

            if (db.Database.CanConnect())
            {
                logger.LogInformation("[DB] Connection successful, ensuring schema...");
                db.Database.EnsureCreated();
                logger.LogInformation("[DB] Database ready!");
                break;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning("[DB] Attempt {Attempt} failed: {Message}", i + 1, ex.Message);

            if (i < maxRetries - 1)
            {
                logger.LogInformation("[DB] Retrying in 3 seconds...");
                Thread.Sleep(3000);
            }
            else
            {
                logger.LogError(ex, "[DB] All connection attempts failed");
            }
        }
    }
}

// ============================================================
// PIPELINE DE MIDDLEWARE
// ============================================================

// Global exception handler - FIRST in pipeline
app.UseGlobalExceptionHandler();

// Enable Swagger in all environments for demo
app.UseSwagger();
app.UseSwaggerUI();

// Skip HTTPS redirect in production (Railway handles SSL at proxy level)
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowFrontend");

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ============================================================
// ENDPOINT DE HEALTH CHECK
// ============================================================
app.MapHealthChecks("/health");

// Simple health endpoint for basic checks
app.MapGet("/", () => Results.Ok(new
{
    Service = "ShareSafely API",
    Status = "Running",
    Timestamp = DateTime.UtcNow
})).AllowAnonymous();

app.Run();

// ============================================================
// HELPER: Convert Railway DATABASE_URL to Npgsql format
// ============================================================
static string ConvertPostgresUrl(string databaseUrl)
{
    // Railway format: postgresql://user:password@host:port/database
    // Npgsql format: Host=host;Port=port;Database=database;Username=user;Password=password

    try
    {
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':');
        var username = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 5432;
        var database = uri.AbsolutePath.TrimStart('/');

        var connStr = $"Host={host};Port={port};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true";

        Console.WriteLine($"[DB] Converted connection - Host: {host}, Port: {port}, Database: {database}, User: {username}");

        return connStr;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DB] Error parsing DATABASE_URL: {ex.Message}");
        Console.WriteLine($"[DB] Raw URL starts with: {databaseUrl.Substring(0, Math.Min(30, databaseUrl.Length))}...");
        throw;
    }
}
