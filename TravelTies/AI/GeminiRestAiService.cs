using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TravelTies.AI;

public sealed class GeminiRestAiService : IAiService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly ILogger<GeminiRestAiService> _logger;

    // Dùng v1 + model cố định
    private const string Model = "gemini-2.5-flash";
    private string Endpoint => $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent?key={_apiKey}";

    public GeminiRestAiService(HttpClient http, IConfiguration cfg, ILogger<GeminiRestAiService> logger)
    {
        _http = http;
        _logger = logger;
        _apiKey = cfg["GoogleAI:ApiKey"]
                  ?? cfg["GOOGLE_API_KEY"]
                  ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY")
                  ?? throw new InvalidOperationException("Missing GoogleAI API key.");
        _http.Timeout = TimeSpan.FromSeconds(20);
    }

    public async Task<string> AskAsync(string userMessage, Guid userId)
    {
        var payload = new
        {
            systemInstruction = new
            {
                role = "system",
                parts = new[]
                {
                new { text = AiAssistant.SystemPrompt },
                new { text = $"Dữ liệu tour hiện tại:\n{TourDataUpdater.LatestTourData}" } // thêm dòng này
            }
            },
            contents = new[]
            {
            new
            {
                role = "user",
                parts = new[]
                {
                    new { text = $"UserId: {userId}\n\n{userMessage}" }
                }
            }
        },
            generationConfig = new
            {
                temperature = 0.6,
                topK = 40,
                topP = 0.95,
                maxOutputTokens = 1024
            }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = JsonContent.Create(payload, options: new JsonSerializerOptions
            {
                PropertyNamingPolicy = null // important to preserve camelCase keys
            })
        };

        using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        var body = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
        {
            _logger.LogError("Gemini API error. Status: {Status}. Url: {Url}. Body: {Body}",
                res.StatusCode, Endpoint, body);
            // trả lời mềm cho UI
            return "Xin lỗi, hiện mình chưa trả lời được.";
        }

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("candidates", out var cands) || cands.GetArrayLength() == 0)
            return "Xin lỗi, hiện mình chưa trả lời được.";

        var parts = cands[0].GetProperty("content").GetProperty("parts");
        var text = string.Join("\n",
            parts.EnumerateArray()
                 .Select(p => p.TryGetProperty("text", out var t) ? t.GetString() : null)
                 .Where(x => !string.IsNullOrWhiteSpace(x)));

        return string.IsNullOrWhiteSpace(text) ? "Xin lỗi, hiện mình chưa trả lời được." : text.Trim();
    }
}
