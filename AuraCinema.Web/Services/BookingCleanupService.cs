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

        while (!stoppingToken.IsCancellationRequested)
        {
            await CleanupExpiredOrdersAsync();
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Chạy mỗi phút
        }
    }

    private async Task CleanupExpiredOrdersAsync()
    {
        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            var expiredOrders = await db.Orders
                .Where(o => o.Status == "Cho thanh toan" && o.HoldExpiryTime <= DateTime.UtcNow)
                .ToListAsync();

            if (expiredOrders.Any())
            {
                _logger.LogInformation("Cleaning up {Count} expired orders.", expiredOrders.Count);
                foreach (var order in expiredOrders)
                {
                    order.Status = "Da huy";
                }
                await db.SaveChangesAsync();
            }
        }
    }
}
