using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using ShareSafely.API.Data;
using ShareSafely.API.Services;
using ShareSafely.API.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// CONFIGURACIÓN DE AZURE KEY VAULT
// ============================================================
// TODO: Implementar lectura de secretos desde Azure Key Vault
// Los secretos a obtener:
// - StorageConnectionString: Conexión a Azure Blob Storage
// - DatabaseConnectionString: Conexión a la base de datos
// - SasTokenDuration: Duración del token SAS en minutos
// ============================================================
if (!builder.Environment.IsDevelopment())
{
    var keyVaultUrl = builder.Configuration["KeyVault:Url"];
    if (!string.IsNullOrEmpty(keyVaultUrl))
    {
        builder.Configuration.AddAzureKeyVault(
            new Uri(keyVaultUrl),
            new DefaultAzureCredential());
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
});

// ============================================================
// INYECCIÓN DE DEPENDENCIAS - SERVICIOS DE LA APLICACIÓN
// ============================================================
builder.Services.AddScoped<IFileMetadataService, FileMetadataService>();
builder.Services.AddScoped<IFileStorageService, AzureBlobStorageService>();
builder.Services.AddScoped<ISasLinkService, SasLinkService>();

// ============================================================
// CONFIGURACIÓN DE APPLICATION INSIGHTS (MONITOREO)
// ============================================================
// TODO: Descomentar cuando se configure Application Insights
// builder.Services.AddApplicationInsightsTelemetry();

// ============================================================
// CONFIGURACIÓN DE CORS
// ============================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        // TODO: Cambiar a la URL real del frontend en producción
        policy.WithOrigins("http://localhost:3000", "https://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ============================================================
// CONFIGURACIÓN DE BASE DE DATOS
// ============================================================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

var app = builder.Build();

// ============================================================
// PIPELINE DE MIDDLEWARE
// ============================================================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthorization();
app.MapControllers();

// ============================================================
// ENDPOINT DE HEALTH CHECK
// ============================================================
app.MapGet("/health", () => Results.Ok(new
{
    Status = "Healthy",
    Timestamp = DateTime.UtcNow
}));

app.Run();
