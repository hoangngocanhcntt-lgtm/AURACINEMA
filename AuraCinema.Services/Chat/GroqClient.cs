using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AuraCinema.Domain.Interfaces.Services;
using AuraCinema.Domain.Models.Chat;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuraCinema.Services.Chat;

public class GroqClient : ILlmClient
{
    private readonly HttpClient _httpClient;
    private readonly LlmOptions _options;
    private readonly ILogger<GroqClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public GroqClient(HttpClient httpClient, IOptions<LlmOptions> options, ILogger<GroqClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
    }

    public async Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        
        var jsonContent = JsonSerializer.Serialize(request, _jsonOptions);
        httpRequest.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                throw new InvalidOperationException("Bé Aura đang quá tải, bạn thử lại sau ít phút nhé!");
            }
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || 
                response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                throw new InvalidOperationException("Lỗi cấu hình API key.");
            }

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Groq API error: {StatusCode} {ErrorBody}", response.StatusCode, errorBody);
            response.EnsureSuccessStatusCode();
        }

        var result = await response.Content.ReadFromJsonAsync<LlmResponse>(_jsonOptions, cancellationToken);
        return result ?? new LlmResponse();
    }
}
