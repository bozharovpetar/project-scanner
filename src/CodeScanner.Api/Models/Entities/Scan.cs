using CodeScanner.Api.Models.Enums;

namespace CodeScanner.Api.Models.Entities;

public class Scan
{
    public int Id { get; set; }
    public string ProjectPath { get; set; } = string.Empty;
    public ScanStatus Status { get; set; } = ScanStatus.Queued;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public int TotalFiles { get; set; }
    public int ProcessedFiles { get; set; }
    public string? ErrorMessage { get; set; }

    public List<ScanFile> Files { get; set; } = [];
}
