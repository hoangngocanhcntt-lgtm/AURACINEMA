using System.Net;
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

    private const int MAX_RETRIES = 3;

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
        var jsonContent = JsonSerializer.Serialize(request, _jsonOptions);

        for (int attempt = 0; attempt <= MAX_RETRIES; attempt++)
        {
            // Phải tạo HttpRequestMessage mới mỗi lần vì .NET không cho gửi lại message đã sent
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            httpRequest.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

            // ===== 429 Rate Limit — Retry với backoff =====
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                if (attempt < MAX_RETRIES)
                {
                    var delay = GetRetryDelay(response, attempt);
                    _logger.LogWarning("Groq 429 rate limited. Retry {Attempt}/{Max} sau {Delay}ms", 
                        attempt + 1, MAX_RETRIES, delay.TotalMilliseconds);
                    await Task.Delay(delay, cancellationToken);
                    continue;
                }
                // Đã hết retry → throw
                throw new InvalidOperationException("Bé Aura đang quá tải, bạn thử lại sau ít phút nhé!");
            }

            // ===== Các lỗi khác =====
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized ||
                    response.StatusCode == HttpStatusCode.Forbidden)
                {
                    throw new InvalidOperationException("Lỗi cấu hình API key.");
                }

                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.StatusCode == HttpStatusCode.BadRequest &&
                    errorBody.Contains("tool_use_failed", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Groq tool_use_failed: {ErrorBody}", errorBody);
                    throw new GroqToolUseFailedException();
                }

                _logger.LogError("Groq API error: {StatusCode} {ErrorBody}", response.StatusCode, errorBody);
                response.EnsureSuccessStatusCode();
            }

            // ===== Thành công =====
            var result = await response.Content.ReadFromJsonAsync<LlmResponse>(_jsonOptions, cancellationToken);
            return result ?? new LlmResponse();
        }

        // Không nên đến đây nhưng phòng hờ
        throw new InvalidOperationException("Bé Aura đang quá tải, bạn thử lại sau ít phút nhé!");
    }

    /// <summary>
    /// Tính thời gian chờ trước khi retry:
    /// 1) Ưu tiên đọc header retry-after từ Groq
    /// 2) Nếu không có → dùng exponential backoff: 2s, 4s, 8s
    /// </summary>
    private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        if (response.Headers.TryGetValues("retry-after", out var values))
        {
            var retryAfter = values.FirstOrDefault();
            if (double.TryParse(retryAfter, out var seconds) && seconds > 0 && seconds <= 30)
            {
                return TimeSpan.FromSeconds(seconds + 0.5); // +0.5s buffer
            }
        }
        // Exponential backoff: 2s, 4s, 8s
        return TimeSpan.FromSeconds(Math.Pow(2, attempt + 1));
    }
}

/// <summary>Groq từ chối tool call do model sinh sai định dạng (HTTP 400 tool_use_failed).</summary>
public class GroqToolUseFailedException : Exception
{
    public GroqToolUseFailedException() : base("Groq tool_use_failed") { }
}
