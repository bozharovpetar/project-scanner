using System.Threading.Channels;
using CodeScanner.Api.Data;
using CodeScanner.Api.Models.Dtos;
using CodeScanner.Api.Models.Enums;
using CodeScanner.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace CodeScanner.Api.BackgroundServices;

public class ScanProcessorService : BackgroundService
{
    private readonly Channel<int> _scanQueue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ScanProgressBroadcaster _broadcaster;
    private readonly ILogger<ScanProcessorService> _logger;

    public ScanProcessorService(
        Channel<int> scanQueue,
        IServiceScopeFactory scopeFactory,
        ScanProgressBroadcaster broadcaster,
        ILogger<ScanProcessorService> logger)
    {
        _scanQueue = scanQueue;
        _scopeFactory = scopeFactory;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scan processor service started");

        await foreach (var scanId in _scanQueue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessScanAsync(scanId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error processing scan {ScanId}", scanId);
            }
        }
    }

    private async Task ProcessScanAsync(int scanId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var fileAnalyzer = scope.ServiceProvider.GetRequiredService<IFileAnalyzer>();

        var scan = await db.Scans
            .Include(s => s.Files)
            .FirstOrDefaultAsync(s => s.Id == scanId, ct);

        if (scan is null)
        {
            _logger.LogWarning("Scan {ScanId} not found, skipping", scanId);
            return;
        }

        scan.Status = ScanStatus.InProgress;
        await db.SaveChangesAsync(ct);

        await _broadcaster.BroadcastAsync(scanId, new ScanProgressEvent(
            "scan_started", scanId, null, 0, scan.TotalFiles, "Scan started"));

        _logger.LogInformation("Processing scan {ScanId}: {FileCount} files in {Path}",
            scanId, scan.TotalFiles, scan.ProjectPath);

        try
        {
            foreach (var file in scan.Files)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var filePath = Path.Combine(scan.ProjectPath, file.RelativePath);
                    if (!File.Exists(filePath))
                    {
                        file.ErrorMessage = "File not found";
                        file.Analyzed = true;
                        file.AnalyzedAt = DateTime.UtcNow;
                        scan.ProcessedFiles++;
                        await db.SaveChangesAsync(ct);
                        continue;
                    }

                    var content = await File.ReadAllTextAsync(filePath, ct);

                    _logger.LogInformation("Analyzing {FilePath} ({Language})", file.RelativePath, file.Language);

                    var findings = await fileAnalyzer.AnalyzeFileAsync(file.RelativePath, content, file.Language, ct);

                    file.Findings.AddRange(findings);
                    file.Analyzed = true;
                    file.AnalyzedAt = DateTime.UtcNow;
                    scan.ProcessedFiles++;

                    await db.SaveChangesAsync(ct);

                    await _broadcaster.BroadcastAsync(scanId, new ScanProgressEvent(
                        "file_completed", scanId, file.RelativePath,
                        scan.ProcessedFiles, scan.TotalFiles,
                        $"Analyzed {file.RelativePath} — {findings.Count} findings"));

                    _logger.LogInformation("Completed {FilePath}: {FindingCount} findings ({Processed}/{Total})",
                        file.RelativePath, findings.Count, scan.ProcessedFiles, scan.TotalFiles);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Error analyzing file {FilePath}", file.RelativePath);
                    file.ErrorMessage = ex.Message;
                    file.Analyzed = true;
                    file.AnalyzedAt = DateTime.UtcNow;
                    scan.ProcessedFiles++;
                    await db.SaveChangesAsync(ct);

                    await _broadcaster.BroadcastAsync(scanId, new ScanProgressEvent(
                        "file_error", scanId, file.RelativePath,
                        scan.ProcessedFiles, scan.TotalFiles,
                        $"Error analyzing {file.RelativePath}: {ex.Message}"));
                }
            }

            scan.Status = ScanStatus.Completed;
            scan.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            await _broadcaster.BroadcastAsync(scanId, new ScanProgressEvent(
                "scan_completed", scanId, null,
                scan.ProcessedFiles, scan.TotalFiles, "Scan completed"));

            _logger.LogInformation("Scan {ScanId} completed successfully", scanId);
        }
        catch (OperationCanceledException)
        {
            scan.Status = ScanStatus.Failed;
            scan.ErrorMessage = "Scan was cancelled";
            await db.SaveChangesAsync(CancellationToken.None);
            _logger.LogWarning("Scan {ScanId} was cancelled", scanId);
        }
        catch (Exception ex)
        {
            scan.Status = ScanStatus.Failed;
            scan.ErrorMessage = ex.Message;
            await db.SaveChangesAsync(CancellationToken.None);

            await _broadcaster.BroadcastAsync(scanId, new ScanProgressEvent(
                "scan_failed", scanId, null,
                scan.ProcessedFiles, scan.TotalFiles,
                $"Scan failed: {ex.Message}"));

            _logger.LogError(ex, "Scan {ScanId} failed", scanId);
        }
        finally
        {
            _broadcaster.Complete(scanId);
        }
    }
}
