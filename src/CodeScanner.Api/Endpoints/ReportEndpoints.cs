using CodeScanner.Api.Models.Dtos;
using CodeScanner.Api.Models.Enums;
using CodeScanner.Api.Services;

namespace CodeScanner.Api.Endpoints;

public static class ReportEndpoints
{
    public static void MapReportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/scans/{scanId:int}").WithTags("Reports");

        group.MapGet("/findings", async (
            int scanId,
            IScanService scanService,
            string? category = null,
            string? severity = null,
            int page = 1,
            int pageSize = 50,
            CancellationToken ct = default) =>
        {
            FindingCategory? cat = null;
            if (!string.IsNullOrEmpty(category) && Enum.TryParse<FindingCategory>(category, true, out var parsedCat))
                cat = parsedCat;

            Severity? sev = null;
            if (!string.IsNullOrEmpty(severity) && Enum.TryParse<Severity>(severity, true, out var parsedSev))
                sev = parsedSev;

            var findings = await scanService.GetFindingsAsync(scanId, cat, sev, page, pageSize, ct);
            return Results.Ok(findings);
        })
        .WithName("GetFindings")
        .WithDescription("Get findings for a scan with optional category/severity filtering");

        group.MapGet("/files", async (
            int scanId,
            IScanService scanService,
            bool? hasFindings = null,
            CancellationToken ct = default) =>
        {
            var files = await scanService.GetFilesAsync(scanId, hasFindings, ct);
            return Results.Ok(files);
        })
        .WithName("GetScanFiles")
        .WithDescription("List files in a scan");

        group.MapGet("/files/{fileId:int}", async (
            int scanId,
            int fileId,
            IScanService scanService,
            CancellationToken ct) =>
        {
            var file = await scanService.GetFileWithFindingsAsync(scanId, fileId, ct);
            if (file is null)
                return Results.NotFound();

            return Results.Ok(new
            {
                file.Id,
                file.RelativePath,
                file.Language,
                file.FileSizeBytes,
                file.Analyzed,
                file.AnalyzedAt,
                file.ErrorMessage,
                Findings = file.Findings.Select(f => new FindingResponse(
                    f.Id, file.RelativePath, f.Category.ToString(), f.Severity.ToString(),
                    f.Title, f.Description, f.LineStart, f.LineEnd, f.CodeSnippet, f.Suggestion))
            });
        })
        .WithName("GetScanFile")
        .WithDescription("Get a specific file with all its findings");

        group.MapGet("/summary", async (
            int scanId,
            IScanService scanService,
            CancellationToken ct) =>
        {
            var summary = await scanService.GetSummaryAsync(scanId, ct);
            return Results.Ok(summary);
        })
        .WithName("GetScanSummary")
        .WithDescription("Get aggregate finding counts by category and severity");
    }
}
