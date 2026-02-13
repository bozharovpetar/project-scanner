using CodeScanner.Api.Models.Enums;

namespace CodeScanner.Api.Models.Entities;

public class Finding
{
    public int Id { get; set; }
    public int ScanFileId { get; set; }
    public FindingCategory Category { get; set; }
    public Severity Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? LineStart { get; set; }
    public int? LineEnd { get; set; }
    public string? CodeSnippet { get; set; }
    public string? Suggestion { get; set; }

    public ScanFile ScanFile { get; set; } = null!;
}
