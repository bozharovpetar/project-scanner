using System.Threading.Channels;
using CodeScanner.Api.Data;
using CodeScanner.Api.Models.Dtos;
using CodeScanner.Api.Models.Entities;
using CodeScanner.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace CodeScanner.Api.Services;

public class ScanService : IScanService
{
    private readonly AppDbContext _db;
    private readonly IFileDiscoveryService _fileDiscovery;
    private readonly Channel<int> _scanQueue;
    private readonly ILogger<ScanService> _logger;

    public ScanService(
        AppDbContext db,
        IFileDiscoveryService fileDiscovery,
        Channel<int> scanQueue,
        ILogger<ScanService> logger)
    {
        _db = db;
        _fileDiscovery = fileDiscovery;
        _scanQueue = scanQueue;
        _logger = logger;
    }

    public async Task<Scan> CreateScanAsync(string projectPath, CancellationToken ct = default)
    {
        if (!Directory.Exists(projectPath))
            throw new ArgumentException($"Directory does not exist: {projectPath}");

        var discoveredFiles = await _fileDiscovery.DiscoverFilesAsync(projectPath, ct);

        var scan = new Scan
        {
            ProjectPath = projectPath,
            Status = ScanStatus.Queued,
            TotalFiles = discoveredFiles.Count,
            Files = discoveredFiles.Select(f => new ScanFile
            {
                RelativePath = f.RelativePath,
                Language = f.Language,
                FileSizeBytes = f.SizeBytes
            }).ToList()
        };

        _db.Scans.Add(scan);
        await _db.SaveChangesAsync(ct);

        await _scanQueue.Writer.WriteAsync(scan.Id, ct);
        _logger.LogInformation("Scan {ScanId} created with {FileCount} files for {Path}", scan.Id, scan.TotalFiles, projectPath);

        return scan;
    }

    public async Task<Scan?> GetScanAsync(int id, CancellationToken ct = default)
    {
        return await _db.Scans
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<List<Scan>> ListScansAsync(int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        return await _db.Scans
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task DeleteScanAsync(int id, CancellationToken ct = default)
    {
        var scan = await _db.Scans.FindAsync([id], ct);
        if (scan is null)
            return;

        if (scan.Status is ScanStatus.Queued or ScanStatus.InProgress)
            throw new InvalidOperationException($"Cannot delete scan {id} while it is {scan.Status}. Wait for it to complete or fail.");

        _db.Scans.Remove(scan);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<FindingResponse>> GetFindingsAsync(
        int scanId, FindingCategory? category, Severity? severity,
        int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.Findings
            .AsNoTracking()
            .Include(f => f.ScanFile)
            .Where(f => f.ScanFile.ScanId == scanId);

        if (category.HasValue)
            query = query.Where(f => f.Category == category.Value);
        if (severity.HasValue)
            query = query.Where(f => f.Severity == severity.Value);

        return await query
            .OrderByDescending(f => f.Severity)
            .ThenBy(f => f.ScanFile.RelativePath)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new FindingResponse(
                f.Id,
                f.ScanFile.RelativePath,
                f.Category.ToString(),
                f.Severity.ToString(),
                f.Title,
                f.Description,
                f.LineStart,
                f.LineEnd,
                f.CodeSnippet,
                f.Suggestion))
            .ToListAsync(ct);
    }

    public async Task<ScanSummary> GetSummaryAsync(int scanId, CancellationToken ct = default)
    {
        var categoryCounts = await _db.Findings
            .AsNoTracking()
            .Where(f => f.ScanFile.ScanId == scanId)
            .GroupBy(f => f.Category)
            .Select(g => new { Category = g.Key.ToString(), Count = g.Count() })
            .ToListAsync(ct);

        var severityCounts = await _db.Findings
            .AsNoTracking()
            .Where(f => f.ScanFile.ScanId == scanId)
            .GroupBy(f => f.Severity)
            .Select(g => new { Severity = g.Key.ToString(), Count = g.Count() })
            .ToListAsync(ct);

        var total = categoryCounts.Sum(c => c.Count);

        return new ScanSummary(
            total,
            categoryCounts.ToDictionary(c => c.Category, c => c.Count),
            severityCounts.ToDictionary(c => c.Severity, c => c.Count));
    }

    public async Task<List<ScanFileResponse>> GetFilesAsync(int scanId, bool? hasFindings, CancellationToken ct = default)
    {
        var query = _db.ScanFiles
            .AsNoTracking()
            .Where(f => f.ScanId == scanId);

        if (hasFindings == true)
            query = query.Where(f => f.Findings.Any());
        else if (hasFindings == false)
            query = query.Where(f => !f.Findings.Any());

        return await query
            .OrderBy(f => f.RelativePath)
            .Select(f => new ScanFileResponse(
                f.Id,
                f.RelativePath,
                f.Language,
                f.FileSizeBytes,
                f.Analyzed,
                f.AnalyzedAt,
                f.Findings.Count))
            .ToListAsync(ct);
    }

    public async Task<ScanFile?> GetFileWithFindingsAsync(int scanId, int fileId, CancellationToken ct = default)
    {
        return await _db.ScanFiles
            .AsNoTracking()
            .Include(f => f.Findings)
            .FirstOrDefaultAsync(f => f.ScanId == scanId && f.Id == fileId, ct);
    }
}
