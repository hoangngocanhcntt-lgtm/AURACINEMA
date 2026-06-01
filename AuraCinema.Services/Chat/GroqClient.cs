using System.Collections.Concurrent;
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
    private readonly ApiKeyRotator _keyRotator;
    private readonly ILogger<GroqClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    // Giới hạn số request đồng thời đến Groq toàn server
    private static readonly SemaphoreSlim ConcurrencyGate = new(5, 5);

    private static readonly Random Jitter = new();

    // ====== MODEL-LEVEL COOLDOWN ======
    // Ghi nhớ model nào vừa bị 429 → bỏ qua ngay, không tốn request lãng phí.
    // Key = model name, Value = thời điểm hết cooldown (UTC).
    private static readonly ConcurrentDictionary<string, DateTime> ModelCooldowns = new();

    // Danh sách model ưu tiên thử: model nhỏ hơn có rate limit cao hơn trên Groq free tier
    private static readonly string[] AllModels = new[]
    {
        "llama-3.3-70b-versatile",   // Thông minh nhất, rate limit thấp nhất
        "llama-3.1-8b-instant",      // Nhanh, rate limit cao
        "gemma2-9b-it",              // Backup
    };

    public GroqClient(HttpClient httpClient, IOptions<LlmOptions> options, ApiKeyRotator keyRotator, ILogger<GroqClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _keyRotator = keyRotator;
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
        // Chờ slot concurrency (timeout 30s)
        if (!await ConcurrencyGate.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken))
        {
            throw new RateLimitedException("Bé Aura đang bận trả lời người khác, bạn thử lại sau chút nhé!");
        }

        try
        {
            // Xây danh sách model cần thử: model chính trước, rồi fallback
            var modelsToTry = BuildModelList(request.Model);

            foreach (var model in modelsToTry)
            {
                // Kiểm tra cooldown — nếu model vừa bị 429 gần đây → BỎ QUA NGAY
                if (IsModelInCooldown(model))
                {
                    _logger.LogDebug("Model {Model} đang cooldown, bỏ qua.", model);
                    continue;
                }

                var req = CloneRequestWithModel(request, model);
                var result = await TrySingleRequestAsync(req, cancellationToken);

                if (result != null)
                    return result;
            }

            // Tất cả model đều đang cooldown hoặc thất bại → throw
            throw new RateLimitedException("Bé Aura đang nghỉ ngơi, bạn quay lại sau ít phút nhé!");
        }
        finally
        {
            ConcurrencyGate.Release();
        }
    }

    /// <summary>
    /// Gửi MỘT request duy nhất đến Groq. Không retry nhiều lần cùng model.
    /// Trả null nếu bị 429 (để chuyển sang model khác nhanh chóng).
    /// </summary>
    private async Task<LlmResponse?> TrySingleRequestAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        var apiKey = _keyRotator.GetNextKey();
        if (string.IsNullOrEmpty(apiKey))
            return null;

        var jsonContent = JsonSerializer.Serialize(request, _jsonOptions);

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpRequest.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HTTP error khi gọi Groq, model {Model}", request.Model);
            return null;
        }

        // ===== 429 Rate Limit → đánh dấu model cooldown, chuyển model khác ngay =====
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var cooldown = GetCooldownFromResponse(response);
            MarkModelCooldown(request.Model, cooldown);
            _keyRotator.MarkRateLimited(apiKey, cooldown);

            _logger.LogWarning("Model {Model} bị 429. Cooldown {Seconds}s → chuyển model khác ngay.",
                request.Model, cooldown.TotalSeconds);
            return null; // Chuyển sang model tiếp theo, KHÔNG retry
        }

        // ===== Auth errors =====
        if (response.StatusCode == HttpStatusCode.Unauthorized ||
            response.StatusCode == HttpStatusCode.Forbidden)
        {
            _keyRotator.MarkRateLimited(apiKey, TimeSpan.FromMinutes(30));
            _logger.LogError("API key ***{KeySuffix} bị Unauthorized/Forbidden", apiKey[^4..]);
            throw new InvalidOperationException("Lỗi cấu hình API key Groq.");
        }

        // ===== Các lỗi khác =====
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == HttpStatusCode.BadRequest &&
                errorBody.Contains("tool_use_failed", StringComparison.OrdinalIgnoreCase))
            {
                throw new GroqToolUseFailedException();
            }

            // Model không khả dụng → skip
            if (response.StatusCode == HttpStatusCode.BadRequest &&
                (errorBody.Contains("model_not_found", StringComparison.OrdinalIgnoreCase) ||
                 errorBody.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
                 errorBody.Contains("model_not_active", StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogWarning("Model {Model} không khả dụng trên Groq, bỏ qua.", request.Model);
                MarkModelCooldown(request.Model, TimeSpan.FromMinutes(10));
                return null;
            }

            // Server error → skip model
            if ((int)response.StatusCode >= 500)
            {
                _logger.LogWarning("Groq 5xx cho model {Model}: {StatusCode}", request.Model, response.StatusCode);
                return null;
            }

            _logger.LogError("Groq API error: {StatusCode} {ErrorBody}", response.StatusCode, errorBody);
            return null;
        }

        // ===== Thành công =====
        var result = await response.Content.ReadFromJsonAsync<LlmResponse>(_jsonOptions, cancellationToken);
        return result ?? new LlmResponse();
    }

    // ====== HELPERS ======

    /// <summary>Xây danh sách model: model chính trước, rồi fallback (loại trùng).</summary>
    private List<string> BuildModelList(string primaryModel)
    {
        var list = new List<string> { primaryModel };
        foreach (var m in AllModels)
        {
            if (!string.Equals(m, primaryModel, StringComparison.OrdinalIgnoreCase))
                list.Add(m);
        }
        return list;
    }

    /// <summary>Kiểm tra model có đang bị cooldown không.</summary>
    private static bool IsModelInCooldown(string model)
    {
        return ModelCooldowns.TryGetValue(model, out var until) && DateTime.UtcNow < until;
    }

    /// <summary>Đánh dấu model bị cooldown.</summary>
    private static void MarkModelCooldown(string model, TimeSpan duration)
    {
        ModelCooldowns[model] = DateTime.UtcNow + duration;
    }

    private static LlmRequest CloneRequestWithModel(LlmRequest original, string newModel)
    {
        if (string.Equals(original.Model, newModel, StringComparison.OrdinalIgnoreCase))
            return original; // Không cần clone nếu cùng model

        return new LlmRequest
        {
            Model = newModel,
            Messages = original.Messages,
            Tools = original.Tools,
            ToolChoice = original.ToolChoice,
            Temperature = original.Temperature,
            TopP = original.TopP,
            MaxTokens = original.MaxTokens
        };
    }

    /// <summary>
    /// Đọc cooldown từ response header hoặc dùng mặc định 60s.
    /// Groq free tier thường rate-limit theo phút nên 60s là hợp lý.
    /// </summary>
    private static TimeSpan GetCooldownFromResponse(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("retry-after", out var values))
        {
            var retryAfter = values.FirstOrDefault();
            if (double.TryParse(retryAfter, out var seconds) && seconds > 0 && seconds <= 120)
            {
                return TimeSpan.FromSeconds(seconds + 1); // +1s buffer
            }
        }
        return TimeSpan.FromSeconds(60); // Mặc định: cooldown 60s
    }
}

/// <summary>Groq từ chối tool call do model sinh sai định dạng (HTTP 400 tool_use_failed).</summary>
public class GroqToolUseFailedException : Exception
{
    public GroqToolUseFailedException() : base("Groq tool_use_failed") { }
}

/// <summary>Tất cả model + key đều bị rate limit. ChatService sẽ xử lý graceful.</summary>
public class RateLimitedException : Exception
{
    public RateLimitedException(string message) : base(message) { }
}
