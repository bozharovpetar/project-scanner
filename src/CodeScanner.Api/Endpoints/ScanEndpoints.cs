using System.Text.Json;
using System.Threading.Channels;
using CodeScanner.Api.Models.Dtos;
using CodeScanner.Api.Services;

namespace CodeScanner.Api.Endpoints;

public static class ScanEndpoints
{
    public static void MapScanEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/scans").WithTags("Scans");

        group.MapPost("/", async (CreateScanRequest request, IScanService scanService, CancellationToken ct) =>
        {
            try
            {
                var scan = await scanService.CreateScanAsync(request.ProjectPath, ct);
                return Results.Accepted($"/api/scans/{scan.Id}", new ScanResponse(
                    scan.Id, scan.ProjectPath, scan.Status.ToString(),
                    scan.CreatedAt, scan.CompletedAt, scan.TotalFiles, scan.ProcessedFiles));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("CreateScan")
        .WithDescription("Create a new code scan for a project directory");

        group.MapGet("/", async (IScanService scanService, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        {
            var scans = await scanService.ListScansAsync(page, pageSize, ct);
            return Results.Ok(scans.Select(s => new ScanResponse(
                s.Id, s.ProjectPath, s.Status.ToString(),
                s.CreatedAt, s.CompletedAt, s.TotalFiles, s.ProcessedFiles)));
        })
        .WithName("ListScans")
        .WithDescription("List all scans with pagination");

        group.MapGet("/{id:int}", async (int id, IScanService scanService, CancellationToken ct) =>
        {
            var scan = await scanService.GetScanAsync(id, ct);
            if (scan is null)
                return Results.NotFound();

            var summary = await scanService.GetSummaryAsync(id, ct);
            return Results.Ok(new ScanDetailResponse(
                scan.Id, scan.ProjectPath, scan.Status.ToString(),
                scan.CreatedAt, scan.CompletedAt, scan.TotalFiles, scan.ProcessedFiles,
                scan.ErrorMessage, summary));
        })
        .WithName("GetScan")
        .WithDescription("Get scan details with summary");

        group.MapDelete("/{id:int}", async (int id, IScanService scanService, CancellationToken ct) =>
        {
            await scanService.DeleteScanAsync(id, ct);
            return Results.NoContent();
        })
        .WithName("DeleteScan")
        .WithDescription("Delete a scan and all related data");

        group.MapGet("/{id:int}/progress", async (int id, ScanProgressBroadcaster broadcaster, HttpContext ctx, CancellationToken ct) =>
        {
            ctx.Response.ContentType = "text/event-stream";
            ctx.Response.Headers.CacheControl = "no-cache";
            ctx.Response.Headers.Connection = "keep-alive";

            var channel = Channel.CreateUnbounded<ScanProgressEvent>();
            using var sub = broadcaster.Subscribe(id, channel);

            try
            {
                await foreach (var evt in channel.Reader.ReadAllAsync(ct))
                {
                    var json = JsonSerializer.Serialize(evt, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    await ctx.Response.WriteAsync($"data: {json}\n\n", ct);
                    await ctx.Response.Body.FlushAsync(ct);
                }
            }
            catch (OperationCanceledException) { }
        })
        .WithName("ScanProgress")
        .WithDescription("Stream scan progress via Server-Sent Events");
    }
}
