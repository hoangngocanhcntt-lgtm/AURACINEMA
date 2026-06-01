namespace AuraCinema.Domain.Models.Chat;

public class LlmOptions
{
    /// <summary>API key đơn lẻ (backward-compatible). Sẽ được gộp vào danh sách key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Danh sách nhiều API key để xoay vòng (key rotation). Ưu tiên nếu có.</summary>
    public List<string> ApiKeys { get; set; } = new();

    public string Model { get; set; } = "llama-3.1-8b-instant";
    public int MaxTokens { get; set; } = 1024;
    public double Temperature { get; set; } = 0.3;
    public double TopP { get; set; } = 0.9;
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Rate limit: số request tối đa mỗi user trong RateLimitWindowSeconds.</summary>
    public int RateLimitMaxRequests { get; set; } = 5;

    /// <summary>Rate limit: cửa sổ thời gian (giây).</summary>
    public int RateLimitWindowSeconds { get; set; } = 60;

    /// <summary>Trả về tất cả key đã cấu hình (gộp ApiKey đơn lẻ + ApiKeys).</summary>
    public IEnumerable<string> GetAllKeys()
    {
        var keys = new HashSet<string>();

        if (!string.IsNullOrWhiteSpace(ApiKey))
            keys.Add(ApiKey.Trim());

        foreach (var k in ApiKeys)
        {
            if (!string.IsNullOrWhiteSpace(k))
                keys.Add(k.Trim());
        }

        return keys;
    }
}
