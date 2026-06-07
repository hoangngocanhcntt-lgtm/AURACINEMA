using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AuraCinema.Domain.Interfaces.Services;
using AuraCinema.Domain.Models.Chat;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuraCinema.Services.Chat;

public class GeminiClient : ILlmClient
{
    private readonly HttpClient _httpClient;
    private readonly LlmOptions _options;
    private readonly ApiKeyRotator _keyRotator;
    private readonly ILogger<GeminiClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public GeminiClient(HttpClient httpClient, IOptions<LlmOptions> options, ApiKeyRotator keyRotator, ILogger<GeminiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _keyRotator = keyRotator;
        _logger = logger;
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        int maxRetries = _keyRotator.TotalKeyCount;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            var apiKey = _keyRotator.GetNextKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new RateLimitedException("Tất cả API key đang bị rate limit.");
            }

            var geminiReq = MapToGeminiRequest(request);
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{request.Model}:generateContent?key={apiKey}";

            var response = await _httpClient.PostAsJsonAsync(url, geminiReq, _jsonOptions, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var geminiResp = await response.Content.ReadFromJsonAsync<GeminiResponse>(_jsonOptions, cancellationToken);
                return MapFromGeminiResponse(geminiResp!);
            }

            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Gemini API Error: {StatusCode} {Error}", response.StatusCode, error);
            
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                _keyRotator.MarkRateLimited(apiKey, TimeSpan.FromSeconds(60));
                continue; // Retry with next key
            }
            
            if (response.StatusCode >= System.Net.HttpStatusCode.InternalServerError)
            {
                // Wait briefly and try another key just in case it's a regional issue with that key/project
                continue;
            }
            
            throw new Exception($"Gemini API returned {response.StatusCode}: {error}");
        }

        throw new RateLimitedException("Gemini Rate Limit - all keys exhausted.");
    }

    private GeminiRequest MapToGeminiRequest(LlmRequest request)
    {
        var geminiReq = new GeminiRequest();
        
        var contents = new List<GeminiContent>();
        GeminiContent? systemInstruction = null;

        foreach (var msg in request.Messages)
        {
            if (msg.Role == "system")
            {
                systemInstruction = new GeminiContent
                {
                    Role = "user", // System instruction doesn't use role in the same way, but it's part of a separate field
                    Parts = new List<GeminiPart> { new GeminiPart { Text = msg.Content } }
                };
            }
            else if (msg.Role == "user")
            {
                contents.Add(new GeminiContent
                {
                    Role = "user",
                    Parts = new List<GeminiPart> { new GeminiPart { Text = msg.Content } }
                });
            }
            else if (msg.Role == "assistant")
            {
                var parts = new List<GeminiPart>();
                if (!string.IsNullOrEmpty(msg.Content))
                {
                    parts.Add(new GeminiPart { Text = msg.Content });
                }
                
                if (msg.ToolCalls != null)
                {
                    foreach (var tc in msg.ToolCalls)
                    {
                        parts.Add(new GeminiPart
                        {
                            FunctionCall = new GeminiFunctionCall
                            {
                                Name = tc.Function.Name,
                                Args = JsonSerializer.Deserialize<Dictionary<string, object>>(tc.Function.Arguments)
                            }
                        });
                    }
                }

                contents.Add(new GeminiContent
                {
                    Role = "model",
                    Parts = parts
                });
            }
            else if (msg.Role == "tool")
            {
                // Parse the tool output which is JSON
                object parsedResponse = new { error = "No content" };
                if (!string.IsNullOrEmpty(msg.Content))
                {
                    try
                    {
                        parsedResponse = JsonSerializer.Deserialize<object>(msg.Content)!;
                    }
                    catch
                    {
                        parsedResponse = new { output = msg.Content };
                    }
                }

                contents.Add(new GeminiContent
                {
                    Role = "function",
                    Parts = new List<GeminiPart>
                    {
                        new GeminiPart
                        {
                            FunctionResponse = new GeminiFunctionResponse
                            {
                                Name = msg.Name ?? "unknown",
                                Response = parsedResponse
                            }
                        }
                    }
                });
            }
        }

        geminiReq.Contents = contents;
        geminiReq.SystemInstruction = systemInstruction;

        if (request.Tools != null && request.Tools.Count > 0)
        {
            geminiReq.Tools = new List<GeminiTool>
            {
                new GeminiTool
                {
                    FunctionDeclarations = request.Tools.Select(t => new GeminiFunctionDeclaration
                    {
                        Name = t.Function.Name,
                        Description = t.Function.Description,
                        Parameters = t.Function.Parameters
                    }).ToList()
                }
            };
        }

        // Configuration
        geminiReq.GenerationConfig = new GeminiGenerationConfig
        {
            Temperature = request.Temperature,
            TopP = request.TopP,
            MaxOutputTokens = request.MaxTokens
        };

        return geminiReq;
    }

    private LlmResponse MapFromGeminiResponse(GeminiResponse geminiResp)
    {
        var resp = new LlmResponse();

        if (geminiResp.Candidates == null || geminiResp.Candidates.Count == 0)
            return resp;

        var candidate = geminiResp.Candidates[0];
        var msg = new LlmMessage
        {
            Role = "assistant"
        };

        var textParts = candidate.Content?.Parts?.Where(p => p.Text != null).Select(p => p.Text);
        if (textParts != null && textParts.Any())
        {
            msg.Content = string.Join("\n", textParts);
        }

        var functionCalls = candidate.Content?.Parts?.Where(p => p.FunctionCall != null).Select(p => p.FunctionCall);
        if (functionCalls != null && functionCalls.Any())
        {
            msg.ToolCalls = new List<LlmToolCall>();
            int i = 0;
            foreach (var fc in functionCalls)
            {
                msg.ToolCalls.Add(new LlmToolCall
                {
                    Id = $"call_{Guid.NewGuid().ToString("N").Substring(0, 8)}",
                    Type = "function",
                    Function = new LlmFunctionCall
                    {
                        Name = fc!.Name!,
                        Arguments = JsonSerializer.Serialize(fc.Args)
                    }
                });
                i++;
            }
        }

        resp.Choices.Add(new LlmChoice
        {
            Message = msg,
            FinishReason = candidate.FinishReason
        });

        return resp;
    }

    // --- Gemini API Models ---

    private class GeminiRequest
    {
        public GeminiContent? SystemInstruction { get; set; }
        public List<GeminiContent>? Contents { get; set; }
        public List<GeminiTool>? Tools { get; set; }
        public GeminiGenerationConfig? GenerationConfig { get; set; }
    }

    private class GeminiContent
    {
        public string? Role { get; set; }
        public List<GeminiPart>? Parts { get; set; }
    }

    private class GeminiPart
    {
        public string? Text { get; set; }
        public GeminiFunctionCall? FunctionCall { get; set; }
        public GeminiFunctionResponse? FunctionResponse { get; set; }
    }

    private class GeminiFunctionCall
    {
        public string? Name { get; set; }
        public Dictionary<string, object>? Args { get; set; }
    }

    private class GeminiFunctionResponse
    {
        public string? Name { get; set; }
        public object? Response { get; set; }
    }

    private class GeminiTool
    {
        public List<GeminiFunctionDeclaration>? FunctionDeclarations { get; set; }
    }

    private class GeminiFunctionDeclaration
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public object? Parameters { get; set; }
    }

    private class GeminiGenerationConfig
    {
        public double? Temperature { get; set; }
        public double? TopP { get; set; }
        public int? MaxOutputTokens { get; set; }
    }

    private class GeminiResponse
    {
        public List<GeminiCandidate>? Candidates { get; set; }
    }

    private class GeminiCandidate
    {
        public GeminiContent? Content { get; set; }
        public string? FinishReason { get; set; }
    }
}
