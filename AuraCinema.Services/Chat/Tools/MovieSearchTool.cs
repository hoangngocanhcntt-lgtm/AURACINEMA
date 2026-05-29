using System.Text.Json;
using AuraCinema.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuraCinema.Services.Chat.Tools;

public class MovieSearchTool : IChatTool
{
    private readonly AppDbContext _db;
    private readonly ILogger<MovieSearchTool> _logger;

    public MovieSearchTool(AppDbContext db, ILogger<MovieSearchTool> logger)
    {
        _db = db;
        _logger = logger;
    }

    public string Name => "search_movies";

    public string Description => "Tìm phim đang chiếu hoặc sắp chiếu theo thể loại, từ khóa tiêu đề, hoặc tâm trạng.";

    public object Schema => new
    {
        type = "object",
        properties = new
        {
            keyword = new { type = "string", description = "Từ khóa trong Title hoặc Director" },
            genre = new { type = "string", description = "Thể loại: Hành động, Hài, Tình cảm..." },
            status = new { type = "string", @enum = new[] { "Dang chieu", "Sap chieu" } },
            limit = new { type = "integer" }
        }
    };

    public async Task<object> ExecuteAsync(JsonElement args, ChatToolContext ctx, CancellationToken ct)
    {
        try
        {
            var keyword = args.TryGetProperty("keyword", out var kw) ? kw.GetString() : null;
            var genre = args.TryGetProperty("genre", out var g) ? g.GetString() : null;
            var status = args.TryGetProperty("status", out var s) ? s.GetString() : null;
            
            var limit = 5;
            if (args.TryGetProperty("limit", out var l) && l.TryGetInt32(out var parsedLimit))
            {
                limit = Math.Clamp(parsedLimit, 1, 10);
            }

            var query = _db.Movies.AsQueryable();

            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(m => (m.Title != null && m.Title.Contains(keyword)) || 
                                         (m.Director != null && m.Director.Contains(keyword)));
            }

            if (!string.IsNullOrEmpty(genre))
            {
                query = query.Where(m => m.Genre != null && m.Genre.Contains(genre));
            }

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(m => m.Status == status);
            }
            else
            {
                query = query.Where(m => m.Status == "Dang chieu" || m.Status == "Sap chieu");
            }

            var now = DateTime.Now;

            var list = await query
                .OrderByDescending(m => m.ReleaseDate)
                .Take(limit)
                .Select(m => new
                {
                    movieId = m.MovieID,
                    title = m.Title,
                    genre = m.Genre,
                    duration = m.Duration,
                    releaseDate = m.ReleaseDate,
                    poster = m.Poster,
                    status = m.Status,
                    showtimeCount = m.Showtimes.Count(s => s.StartTime >= now)
                })
                .ToListAsync(ct);

            if (list.Count == 0)
            {
                return new
                {
                    ok = true,
                    count = 0,
                    movies = Array.Empty<object>(),
                    hint = "Không tìm thấy phim phù hợp."
                };
            }

            return new
            {
                ok = true,
                count = list.Count,
                movies = list
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tìm kiếm phim");
            return new { ok = false, error = "SEARCH_ERROR", message = "Lỗi khi tìm kiếm phim từ hệ thống." };
        }
    }
}
