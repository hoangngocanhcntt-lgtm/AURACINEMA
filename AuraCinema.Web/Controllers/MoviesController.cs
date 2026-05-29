using AuraCinema.Infrastructure.Data;
using AuraCinema.Web.ViewModels.Home;
using AuraCinema.Web.ViewModels.Movie;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCinema.Web.Controllers;

public class MoviesController : Controller
{
    private readonly AppDbContext _db;
    public MoviesController(AppDbContext db) => _db = db;

    // GET /Movies?search=abc&status=Dang+chieu&page=1
    public async Task<IActionResult> Index(string? search, string? status, int page = 1)
    {
        const int pageSize = 12;

        var query = _db.Movies.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(m => m.Title.Contains(search));

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(m => m.Status == status);
        else
            query = query.Where(m => m.Status == "Dang chieu" || m.Status == "Sap chieu");

        var total = await query.CountAsync();

        var movies = await query
            .OrderByDescending(m => m.ReleaseDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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

        var vm = new MovieListViewModel
        {
            Movies       = movies,
            SearchTerm   = search,
            StatusFilter = status,
            CurrentPage  = page,
            TotalPages   = (int)Math.Ceiling(total / (double)pageSize)
        };

        return View(vm);
    }

    // GET /Movies/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var movie = await _db.Movies.FindAsync(id);
        if (movie is null) return NotFound();

        var now = DateTime.Now;
        var today = now.Date;
        var maxDate = today.AddDays(5);

        var showtimes = await _db.Showtimes
            .Include(s => s.Room)
            .Where(s => s.MovieID == id && s.Status == "Đang mở bán" && s.StartTime >= now && s.StartTime < maxDate)
            .OrderBy(s => s.StartTime)
            .ToListAsync();

        // Count sold/held seats per showtime
        var showtimeIds = showtimes.Select(s => s.ShowtimeID).ToList();
        var seatCounts = await _db.Orders
            .Where(o => showtimeIds.Contains(o.ShowtimeID) &&
                        (o.Status == "Đã thanh toán" || (o.Status == "Chờ thanh toán" && o.HoldExpiryTime > DateTime.Now)))
            .SelectMany(o => o.OrderSeats)
            .GroupBy(os => os.Order.ShowtimeID)
            .Select(g => new { ShowtimeID = g.Key, Sold = g.Count() })
            .ToDictionaryAsync(x => x.ShowtimeID, x => x.Sold);

        // Group by date
        var groups = showtimes
            .GroupBy(s => DateOnly.FromDateTime(s.StartTime))
            .Select(g => new ShowtimeGroupViewModel
            {
                Date  = g.Key,
                DayLabel = g.Key == DateOnly.FromDateTime(today)  ? "Hôm nay" :
                           g.Key == DateOnly.FromDateTime(today.AddDays(1)) ? "Ngày mai" :
                           g.Key.ToString("dd/MM"),
                Showtimes = g.Select(s => new ShowtimeItemViewModel
                {
                    ShowtimeID     = s.ShowtimeID,
                    StartTime      = s.StartTime.ToString("HH:mm"),
                    EndTime        = s.EndTime.ToString("HH:mm"),
                    RoomName       = s.Room.RoomName,
                    AvailableSeats = s.Room.Capacity - seatCounts.GetValueOrDefault(s.ShowtimeID, 0),
                    TotalSeats     = s.Room.Capacity
                }).ToList()
            })
            .ToList();

        var vm = new MovieDetailViewModel
        {
            MovieID     = movie.MovieID,
            Title       = movie.Title,
            Genre       = movie.Genre,
            Director    = movie.Director,
            Actors      = movie.Actors,
            Description = movie.Description,
            Duration    = movie.Duration,
            ReleaseDate = movie.ReleaseDate,
            Poster      = movie.Poster,
            Trailer     = movie.Trailer,
            Status      = movie.Status,
            ShowtimeGroups = groups
        };

        return View(vm);
    }
}
