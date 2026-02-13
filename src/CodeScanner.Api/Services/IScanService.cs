using CodeScanner.Api.Models.Dtos;
using CodeScanner.Api.Models.Entities;
using CodeScanner.Api.Models.Enums;

namespace CodeScanner.Api.Services;

public interface IScanService
{
    Task<Scan> CreateScanAsync(string projectPath, CancellationToken ct = default);
    Task<Scan?> GetScanAsync(int id, CancellationToken ct = default);
    Task<List<Scan>> ListScansAsync(int page, int pageSize, CancellationToken ct = default);
    Task DeleteScanAsync(int id, CancellationToken ct = default);
    Task<List<FindingResponse>> GetFindingsAsync(int scanId, FindingCategory? category, Severity? severity, int page, int pageSize, CancellationToken ct = default);
    Task<ScanSummary> GetSummaryAsync(int scanId, CancellationToken ct = default);
    Task<List<ScanFileResponse>> GetFilesAsync(int scanId, bool? hasFindings, CancellationToken ct = default);
    Task<ScanFile?> GetFileWithFindingsAsync(int scanId, int fileId, CancellationToken ct = default);
}
