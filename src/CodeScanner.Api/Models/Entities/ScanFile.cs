namespace CodeScanner.Api.Models.Entities;

public class ScanFile
{
    public int Id { get; set; }
    public int ScanId { get; set; }
    public string RelativePath { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public bool Analyzed { get; set; }
    public DateTime? AnalyzedAt { get; set; }
    public string? ErrorMessage { get; set; }

    public Scan Scan { get; set; } = null!;
    public List<Finding> Findings { get; set; } = [];
}
