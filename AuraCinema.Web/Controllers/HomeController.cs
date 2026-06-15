using AuraCinema.Infrastructure.Data;
using AuraCinema.Web.ViewModels.Home;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCinema.Web.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;
    public HomeController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var now = DateTime.Now;
        var next4Days = now.AddDays(4);

        var movies = await _db.Movies
            .Where(m => m.Status == "Dang chieu" || m.Status == "Sap chieu")
            .OrderByDescending(m => m.ReleaseDate)
            .Select(m => new MovieCardViewModel
            {
                MovieID     = m.MovieID,
                Title       = m.Title,
                Genre       = m.Genre,
                Poster      = m.Poster,
                Duration    = m.Duration,
                Status      = m.Status,
                ReleaseDate = m.ReleaseDate,
                Director    = m.Director,
                HasEarlyTickets = m.Showtimes.Any(s => s.StartTime > now && s.StartTime <= next4Days)
            })
            .ToListAsync();

        var featuredMovies = movies
            .Where(m => m.Status == "Sap chieu" && m.HasEarlyTickets)
            .Take(3)
            .ToList();

        if (featuredMovies.Count < 3)
        {
            featuredMovies.AddRange(movies
                .Where(m => m.Status == "Dang chieu")
                .Take(3 - featuredMovies.Count));
        }

        var vm = new HomeViewModel
        {
            FeaturedMovies = featuredMovies,
            NowShowing     = movies.Where(m => m.Status == "Dang chieu").ToList(),
            ComingSoon     = movies.Where(m => m.Status == "Sap chieu").ToList(),
            TotalRooms     = await _db.Rooms.CountAsync(),
            TotalSeats     = await _db.Seats.CountAsync()
        };

        if (User.Identity?.IsAuthenticated == true)
        {
            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdString, out int userId))
            {
                var rawTickets = await _db.Orders
                    .Include(o => o.Showtime).ThenInclude(s => s.Movie)
                    .Include(o => o.Showtime).ThenInclude(s => s.Room)
                    .Include(o => o.OrderSeats).ThenInclude(os => os.Seat)
                    .Where(o => o.UserID == userId && (o.Status == "Da thanh toan" || o.Status == "Da su dung" || o.Status == "Đã thanh toán" || o.Status == "Đã sử dụng"))
                    .OrderByDescending(o => o.OrderID)
                    .Take(3)
                    .ToListAsync();

                vm.RecentTickets = rawTickets.Select(o => new AuraCinema.Web.ViewModels.MyTickets.TicketItemViewModel
                {
                    OrderID = o.OrderID,
                    OrderCode = o.OrderCode,
                    MovieTitle = o.Showtime.Movie.Title,
                    Poster = o.Showtime.Movie.Poster,
                    RoomName = o.Showtime.Room.RoomName,
                    Showtime = o.Showtime.StartTime,
                    SeatList = string.Join(", ", o.OrderSeats.Select(s => $"{s.Seat.RowLabel}{s.Seat.SeatNumber}")),
                    FinalAmount = o.FinalAmount,
                    Status = o.Status switch
                    {
                        "Da thanh toan" or "Đã thanh toán" => "Đã thanh toán",
                        "Cho thanh toan" or "Chờ thanh toán" => "Chờ thanh toán",
                        "Da su dung" or "Đã sử dụng" => "Đã sử dụng",
                        "can hoan tien" or "Cần hoàn tiền" => "Cần hoàn tiền",
                        "da huy" or "Đã hủy" => "Đã hủy",
                        "Đã hoàn tiền" or "da hoan tien" => "Đã hoàn tiền",
                        _ => o.Status
                    }
                }).ToList();
            }
        }

        return View(vm);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new AuraCinema.Web.Models.ErrorViewModel { RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [HttpGet("/fix")]
    public IActionResult FixPrices([FromServices] AuraCinema.Infrastructure.Data.AppDbContext db)
    {
        var configs = db.PriceConfigs.ToList();
        foreach(var c in configs)
        {
            if (c.ConfigCode == "BASE_PRICE") c.SurchargeAmount = 2000;
            if (c.ConfigCode == "VIP_SURCHARGE") c.SurchargeAmount = 500;
            if (c.ConfigCode == "COUPLE_SURCHARGE") c.SurchargeAmount = 1000;
            if (c.ConfigCode == "WEEKEND_SURCHARGE") c.SurchargeAmount = 1000;
            if (c.ConfigCode == "EVENING_SURCHARGE") c.SurchargeAmount = 1000;
        }
        db.SaveChanges();
        return Json(configs);
    }
}
