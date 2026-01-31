using Azure;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ShareSafely.Functions.Functions;

public class CleanupExpiredFiles
{
    private readonly ILogger<CleanupExpiredFiles> _logger;
    private readonly IConfiguration _config;
    private readonly Lazy<BlobContainerClient> _lazyContainerClient;

    // Retry configuration
    private const int MaxRetries = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    public CleanupExpiredFiles(
        ILogger<CleanupExpiredFiles> logger,
        IConfiguration config)
    {
        _logger = logger;
        _config = config;
        _lazyContainerClient = new Lazy<BlobContainerClient>(() => InitializeContainer());
    }

    private BlobContainerClient ContainerClient => _lazyContainerClient.Value;

    private BlobContainerClient InitializeContainer()
    {
        var connectionString = _config["AzureStorage:ConnectionString"];
        var containerName = _config["AzureStorage:ContainerName"];

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Azure Storage connection string not configured");
        }

        var options = new BlobClientOptions
        {
            Retry =
            {
                MaxRetries = 3,
                Delay = TimeSpan.FromSeconds(1),
                MaxDelay = TimeSpan.FromSeconds(10),
                Mode = Azure.Core.RetryMode.Exponential
            }
        };

        var blobServiceClient = new BlobServiceClient(connectionString, options);
        return blobServiceClient.GetBlobContainerClient(containerName ?? "archivos");
    }

    /// <summary>
    /// Se ejecuta cada hora para limpiar archivos expirados
    /// Cron: "0 0 * * * *" = cada hora en punto
    /// </summary>
    [Function("CleanupExpiredFiles")]
    public async Task Run([TimerTrigger("0 0 * * * *")] TimerInfo timer)
    {
        var runId = Guid.NewGuid().ToString("N")[..8];
        _logger.LogInformation("[{RunId}] Starting cleanup of expired files: {Time}", runId, DateTime.UtcNow);

        var stats = new CleanupStats();

        try
        {
            var expiredFiles = await GetExpiredFilesWithRetryAsync(runId);
            stats.TotalFound = expiredFiles.Count;

            _logger.LogInformation("[{RunId}] Found {Count} expired files", runId, expiredFiles.Count);

            if (expiredFiles.Count == 0)
            {
                _logger.LogInformation("[{RunId}] No files to clean up", runId);
                return;
            }

            foreach (var file in expiredFiles)
            {
                await ProcessExpiredFileAsync(file, stats, runId);
            }

            LogCleanupSummary(stats, runId);
        }
        catch (SqlException ex)
        {
            _logger.LogCritical(ex, "[{RunId}] Database connection failed during cleanup", runId);
            throw; // Re-throw to mark function as failed
        }
        catch (RequestFailedException ex)
        {
            _logger.LogCritical(ex, "[{RunId}] Azure storage connection failed during cleanup", runId);
            throw; // Re-throw to mark function as failed
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{RunId}] Unexpected error during cleanup. Stats: {Stats}", runId, stats);
            throw; // Re-throw to mark function as failed
        }
    }

    private async Task<List<ExpiredFile>> GetExpiredFilesWithRetryAsync(string runId)
    {
        var connectionString = _config["ConnectionStrings:DefaultConnection"];

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Database connection string not configured");
        }

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                return await GetExpiredFilesAsync(connectionString);
            }
            catch (SqlException ex) when (IsTransientError(ex) && attempt < MaxRetries)
            {
                _logger.LogWarning(ex,
                    "[{RunId}] Transient database error on attempt {Attempt}/{MaxRetries}. Retrying...",
                    runId, attempt, MaxRetries);
                await Task.Delay(RetryDelay * attempt);
            }
        }

        // Final attempt - let it throw
        return await GetExpiredFilesAsync(connectionString);
    }

    private async Task<List<ExpiredFile>> GetExpiredFilesAsync(string connectionString)
    {
        var files = new List<ExpiredFile>();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string query = @"
            SELECT Id, Nombre, ContentType
            FROM Archivos
            WHERE Estado = 1
              AND FechaExpiracion IS NOT NULL
              AND FechaExpiracion < @Now";

        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@Now", DateTime.UtcNow);
        command.CommandTimeout = 30;

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            files.Add(new ExpiredFile
            {
                Id = reader.GetGuid(0),
                BlobName = reader.GetString(1),
                ContentType = reader.IsDBNull(2) ? string.Empty : reader.GetString(2)
            });
        }

        return files;
    }

    private async Task ProcessExpiredFileAsync(ExpiredFile file, CleanupStats stats, string runId)
    {
        try
        {
            // Delete blob with retry
            var blobDeleted = await DeleteBlobWithRetryAsync(file, runId);

            if (blobDeleted)
            {
                // Update database status
                await UpdateFileStatusWithRetryAsync(file.Id, 3, runId); // 3 = Eliminado
                stats.Deleted++;
                _logger.LogInformation("[{RunId}] Deleted file: {Id} - {Name}", runId, file.Id, file.BlobName);
            }
            else
            {
                // Blob not found - still mark as deleted in DB
                await UpdateFileStatusWithRetryAsync(file.Id, 3, runId);
                stats.NotFound++;
                _logger.LogWarning("[{RunId}] Blob not found (marking as deleted): {Id} - {Name}",
                    runId, file.Id, file.BlobName);
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Blob already deleted - mark as deleted in DB
            try
            {
                await UpdateFileStatusWithRetryAsync(file.Id, 3, runId);
                stats.NotFound++;
                _logger.LogWarning("[{RunId}] Blob already deleted: {Id}", runId, file.Id);
            }
            catch (Exception dbEx)
            {
                stats.Errors++;
                _logger.LogError(dbEx, "[{RunId}] Failed to update status after blob 404: {Id}", runId, file.Id);
            }
        }
        catch (Exception ex)
        {
            stats.Errors++;
            _logger.LogError(ex, "[{RunId}] Error processing file: {Id} - {Name}", runId, file.Id, file.BlobName);
        }
    }

    private async Task<bool> DeleteBlobWithRetryAsync(ExpiredFile file, string runId)
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var blobClient = ContainerClient.GetBlobClient(file.BlobName);
                var response = await blobClient.DeleteIfExistsAsync();
                return response.Value;
            }
            catch (RequestFailedException ex) when (ex.Status != 404 && attempt < MaxRetries)
            {
                _logger.LogWarning(ex,
                    "[{RunId}] Transient blob error on attempt {Attempt}/{MaxRetries} for {Id}",
                    runId, attempt, MaxRetries, file.Id);
                await Task.Delay(RetryDelay * attempt);
            }
        }

        // Final attempt
        var finalBlobClient = ContainerClient.GetBlobClient(file.BlobName);
        var finalResponse = await finalBlobClient.DeleteIfExistsAsync();
        return finalResponse.Value;
    }

    private async Task UpdateFileStatusWithRetryAsync(Guid id, int status, string runId)
    {
        var connectionString = _config["ConnectionStrings:DefaultConnection"];

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await UpdateFileStatusAsync(connectionString!, id, status);
                return;
            }
            catch (SqlException ex) when (IsTransientError(ex) && attempt < MaxRetries)
            {
                _logger.LogWarning(ex,
                    "[{RunId}] Transient DB error updating status, attempt {Attempt}/{MaxRetries} for {Id}",
                    runId, attempt, MaxRetries, id);
                await Task.Delay(RetryDelay * attempt);
            }
        }

        // Final attempt - let it throw
        await UpdateFileStatusAsync(connectionString!, id, status);
    }

    private static async Task UpdateFileStatusAsync(string connectionString, Guid id, int status)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string query = "UPDATE Archivos SET Estado = @Status WHERE Id = @Id";

        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@Status", status);
        command.Parameters.AddWithValue("@Id", id);
        command.CommandTimeout = 15;

        await command.ExecuteNonQueryAsync();
    }

    private static bool IsTransientError(SqlException ex)
    {
        // Common transient SQL error numbers
        int[] transientErrors = { -2, 53, 10053, 10054, 10060, 40197, 40501, 40613, 49918, 49919, 49920 };
        return transientErrors.Contains(ex.Number);
    }

    private void LogCleanupSummary(CleanupStats stats, string runId)
    {
        var level = stats.Errors > 0 ? LogLevel.Warning : LogLevel.Information;

        _logger.Log(level,
            "[{RunId}] Cleanup completed. Found: {Found}, Deleted: {Deleted}, NotFound: {NotFound}, Errors: {Errors}",
            runId, stats.TotalFound, stats.Deleted, stats.NotFound, stats.Errors);
    }

    private class ExpiredFile
    {
        public Guid Id { get; set; }
        public string BlobName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
    }

    private class CleanupStats
    {
        public int TotalFound { get; set; }
        public int Deleted { get; set; }
        public int NotFound { get; set; }
        public int Errors { get; set; }

        public override string ToString() =>
            $"Found={TotalFound}, Deleted={Deleted}, NotFound={NotFound}, Errors={Errors}";
    }
}
