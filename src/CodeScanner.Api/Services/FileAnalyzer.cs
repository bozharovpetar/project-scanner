using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CodeScanner.Api.Models.Entities;
using CodeScanner.Api.Models.Enums;

namespace CodeScanner.Api.Services;

public class FileAnalyzer : IFileAnalyzer
{
    private readonly IOllamaClient _ollamaClient;
    private readonly IConfiguration _config;
    private readonly ILogger<FileAnalyzer> _logger;

    public FileAnalyzer(IOllamaClient ollamaClient, IConfiguration config, ILogger<FileAnalyzer> logger)
    {
        _ollamaClient = ollamaClient;
        _config = config;
        _logger = logger;
    }

    public async Task<List<Finding>> AnalyzeFileAsync(string filePath, string fileContent, string language, CancellationToken ct = default)
    {
        var maxChars = _config.GetValue("Scanner:MaxContentChars", 80000);
        var allFindings = new List<Finding>();

        if (fileContent.Length <= maxChars)
        {
            var findings = await AnalyzeChunkAsync(filePath, fileContent, language, ct);
            allFindings.AddRange(findings);
        }
        else
        {
            var chunks = SplitIntoChunks(fileContent, maxChars);
            _logger.LogInformation("File {Path} split into {Count} chunks", filePath, chunks.Count);

            foreach (var chunk in chunks)
            {
                var findings = await AnalyzeChunkAsync(filePath, chunk, language, ct);
                allFindings.AddRange(findings);
            }

            allFindings = DeduplicateFindings(allFindings);
        }

        return allFindings;
    }

    private async Task<List<Finding>> AnalyzeChunkAsync(string filePath, string content, string language, CancellationToken ct)
    {
        var prompt = PromptTemplates.BuildAnalysisPrompt(filePath, language, content);
        var rawResponse = await _ollamaClient.GenerateAsync(prompt, ct);

        return ParseResponse(rawResponse, filePath);
    }

    private List<Finding> ParseResponse(string rawResponse, string filePath)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<OllamaAnalysisResponse>(rawResponse, JsonOptions);
            if (parsed?.Findings is null)
                return [];

            return parsed.Findings
                .Select(f => MapToFinding(f, filePath))
                .Where(f => f is not null)
                .Cast<Finding>()
                .ToList();
        }
        catch (JsonException)
        {
            // Try to extract JSON from markdown code fences
            var match = Regex.Match(rawResponse, @"```(?:json)?\s*([\s\S]*?)```");
            if (match.Success)
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<OllamaAnalysisResponse>(match.Groups[1].Value.Trim(), JsonOptions);
                    if (parsed?.Findings is not null)
                    {
                        return parsed.Findings
                            .Select(f => MapToFinding(f, filePath))
                            .Where(f => f is not null)
                            .Cast<Finding>()
                            .ToList();
                    }
                }
                catch (JsonException) { }
            }

            _logger.LogWarning("Failed to parse Ollama response for {FilePath}. Raw: {Response}",
                filePath, rawResponse[..Math.Min(rawResponse.Length, 500)]);
            return [];
        }
    }

    private static Finding? MapToFinding(OllamaFinding f, string filePath)
    {
        if (string.IsNullOrWhiteSpace(f.Title))
            return null;

        return new Finding
        {
            Category = ParseEnum<FindingCategory>(f.Category, FindingCategory.LintError),
            Severity = ParseEnum<Severity>(f.Severity, Models.Enums.Severity.Medium),
            Title = f.Title,
            Description = f.Description ?? string.Empty,
            LineStart = f.LineStart,
            LineEnd = f.LineEnd,
            CodeSnippet = f.CodeSnippet,
            Suggestion = f.Suggestion
        };
    }

    private static T ParseEnum<T>(string? value, T defaultValue) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        return Enum.TryParse<T>(value, ignoreCase: true, out var result) ? result : defaultValue;
    }

    private static List<Finding> DeduplicateFindings(List<Finding> findings)
    {
        return findings
            .GroupBy(f => new { f.Title, f.LineStart, f.Category })
            .Select(g => g.First())
            .ToList();
    }

    private static List<string> SplitIntoChunks(string content, int maxChars)
    {
        var lines = content.Split('\n');
        var chunks = new List<string>();
        var currentChunk = new List<string>();
        var currentLength = 0;
        const int overlapLines = 20;

        foreach (var line in lines)
        {
            if (currentLength + line.Length + 1 > maxChars && currentChunk.Count > 0)
            {
                chunks.Add(string.Join('\n', currentChunk));

                // Keep last N lines for overlap
                var overlap = currentChunk.Skip(Math.Max(0, currentChunk.Count - overlapLines)).ToList();
                currentChunk = new List<string>(overlap);
                currentLength = currentChunk.Sum(l => l.Length + 1);
            }

            currentChunk.Add(line);
            currentLength += line.Length + 1;
        }

        if (currentChunk.Count > 0)
            chunks.Add(string.Join('\n', currentChunk));

        return chunks;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}

internal class OllamaAnalysisResponse
{
    [JsonPropertyName("findings")]
    public List<OllamaFinding>? Findings { get; set; }
}

internal class OllamaFinding
{
    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("severity")]
    public string? Severity { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("lineStart")]
    public int? LineStart { get; set; }

    [JsonPropertyName("lineEnd")]
    public int? LineEnd { get; set; }

    [JsonPropertyName("codeSnippet")]
    public string? CodeSnippet { get; set; }

    [JsonPropertyName("suggestion")]
    public string? Suggestion { get; set; }
}
