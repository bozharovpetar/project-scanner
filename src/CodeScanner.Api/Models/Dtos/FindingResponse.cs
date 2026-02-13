namespace CodeScanner.Api.Models.Dtos;

public record FindingResponse(
    int Id,
    string FilePath,
    string Category,
    string Severity,
    string Title,
    string Description,
    int? LineStart,
    int? LineEnd,
    string? CodeSnippet,
    string? Suggestion);
