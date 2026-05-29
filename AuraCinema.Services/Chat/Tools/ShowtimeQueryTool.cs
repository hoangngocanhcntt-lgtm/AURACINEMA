using System.Text.Json;
using AuraCinema.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuraCinema.Services.Chat.Tools;

public class ShowtimeQueryTool : IChatTool
{
    private readonly AppDbContext _db;
    private readonly ILogger<ShowtimeQueryTool> _logger;

    public ShowtimeQueryTool(AppDbContext db, ILogger<ShowtimeQueryTool> logger)
    {
        _db = db;
        _logger = logger;
    }

    public string Name => "get_showtimes";

    public string Description => "Lấy danh sách suất chiếu của một phim trong vài ngày tới, kèm số ghế còn trống.";

    public object Schema => new
    {
        type = "object",
        properties = new
        {
            movieId = new { type = "integer" },
            title = new { type = "string", description = "Nếu không biết movieId, có thể truyền title gần đúng" },
            fromDate = new { type = "string", format = "date" },
            days = new { type = "integer" }
        }
    };

    public async Task<object> ExecuteAsync(JsonElement args, ChatToolContext ctx, CancellationToken ct)
    {
        try
        {
            int? movieId = null;
            if (args.TryGetProperty("movieId", out var mId) && mId.ValueKind == JsonValueKind.Number)
            {
                movieId = mId.GetInt32();
            }

            string? title = null;
            if (args.TryGetProperty("title", out var tProp) && tProp.ValueKind == JsonValueKind.String)
            {
                title = tProp.GetString();
            }

            if (movieId == null && string.IsNullOrWhiteSpace(title))
            {
                return new { ok = false, error = "INVALID_ARGS", message = "Cần cung cấp movieId hoặc title." };
            }

            var fromDate = DateTime.Today;
            if (args.TryGetProperty("fromDate", out var fdProp) && fdProp.ValueKind == JsonValueKind.String && DateTime.TryParse(fdProp.GetString(), out var parsedDate))
            {
                fromDate = parsedDate.Date;
            }

            var days = 5;
            if (args.TryGetProperty("days", out var dProp) && dProp.ValueKind == JsonValueKind.Number && dProp.TryGetInt32(out var parsedDays))
            {
                days = Math.Clamp(parsedDays, 1, 14);
            }

            var movie = movieId.HasValue 
                ? await _db.Movies.FindAsync(new object[] { movieId.Value }, ct)
                : await _db.Movies.FirstOrDefaultAsync(m => m.Title != null && m.Title.Contains(title!), ct);

            if (movie == null)
            {
                return new { ok = false, error = "MOVIE_NOT_FOUND", message = "Không tìm thấy phim này." };
            }

            var id = movie.MovieID;
            var now = DateTime.Now;
            var startDate = now > fromDate ? now : fromDate;
            var maxDate = fromDate.AddDays(days);

            var showtimes = await _db.Showtimes
                .Include(s => s.Room)
                .Where(s => s.MovieID == id && s.Status == "Đang mở bán" && s.StartTime >= startDate && s.StartTime < maxDate)
                .OrderBy(s => s.StartTime)
                .ToListAsync(ct);

            var showtimeIds = showtimes.Select(s => s.ShowtimeID).ToList();
            
            var seatCounts = await _db.Orders
                .Where(o => showtimeIds.Contains(o.ShowtimeID) &&
                            (o.Status == "Đã thanh toán" || 
                             o.Status == "Da thanh toan" || 
                             ((o.Status == "Chờ thanh toán" || o.Status == "Cho thanh toan") && o.HoldExpiryTime > DateTime.Now)))
                .SelectMany(o => o.OrderSeats)
                .GroupBy(os => os.Order.ShowtimeID)
                .Select(g => new { ShowtimeID = g.Key, Sold = g.Count() })
                .ToDictionaryAsync(x => x.ShowtimeID, x => x.Sold, ct);

            var today = DateOnly.FromDateTime(DateTime.Today);
            var tomorrow = today.AddDays(1);

            var groups = showtimes
                .GroupBy(s => DateOnly.FromDateTime(s.StartTime))
                .Select(g => new
                {
                    date = g.Key.ToString("yyyy-MM-dd"),
                    dayLabel = g.Key == today ? "Hôm nay" :
                               g.Key == tomorrow ? "Ngày mai" :
                               g.Key.ToString("dd/MM"),
                    showtimes = g.Select(s => new
                    {
                        showtimeId = s.ShowtimeID,
                        startTime = s.StartTime.ToString("HH:mm"),
                        endTime = s.EndTime.ToString("HH:mm"),
                        roomName = s.Room?.RoomName ?? "",
                        availableSeats = (s.Room?.Capacity ?? 0) - seatCounts.GetValueOrDefault(s.ShowtimeID, 0),
                        totalSeats = s.Room?.Capacity ?? 0
                    }).ToList()
                })
                .ToList();

            return new
            {
                ok = true,
                movie = new { id = movie.MovieID, title = movie.Title },
                groups = groups
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi query suất chiếu");
            return new { ok = false, error = "QUERY_ERROR", message = "Đã có lỗi xảy ra khi lấy lịch chiếu." };
        }
    }
}
