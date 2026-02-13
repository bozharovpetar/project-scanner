namespace CodeScanner.Api.Models.Dtos;

public record ScanFileResponse(
    int Id,
    string RelativePath,
    string Language,
    long FileSizeBytes,
    bool Analyzed,
    DateTime? AnalyzedAt,
    int FindingCount);
