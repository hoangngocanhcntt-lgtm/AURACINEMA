using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace AuraCinema.Services.Chat;

/// <summary>
/// Rate limiter per-user dùng sliding window.
/// Giới hạn mỗi user tối đa N request trong M giây.
/// Thread-safe, đăng ký Singleton.
/// </summary>
public sealed class ChatRateLimiter
{
    private readonly int _maxRequests;
    private readonly TimeSpan _window;
    private readonly ConcurrentDictionary<string, SlidingWindow> _windows = new();
    private readonly ILogger<ChatRateLimiter> _logger;

    /// <summary>Tạo rate limiter với giới hạn maxRequests trong windowSeconds.</summary>
    public ChatRateLimiter(int maxRequests, int windowSeconds, ILogger<ChatRateLimiter> logger)
    {
        _maxRequests = maxRequests;
        _window = TimeSpan.FromSeconds(windowSeconds);
        _logger = logger;
        _logger.LogInformation("ChatRateLimiter: {Max} requests / {Window}s per user", maxRequests, windowSeconds);
    }

    /// <summary>
    /// Kiểm tra và tiêu thụ 1 slot cho identifier.
    /// Trả true nếu được phép, false nếu bị giới hạn.
    /// </summary>
    public bool TryAcquire(string identifier)
    {
        var window = _windows.GetOrAdd(identifier, _ => new SlidingWindow(_maxRequests, _window));
        var allowed = window.TryAcquire();

        if (!allowed)
        {
            _logger.LogWarning("Rate limit: user {Id} đã vượt {Max} requests / {Window}s",
                identifier, _maxRequests, _window.TotalSeconds);
        }

        return allowed;
    }

    /// <summary>Trả về thời gian chờ (giây) trước khi user có thể gửi tiếp. 0 nếu không bị limit.</summary>
    public double GetRetryAfterSeconds(string identifier)
    {
        if (_windows.TryGetValue(identifier, out var window))
        {
            return window.GetRetryAfterSeconds();
        }
        return 0;
    }

    /// <summary>Xóa dữ liệu tracking cũ (gọi định kỳ nếu cần).</summary>
    public void Cleanup()
    {
        var cutoff = DateTime.UtcNow - _window - _window; // 2x window
        foreach (var kvp in _windows)
        {
            if (kvp.Value.IsExpired(cutoff))
            {
                _windows.TryRemove(kvp.Key, out _);
            }
        }
    }

    private sealed class SlidingWindow
    {
        private readonly int _maxRequests;
        private readonly TimeSpan _window;
        private readonly Queue<DateTime> _timestamps = new();
        private readonly object _lock = new();

        public SlidingWindow(int maxRequests, TimeSpan window)
        {
            _maxRequests = maxRequests;
            _window = window;
        }

        public bool TryAcquire()
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                PurgeExpired(now);

                if (_timestamps.Count >= _maxRequests)
                    return false;

                _timestamps.Enqueue(now);
                return true;
            }
        }

        public double GetRetryAfterSeconds()
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                PurgeExpired(now);

                if (_timestamps.Count < _maxRequests)
                    return 0;

                var oldest = _timestamps.Peek();
                var retryAfter = (oldest + _window - now).TotalSeconds;
                return retryAfter > 0 ? retryAfter : 0;
            }
        }

        public bool IsExpired(DateTime cutoff)
        {
            lock (_lock)
            {
                return _timestamps.Count == 0 || _timestamps.All(t => t < cutoff);
            }
        }

        private void PurgeExpired(DateTime now)
        {
            var threshold = now - _window;
            while (_timestamps.Count > 0 && _timestamps.Peek() < threshold)
            {
                _timestamps.Dequeue();
            }
        }
    }
}
