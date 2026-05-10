using Hangfire.Console;
using Hangfire.Server;
using Meducate.Domain.Repositories;
using Meducate.Domain.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Meducate.Application.Services;

internal sealed class DataIntegrityCheckService(
    ITopicQueryRepository queryRepo,
    ILLMProcessor llmProcessor,
    IEmailService emailService,
    IConfiguration config,
    ILogger<DataIntegrityCheckService> logger)
{
    private readonly ITopicQueryRepository _queryRepo = queryRepo;
    private readonly ILLMProcessor _llmProcessor = llmProcessor;
    private readonly IEmailService _emailService = emailService;
    private readonly IConfiguration _config = config;
    private readonly ILogger<DataIntegrityCheckService> _logger = logger;

    internal const int BatchSize = 50;

    internal async Task RunAsync(PerformContext? console, CancellationToken ct)
    {
        var validCategories = _llmProcessor.GetValidCategories();
        var failures = new List<string>();
        var warnings = new List<string>();

        // Global check: any topics with a missing or non-standard category
        var needingCategory = await _queryRepo.GetTopicsNeedingCategoryAsync(validCategories, ct);
        if (needingCategory.Count > 0)
        {
            var detail = $"{needingCategory.Count} topic(s) have a null or non-standard category: " +
                         string.Join(", ", needingCategory.Take(5).Select(t => t.Name)) +
                         (needingCategory.Count > 5 ? $" (+{needingCategory.Count - 5} more)" : "");

            failures.Add(detail);
            _logger.LogWarning("Data integrity: {Detail}", detail);
            console?.WriteLine($"[FAIL] {detail}");
        }
        else
        {
            console?.WriteLine("[OK] All topics have valid categories.");
        }

        // Rotating batch check over served topics
        var servedCount = await _queryRepo.GetServedTopicCountAsync(ct);
        if (servedCount == 0)
        {
            console?.WriteLine("No served topics found — skipping batch check.");
            _logger.LogWarning("Data integrity check: no served topics found");
            return;
        }

        var totalBatches = (int)Math.Ceiling((double)servedCount / BatchSize);
        var batchIndex = DateOnly.FromDateTime(DateTime.UtcNow).DayNumber % totalBatches;
        var skip = batchIndex * BatchSize;
        var batch = await _queryRepo.GetServedTopicBatchAsync(skip, BatchSize, ct);

        _logger.LogInformation(
            "Data integrity check: batch {Index}/{Total}, topics {Skip}-{End} of {Total2} served",
            batchIndex + 1, totalBatches, skip + 1, skip + batch.Count, servedCount);

        console?.WriteLine($"Batch {batchIndex + 1}/{totalBatches}: checking topics {skip + 1}-{skip + batch.Count} of {servedCount}.");

        foreach (var topic in batch)
        {
            // Category should always be valid here (served topics have Category != null),
            // but double-check in case of a race with the refresh job
            if (!validCategories.Contains(topic.Category!))
            {
                var msg = $"{topic.Name}: invalid category '{topic.Category}'";
                failures.Add(msg);
                _logger.LogWarning("Data integrity [FAIL] {Message}", msg);
                console?.WriteLine($"  [FAIL] {msg}");
                continue;
            }

            if (string.IsNullOrWhiteSpace(topic.TopicType))
            {
                var msg = $"{topic.Name}: missing topic type";
                failures.Add(msg);
                _logger.LogWarning("Data integrity [FAIL] {Message}", msg);
                console?.WriteLine($"  [FAIL] {msg}");
                continue;
            }

            if (string.IsNullOrWhiteSpace(topic.Summary))
            {
                var msg = $"{topic.Name}: missing summary";
                failures.Add(msg);
                _logger.LogWarning("Data integrity [FAIL] {Message}", msg);
                console?.WriteLine($"  [FAIL] {msg}");
                continue;
            }

            var emptyFields = new List<string>();
            if (topic.Observations == null || topic.Observations.Count == 0) emptyFields.Add("observations");
            if (topic.Factors == null || topic.Factors.Count == 0) emptyFields.Add("factors");
            if (topic.Actions == null || topic.Actions.Count == 0) emptyFields.Add("actions");

            if (emptyFields.Count > 0)
            {
                var msg = $"{topic.Name}: empty structured field(s): {string.Join(", ", emptyFields)}";
                warnings.Add(msg);
                _logger.LogWarning("Data integrity [WARN] {Message}", msg);
                console?.WriteLine($"  [WARN] {msg}");
            }
        }

        _logger.LogInformation(
            "Data integrity check complete: {Failures} failure(s), {Warnings} warning(s) in batch of {Count}",
            failures.Count, warnings.Count, batch.Count);

        console?.WriteLine($"Done: {failures.Count} failure(s), {warnings.Count} warning(s) in {batch.Count} topics checked.");

        if (failures.Count > 0)
        {
            var alertEmail = _config["Admin:AlertEmail"];
            if (string.IsNullOrWhiteSpace(alertEmail))
            {
                _logger.LogWarning("Data integrity failures found but Admin:AlertEmail is not configured — no alert sent");
                console?.WriteLine("[WARN] Admin:AlertEmail not configured — alert email not sent.");
                return;
            }

            await _emailService.SendDataIntegrityAlertAsync(
                alertEmail,
                failures.Count,
                warnings.Count,
                batch.Count,
                batchIndex,
                totalBatches,
                failures);
        }
    }
}
