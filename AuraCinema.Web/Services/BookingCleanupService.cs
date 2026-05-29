using AuraCinema.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AuraCinema.Web.Services;

public class BookingCleanupService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<BookingCleanupService> _logger;

    public BookingCleanupService(IServiceProvider services, ILogger<BookingCleanupService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Booking Cleanup Service is starting.");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await CleanupExpiredOrdersAsync();
                await UpdateShowtimeStatusAsync();
                await UpdateMovieStatusAsync();
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Chạy mỗi phút
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Booking Cleanup Service is stopping due to cancellation.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred in Booking Cleanup Service.");
        }
    }

    private async Task CleanupExpiredOrdersAsync()
    {
        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            var expiredOrders = await db.Orders
                .Where(o => o.Status == "Chờ thanh toán" && o.HoldExpiryTime <= DateTime.Now)
                .ToListAsync();

            if (expiredOrders.Any())
            {
                _logger.LogInformation("Cleaning up {Count} expired orders.", expiredOrders.Count);
                foreach (var order in expiredOrders)
                {
                    order.Status = "Đã hủy";
                }
                await db.SaveChangesAsync();
            }
        }
    }

    private async Task UpdateShowtimeStatusAsync()
    {
        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTime.Now;
            var today = now.Date;
            var maxSellingDate = today.AddDays(4);

            // Suất chiếu lọt vào khung 4 ngày -> "Đang mở bán" (bao gồm cả dữ liệu cũ 'Sap chieu')
            var newlyOpened = await db.Showtimes
                .Where(s => (s.Status == "Đã lên lịch" || s.Status == "Sap chieu") && s.StartTime.Date <= maxSellingDate && s.StartTime > now)
                .ToListAsync();

            foreach (var s in newlyOpened)
            {
                s.Status = "Đang mở bán";
            }

            // Suất chiếu đã bắt đầu -> "Đang chiếu"
            var startedShowtimes = await db.Showtimes
                .Where(s => (s.Status == "Đã lên lịch" || s.Status == "Đang mở bán" || s.Status == "Sap chieu") && s.StartTime <= now && s.EndTime > now)
                .ToListAsync();

            foreach (var s in startedShowtimes)
            {
                s.Status = "Đang chiếu";
            }

            // Suất chiếu đã kết thúc -> "Đã kết thúc"
            var endedShowtimes = await db.Showtimes
                .Where(s => (s.Status == "Đã lên lịch" || s.Status == "Đang mở bán" || s.Status == "Đang chiếu" || s.Status == "Sap chieu" || s.Status == "Dang dien ra") && s.EndTime <= now)
                .ToListAsync();

            foreach (var s in endedShowtimes)
            {
                s.Status = "Đã kết thúc";
            }

            if (newlyOpened.Any() || startedShowtimes.Any() || endedShowtimes.Any())
            {
                _logger.LogInformation("Updated showtime status: {Opened} → Đang mở bán, {Started} → Đang chiếu, {Ended} → Đã kết thúc.",
                    newlyOpened.Count, startedShowtimes.Count, endedShowtimes.Count);
                await db.SaveChangesAsync();
            }
        }
    }
    private async Task UpdateMovieStatusAsync()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var today = DateOnly.FromDateTime(DateTime.Today);

        // Sắp chiếu → Đang chiếu khi đến hoặc qua ngày khởi chiếu
        var toNowShowing = await db.Movies
            .Where(m => m.Status == "Sap chieu" && m.ReleaseDate <= today)
            .ToListAsync();

        foreach (var m in toNowShowing)
            m.Status = "Dang chieu";

        if (toNowShowing.Any())
        {
            _logger.LogInformation("Updated {Count} movie(s) from Sap chieu → Dang chieu.", toNowShowing.Count);
            await db.SaveChangesAsync();
        }
    }
}
