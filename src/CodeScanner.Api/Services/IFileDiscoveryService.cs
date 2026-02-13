namespace CodeScanner.Api.Services;

public record DiscoveredFile(string AbsolutePath, string RelativePath, string Language, long SizeBytes);

public interface IFileDiscoveryService
{
    Task<List<DiscoveredFile>> DiscoverFilesAsync(string projectPath, CancellationToken ct = default);
}
