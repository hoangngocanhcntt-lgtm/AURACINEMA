using Microsoft.Extensions.Logging;

namespace AuraCinema.Services.Chat;

/// <summary>
/// Xoay vòng API key Groq theo round-robin.
/// Khi 1 key bị 429, đánh dấu "cooldown" và chuyển sang key tiếp theo.
/// Thread-safe, đăng ký Singleton.
/// </summary>
public sealed class ApiKeyRotator
{
    private readonly string[] _keys;
    private readonly DateTime[] _cooldownUntil;
    private int _currentIndex;
    private readonly object _lock = new();
    private readonly ILogger<ApiKeyRotator> _logger;

    private static readonly TimeSpan DefaultCooldown = TimeSpan.FromSeconds(60);

    public ApiKeyRotator(IEnumerable<string> apiKeys, ILogger<ApiKeyRotator> logger)
    {
        _keys = apiKeys.Where(k => !string.IsNullOrWhiteSpace(k)).ToArray();
        if (_keys.Length == 0)
            throw new InvalidOperationException("Không có API key nào được cấu hình cho Groq LLM. Hãy thêm Llm:ApiKeys vào appsettings.json.");

        _cooldownUntil = new DateTime[_keys.Length];
        _currentIndex = 0;
        _logger = logger;

        _logger.LogInformation("ApiKeyRotator khởi tạo với {Count} key(s)", _keys.Length);
    }

    /// <summary>Lấy key khả dụng tiếp theo. Trả null nếu tất cả đang cooldown.</summary>
    public string? GetNextKey()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            // Thử tất cả key, bắt đầu từ current
            for (int i = 0; i < _keys.Length; i++)
            {
                var idx = (_currentIndex + i) % _keys.Length;
                if (_cooldownUntil[idx] <= now)
                {
                    _currentIndex = (idx + 1) % _keys.Length;
                    return _keys[idx];
                }
            }

            // Tất cả đang cooldown → trả key sắp hết cooldown nhất
            var minCooldown = _cooldownUntil.Min();
            var bestIdx = Array.IndexOf(_cooldownUntil, minCooldown);
            _currentIndex = (bestIdx + 1) % _keys.Length;
            _logger.LogWarning("Tất cả API key đang cooldown. Dùng key sắp hết cooldown nhất (còn {Remaining}s)",
                (minCooldown - now).TotalSeconds);
            return _keys[bestIdx];
        }
    }

    /// <summary>Đánh dấu key bị rate-limit, tạm thời không dùng.</summary>
    public void MarkRateLimited(string key, TimeSpan? cooldown = null)
    {
        lock (_lock)
        {
            var idx = Array.IndexOf(_keys, key);
            if (idx >= 0)
            {
                _cooldownUntil[idx] = DateTime.UtcNow + (cooldown ?? DefaultCooldown);
                _logger.LogWarning("API key ***{KeySuffix} bị rate-limit, cooldown {Seconds}s",
                    key[^4..], (cooldown ?? DefaultCooldown).TotalSeconds);
            }
        }
    }

    /// <summary>Số key đang khả dụng.</summary>
    public int AvailableKeyCount
    {
        get
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                return _cooldownUntil.Count(t => t <= now);
            }
        }
    }

    public int TotalKeyCount => _keys.Length;
}
