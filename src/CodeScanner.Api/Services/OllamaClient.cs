using System.Text.Json;

namespace CodeScanner.Api.Services;

public class OllamaClient : IOllamaClient
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly ILogger<OllamaClient> _logger;

    public OllamaClient(HttpClient httpClient, IConfiguration config, ILogger<OllamaClient> logger)
    {
        _httpClient = httpClient;
        _model = config.GetValue<string>("Ollama:Model") ?? "qwen2.5-coder:7b";
        _logger = logger;
    }

    public async Task<string> GenerateAsync(string prompt, CancellationToken ct = default)
    {
        var request = new
        {
            model = _model,
            prompt,
            stream = false,
            format = "json"
        };

        _logger.LogDebug("Sending prompt to Ollama ({Model}), length: {Length} chars", _model, prompt.Length);

        var response = await _httpClient.PostAsJsonAsync("/api/generate", request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var result = json.GetProperty("response").GetString() ?? "{}";

        _logger.LogDebug("Received Ollama response, length: {Length} chars", result.Length);
        return result;
    }
}
