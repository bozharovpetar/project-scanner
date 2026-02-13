namespace CodeScanner.Api.Models.Dtos;

public record ScanDetailResponse(
    int Id,
    string ProjectPath,
    string Status,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    int TotalFiles,
    int ProcessedFiles,
    string? ErrorMessage,
    ScanSummary Summary);

public record ScanSummary(
    int TotalFindings,
    Dictionary<string, int> ByCategory,
    Dictionary<string, int> BySeverity);
