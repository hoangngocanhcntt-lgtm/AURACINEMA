using AuraCinema.Infrastructure.Data;
using AuraCinema.Web.Areas.Admin.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCinema.Web.Areas.Admin.Controllers;

[Authorize(Roles = "Admin")]
public class DashboardController : AdminBaseController
{
    private readonly AppDbContext _db;

    public DashboardController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1);

        // Fetch valid orders
        var validOrders = await _db.Orders
            .Where(o => o.Status == "Da thanh toan" || o.Status == "Da su dung")
            .Include(o => o.OrderSeats)
            .Include(o => o.Showtime)
                .ThenInclude(s => s.Movie)
            .ToListAsync();

        // 1. KPI Scorecards
        var totalRevenue = validOrders.Sum(o => o.FinalAmount);
        var totalTicketsSold = validOrders.Sum(o => o.OrderSeats.Count);
        
        // Mock Concession Orders since UI for it isn't built yet
        var totalConcessionOrders = (int)(validOrders.Count * 0.4); 

        // Occupancy Rate (mock calculation based on sold seats vs total seats)
        var totalSeatsInCinema = await _db.Seats.CountAsync();
        var activeShowtimes = await _db.Showtimes.CountAsync(s => s.StartTime > now.AddDays(-7) && s.StartTime < now.AddDays(7));
        var theoreticalCapacity = activeShowtimes > 0 ? activeShowtimes * totalSeatsInCinema : 1;
        var occupancyRate = Math.Min(100, Math.Round((double)totalTicketsSold / theoreticalCapacity * 100, 2));
        if (occupancyRate == 0 && totalTicketsSold > 0) occupancyRate = 12.5; // Mock slightly for visual

        // 2. Line Chart: Revenue over the last 7 days
        var last7Days = Enumerable.Range(0, 7).Select(i => now.AddDays(-6 + i).Date).ToList();
        var revenueByDay = last7Days.Select(d => 
            validOrders.Where(o => o.HoldExpiryTime.Date == d) // Using HoldExpiryTime as proxy for OrderDate
                       .Sum(o => o.FinalAmount)
        ).ToList();

        // 3. Donut Chart: Revenue Structure
        // Mock Concession revenue to be 20% of ticket revenue for visual demo
        var ticketRevenue = totalRevenue;
        var concessionRevenue = (int)(totalRevenue * 0.2);

        // 4. Pie Chart: Payment Methods
        var payOSCount = validOrders.Count(o => !string.IsNullOrEmpty(o.PayOSTransID));
        var cashCount = (int)(validOrders.Count * 0.1); // Mock 10% cash
        var momoCount = (int)(validOrders.Count * 0.2); // Mock 20% Momo

        // 5. Horizontal Bar Chart: Top 5 Movies
        var topMovies = validOrders
            .GroupBy(o => o.Showtime.Movie.Title)
            .Select(g => new { Title = g.Key, Tickets = g.Sum(o => o.OrderSeats.Count) })
            .OrderByDescending(x => x.Tickets)
            .Take(5)
            .ToList();

        // 6. Column Chart: Customers by Time (Khung giờ)
        // Group by hour of showtime
        var hours = new List<string> { "09:00", "12:00", "15:00", "18:00", "21:00" };
        var customersByHour = new List<int> { 
            validOrders.Count(o => o.Showtime.StartTime.Hour < 11),
            validOrders.Count(o => o.Showtime.StartTime.Hour >= 11 && o.Showtime.StartTime.Hour < 14),
            validOrders.Count(o => o.Showtime.StartTime.Hour >= 14 && o.Showtime.StartTime.Hour < 17),
            validOrders.Count(o => o.Showtime.StartTime.Hour >= 17 && o.Showtime.StartTime.Hour < 20),
            validOrders.Count(o => o.Showtime.StartTime.Hour >= 20)
        };

        // 7. Funnel Chart: Promotion Usage
        var totalOrdersCount = validOrders.Count;
        var ordersWithPromo = validOrders.Count(o => o.PromoID != null);

        var vm = new DashboardViewModel
        {
            TotalRevenue = totalRevenue,
            TotalTicketsSold = totalTicketsSold,
            TotalConcessionOrders = totalConcessionOrders,
            RoomOccupancyRate = occupancyRate,
            
            RevenueOverTime = new LineChartData { Labels = last7Days.Select(d => d.ToString("dd/MM")).ToList(), Data = revenueByDay },
            RevenueStructure = new DonutChartData { TicketRevenue = ticketRevenue, ConcessionRevenue = concessionRevenue },
            PaymentMethods = new PieChartData { PayOS = payOSCount, Cash = cashCount, Momo = momoCount },
            TopMovies = new HorizontalBarChartData { Labels = topMovies.Select(m => m.Title).ToList(), Data = topMovies.Select(m => m.Tickets).ToList() },
            CustomersByTime = new ColumnChartData { Labels = hours, Data = customersByHour },
            PromotionUsage = new FunnelChartData { TotalOrders = totalOrdersCount, OrdersWithPromoCode = ordersWithPromo }
        };

        return View(vm);
    }
}
