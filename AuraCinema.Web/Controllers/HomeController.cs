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
                Director    = m.Director
            })
            .ToListAsync();

        var vm = new HomeViewModel
        {
            FeaturedMovies = movies.Where(m => m.Status == "Dang chieu").Take(5).ToList(),
            NowShowing     = movies.Where(m => m.Status == "Dang chieu").ToList(),
            ComingSoon     = movies.Where(m => m.Status == "Sap chieu").ToList()
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
    public IActionResult Error() => View(new AuraCinema.Web.Models.ErrorViewModel
    {
        RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier
    });
}
