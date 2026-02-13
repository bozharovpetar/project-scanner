namespace CodeScanner.Api.Services;

public interface IOllamaClient
{
    Task<string> GenerateAsync(string prompt, CancellationToken ct = default);
}
