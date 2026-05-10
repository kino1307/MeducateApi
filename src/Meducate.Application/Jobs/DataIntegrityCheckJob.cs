using Hangfire;
using Hangfire.Console;
using Hangfire.Server;
using Meducate.Application.Services;
using Microsoft.Extensions.Logging;

namespace Meducate.Application.Jobs;

internal sealed class DataIntegrityCheckJob(
    DataIntegrityCheckService checkService,
    ILogger<DataIntegrityCheckJob> logger)
{
    private readonly DataIntegrityCheckService _checkService = checkService;
    private readonly ILogger<DataIntegrityCheckJob> _logger = logger;

    [DisableConcurrentExecution(timeoutInSeconds: 0)]
    public async Task ExecuteAsync(IJobCancellationToken jobCancellationToken, PerformContext? context = null)
    {
        try
        {
            var ct = jobCancellationToken.ShutdownToken;
            _logger.LogInformation("Starting data integrity check job");
            context?.WriteLine("Starting data integrity check...");
            await _checkService.RunAsync(context, ct);
            _logger.LogInformation("Data integrity check job completed");
            context?.WriteLine("Data integrity check completed.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Data integrity check job was cancelled (shutdown)");
            context?.WriteLine("Job cancelled (shutdown).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Data integrity check job failed");
            context?.WriteLine($"Job failed: {ex.Message}");
            throw;
        }
    }
}
