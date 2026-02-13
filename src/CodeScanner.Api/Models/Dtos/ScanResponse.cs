namespace CodeScanner.Api.Models.Dtos;

public record ScanResponse(
    int Id,
    string ProjectPath,
    string Status,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    int TotalFiles,
    int ProcessedFiles);
