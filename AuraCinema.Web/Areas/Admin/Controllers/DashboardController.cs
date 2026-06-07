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

    public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate, string rangeType = "7days")
    {
        var now = DateTime.UtcNow.Date;
        
        // Determine date range
        DateTime fromDate;
        DateTime toDate = endDate ?? now.AddDays(1).AddSeconds(-1); // default to end of today

        if (startDate.HasValue)
        {
            fromDate = startDate.Value.Date;
            if (!endDate.HasValue) toDate = fromDate.AddDays(1).AddSeconds(-1);
            else toDate = endDate.Value.Date.AddDays(1).AddSeconds(-1);
            rangeType = "custom";
        }
        else
        {
            switch (rangeType)
            {
                case "today":
                    fromDate = now;
                    break;
                case "thisWeek":
                    int diff = (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;
                    fromDate = now.AddDays(-1 * diff);
                    break;
                case "thisMonth":
                    fromDate = new DateTime(now.Year, now.Month, 1);
                    break;
                case "thisYear":
                    fromDate = new DateTime(now.Year, 1, 1);
                    break;
                case "7days":
                default:
                    fromDate = now.AddDays(-6); // Last 7 days including today
                    rangeType = "7days";
                    break;
            }
        }

        // Fetch valid orders within date range
        var validOrdersQuery = _db.Orders
            .Where(o => o.Status == "Da thanh toan" || o.Status == "Da su dung" || o.Status == "Đã thanh toán" || o.Status == "Đã sử dụng")
            .Where(o => o.HoldExpiryTime >= fromDate && o.HoldExpiryTime <= toDate)
            .Include(o => o.OrderSeats)
            .Include(o => o.OrderServices)
                .ThenInclude(os => os.Service)
            .Include(o => o.Showtime)
                .ThenInclude(s => s.Movie);

        var validOrders = await validOrdersQuery.ToListAsync();

        // 1. KPI Scorecards
        var totalRevenue = validOrders.Sum(o => o.FinalAmount);
        var totalTicketsSold = validOrders.Sum(o => o.OrderSeats.Count);
        
        var concessionRevenue = validOrders.Sum(o => o.OrderServices.Sum(os => os.Price * os.Quantity));
        var ticketRevenue = totalRevenue - concessionRevenue;
        if (ticketRevenue < 0) ticketRevenue = 0;

        // Occupancy Rate
        var totalSeatsInCinema = await _db.Seats.CountAsync();
        var activeShowtimes = await _db.Showtimes.CountAsync(s => s.StartTime >= fromDate && s.StartTime <= toDate);
        var theoreticalCapacity = activeShowtimes > 0 ? activeShowtimes * totalSeatsInCinema : 1;
        var occupancyRate = Math.Min(100, Math.Round((double)totalTicketsSold / theoreticalCapacity * 100, 2));
        if (occupancyRate == 0 && totalTicketsSold > 0) occupancyRate = 12.5;

        // 2. Line Chart: Revenue over time
        var daysCount = (toDate.Date - fromDate.Date).Days + 1;
        var labels = new List<string>();
        var revenueByDay = new List<int>();

        if (daysCount <= 31) // Show daily if a month or less
        {
            for (int i = 0; i < daysCount; i++)
            {
                var d = fromDate.AddDays(i);
                labels.Add(d.ToString("dd/MM"));
                revenueByDay.Add(validOrders.Where(o => o.HoldExpiryTime.Date == d.Date).Sum(o => o.FinalAmount));
            }
        }
        else // Group by month
        {
            var currentMonth = new DateTime(fromDate.Year, fromDate.Month, 1);
            while (currentMonth <= toDate)
            {
                labels.Add(currentMonth.ToString("MM/yyyy"));
                revenueByDay.Add(validOrders.Where(o => o.HoldExpiryTime.Year == currentMonth.Year && o.HoldExpiryTime.Month == currentMonth.Month).Sum(o => o.FinalAmount));
                currentMonth = currentMonth.AddMonths(1);
            }
        }

        // 3. Pie Chart: Revenue By Movie
        var revenueByMovieGroup = validOrders
            .GroupBy(o => o.Showtime.Movie.Title)
            .Select(g => new { Title = g.Key, Revenue = g.Sum(o => o.FinalAmount) })
            .OrderByDescending(x => x.Revenue)
            .ToList();

        var top3MoviesForPie = revenueByMovieGroup.Take(3).ToList();
        var otherMoviesRevenue = revenueByMovieGroup.Skip(3).Sum(x => x.Revenue);
        
        var pieLabels = top3MoviesForPie.Select(m => m.Title).ToList();
        var pieData = top3MoviesForPie.Select(m => m.Revenue).ToList();
        if (otherMoviesRevenue > 0)
        {
            pieLabels.Add("Khác");
            pieData.Add(otherMoviesRevenue);
        }

        // 4. Pie Chart: Concession Sales (Replaced Payment Methods)
        var concessionGroup = validOrders.SelectMany(o => o.OrderServices)
            .GroupBy(os => os.Service.ServiceName)
            .Select(g => new { Name = g.Key, Revenue = g.Sum(os => os.Price * os.Quantity) })
            .OrderByDescending(x => x.Revenue)
            .ToList();

        var top3Concessions = concessionGroup.Take(3).ToList();
        var otherConcessionsRevenue = concessionGroup.Skip(3).Sum(x => x.Revenue);

        var concessionLabels = top3Concessions.Select(c => c.Name).ToList();
        var concessionData = top3Concessions.Select(c => c.Revenue).ToList();
        if (otherConcessionsRevenue > 0)
        {
            concessionLabels.Add("Khác");
            concessionData.Add(otherConcessionsRevenue);
        }

        if (concessionLabels.Count == 0)
        {
            // Fallback if no concession sales
            concessionLabels.Add("Chưa có dữ liệu");
            concessionData.Add(1);
        }

        // 5. Horizontal Bar Chart: Top Movies By Revenue
        var topMovies = revenueByMovieGroup.Take(5).ToList();

        // 6. Column Chart: Customers by Time (Khung giờ)
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
            StartDate = fromDate,
            EndDate = toDate.Date,
            RangeType = rangeType,

            TotalRevenue = totalRevenue,
            TotalTicketsSold = totalTicketsSold,
            TotalTicketRevenue = ticketRevenue,
            TotalConcessionRevenue = concessionRevenue,
            RoomOccupancyRate = occupancyRate,
            
            RevenueOverTime = new LineChartData { Labels = labels, Data = revenueByDay },
            RevenueByMovie = new PieChartData { Labels = pieLabels, Data = pieData },
            PaymentMethods = new PieChartData { Labels = concessionLabels, Data = concessionData }, // Reusing PaymentMethods property for Concession Sales to minimize changes
            TopMoviesByRevenue = new HorizontalBarChartData { Labels = topMovies.Select(m => m.Title).ToList(), Data = topMovies.Select(m => m.Revenue).ToList() },
            CustomersByTime = new ColumnChartData { Labels = hours, Data = customersByHour },
            PromotionUsage = new FunnelChartData { TotalOrders = totalOrdersCount, OrdersWithPromoCode = ordersWithPromo }
        };

        return View(vm);
    }
}
