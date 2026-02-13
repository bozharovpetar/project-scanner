using CodeScanner.Api.Models.Entities;

namespace CodeScanner.Api.Services;

public interface IFileAnalyzer
{
    Task<List<Finding>> AnalyzeFileAsync(string filePath, string fileContent, string language, CancellationToken ct = default);
}
