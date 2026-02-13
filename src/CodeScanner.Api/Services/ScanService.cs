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
            .Include(s => s.Files)
            .ThenInclude(f => f.Findings)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<List<Scan>> ListScansAsync(int page, int pageSize, CancellationToken ct = default)
    {
        return await _db.Scans
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task DeleteScanAsync(int id, CancellationToken ct = default)
    {
        var scan = await _db.Scans.FindAsync([id], ct);
        if (scan is not null)
        {
            _db.Scans.Remove(scan);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<List<FindingResponse>> GetFindingsAsync(
        int scanId, FindingCategory? category, Severity? severity,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.Findings
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
        var findings = await _db.Findings
            .Include(f => f.ScanFile)
            .Where(f => f.ScanFile.ScanId == scanId)
            .ToListAsync(ct);

        return new ScanSummary(
            findings.Count,
            findings.GroupBy(f => f.Category.ToString()).ToDictionary(g => g.Key, g => g.Count()),
            findings.GroupBy(f => f.Severity.ToString()).ToDictionary(g => g.Key, g => g.Count()));
    }

    public async Task<List<ScanFileResponse>> GetFilesAsync(int scanId, bool? hasFindings, CancellationToken ct = default)
    {
        var query = _db.ScanFiles
            .Include(f => f.Findings)
            .Where(f => f.ScanId == scanId);

        if (hasFindings == true)
            query = query.Where(f => f.Findings.Count > 0);
        else if (hasFindings == false)
            query = query.Where(f => f.Findings.Count == 0);

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
            .Include(f => f.Findings)
            .FirstOrDefaultAsync(f => f.ScanId == scanId && f.Id == fileId, ct);
    }
}
