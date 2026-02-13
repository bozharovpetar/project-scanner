namespace CodeScanner.Api.Models.Dtos;

public record ScanProgressEvent(
    string Type,
    int ScanId,
    string? FilePath,
    int ProcessedFiles,
    int TotalFiles,
    string? Message);
