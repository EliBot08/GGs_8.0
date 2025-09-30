using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GGs.Server.Services;

public interface ILlmProvider
{
    Task<string> GenerateAsync(string prompt, CancellationToken ct = default);
}

public sealed class OpenAiLlmProvider : ILlmProvider
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _cfg;
    private readonly ILogger<OpenAiLlmProvider> _logger;

    public OpenAiLlmProvider(IHttpClientFactory httpFactory, IConfiguration cfg, ILogger<OpenAiLlmProvider> logger)
    {
        _httpFactory = httpFactory;
        _cfg = cfg;
        _logger = logger;
    }

    public async Task<string> GenerateAsync(string prompt, CancellationToken ct = default)
    {
        var baseUrl = _cfg["Eli:OpenAI:BaseUrl"] ?? "https://api.openai.com";
        var apiKey = _cfg["Eli:OpenAI:ApiKey"] ?? string.Empty;
        var model = _cfg["Eli:OpenAI:Model"] ?? "gpt-4o-mini";

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("LLM API key not configured. Returning fallback message.");
            return "EliBot: AI backend is not configured.";
        }

        var http = _httpFactory.CreateClient("openai");
        http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        http.DefaultRequestHeaders.Accept.ParseAdd("application/json");

        var body = new
        {
            model,
            messages = new object[]
            {
                new { role = "system", content = "You are EliBot, a helpful Windows gaming optimization assistant." },
                new { role = "user", content = prompt }
            },
            temperature = 0.2,
            max_tokens = 400
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        using var res = await http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("LLM request failed: {Status} {Body}", res.StatusCode, err);
            return "EliBot: The AI service is temporarily unavailable.";
        }
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        try
        {
            var text = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            return text ?? "";
        }
        catch
        {
            return "EliBot: The AI response could not be parsed.";
        }
    }
}

public sealed class AnthropicLlmProvider : ILlmProvider
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _cfg;
    private readonly ILogger<AnthropicLlmProvider> _logger;

    public AnthropicLlmProvider(IHttpClientFactory httpFactory, IConfiguration cfg, ILogger<AnthropicLlmProvider> logger)
    {
        _httpFactory = httpFactory;
        _cfg = cfg;
        _logger = logger;
    }

    public async Task<string> GenerateAsync(string prompt, CancellationToken ct = default)
    {
        var baseUrl = _cfg["Eli:Anthropic:BaseUrl"] ?? "https://api.anthropic.com";
        var apiKey = _cfg["Eli:Anthropic:ApiKey"] ?? string.Empty;
        var model = _cfg["Eli:Anthropic:Model"] ?? "claude-3-5-sonnet-latest";

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Anthropic API key not configured. Returning fallback message.");
            return "EliBot: AI backend is not configured.";
        }

        var http = _httpFactory.CreateClient("anthropic");
        http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        http.DefaultRequestHeaders.Add("x-api-key", apiKey);
        http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/json");

        var body = new
        {
            model,
            max_tokens = 500,
            messages = new object[]
            {
                new { role = "user", content = prompt }
            }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "v1/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        using var res = await http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Anthropic request failed: {Status} {Body}", res.StatusCode, err);
            return "EliBot: The AI service is temporarily unavailable.";
        }
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        try
        {
            var text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString();
            return text ?? "";
        }
        catch
        {
            return "EliBot: The AI response could not be parsed.";
        }
    }
}

public sealed class CompositeLlmProvider : ILlmProvider
{
    private readonly OpenAiLlmProvider _openai;
    private readonly AnthropicLlmProvider _anthropic;
    private readonly IConfiguration _cfg;
    public CompositeLlmProvider(OpenAiLlmProvider openai, AnthropicLlmProvider anthropic, IConfiguration cfg)
    {
        _openai = openai; _anthropic = anthropic; _cfg = cfg;
    }
    public async Task<string> GenerateAsync(string prompt, CancellationToken ct = default)
    {
        var provider = _cfg["Eli:Provider"]?.ToLowerInvariant() ?? "openai";
        if (provider == "anthropic") return await _anthropic.GenerateAsync(prompt, ct);
        return await _openai.GenerateAsync(prompt, ct);
    }
}


