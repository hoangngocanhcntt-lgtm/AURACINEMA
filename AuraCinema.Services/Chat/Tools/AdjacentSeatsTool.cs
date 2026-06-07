using System.Text.Json;
using AuraCinema.Domain.Interfaces.Services;
using AuraCinema.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace AuraCinema.Services.Chat.Tools;

public class AdjacentSeatsTool : IChatTool
{
    private readonly IBookingService _bookingService;
    private readonly ILogger<AdjacentSeatsTool> _logger;

    public AdjacentSeatsTool(IBookingService bookingService, ILogger<AdjacentSeatsTool> logger)
    {
        _bookingService = bookingService;
        _logger = logger;
    }

    public string Name => "get_available_adjacent_seats";

    public string Description => "Tìm các cụm ghế kề nhau còn trống cho một suất chiếu, theo loại ghế và số lượng yêu cầu.";

    public object Schema => new
    {
        type = "object",
        properties = new
        {
            showtimeId = new { type = "integer", description = "ID suất chiếu" },
            count = new { type = "integer", description = "Số ghế cần ngồi cạnh nhau" },
            seatType = new { type = "string", description = "Loại ghế: Thuong, VIP, Doi", @enum = new[] { "Thuong", "VIP", "Doi" } }
        },
        required = new[] { "showtimeId", "count" }
    };

    public async Task<object> ExecuteAsync(JsonElement args, ChatToolContext ctx, CancellationToken ct)
    {
        try
        {
            // Parse showtimeId
            if (!args.TryGetAny(out var stProp, "showtimeId", "showtime_id") || !stProp.TryGetInt32(out var showtimeId))
            {
                return new { ok = false, error = "INVALID_ARGS", message = "Cần cung cấp showtimeId." };
            }

            // Parse count (default 2)
            var count = 2;
            if (args.TryGetProperty("count", out var cProp) && cProp.TryGetInt32(out var parsedCount))
            {
                count = Math.Clamp(parsedCount, 1, 10);
            }

            // Parse seatType (optional)
            string? seatType = null;
            if (args.TryGetAny(out var typeProp, "seatType", "seat_type") && typeProp.ValueKind == JsonValueKind.String)
            {
                seatType = typeProp.GetString();
            }

            // Lấy layout ghế
            var (showtime, allSeats, soldOrHeldSeatIds) = await _bookingService.GetShowtimeSeatLayoutAsync(showtimeId);

            _logger.LogInformation("AdjacentSeatsTool: showtimeId={ShowtimeId}, RoomID={RoomId}, totalSeats={Total}, soldOrHeld={Sold}, count={Count}, seatType={SeatType}",
                showtimeId, showtime.RoomID, allSeats.Count, soldOrHeldSeatIds.Count, count, seatType ?? "all");

            // Lọc ghế còn trống
            var availableSeats = allSeats
                .Where(s => !soldOrHeldSeatIds.Contains(s.SeatID))
                .ToList();

            _logger.LogInformation("AdjacentSeatsTool: availableSeats={Available}", availableSeats.Count);

            // Lọc theo loại ghế nếu có
            if (!string.IsNullOrEmpty(seatType))
            {
                // Log all distinct SeatType values for debugging
                var distinctTypes = availableSeats.Select(s => s.SeatType).Distinct().ToList();
                _logger.LogInformation("AdjacentSeatsTool: filtering by seatType='{SeatType}', available types in room: [{Types}]",
                    seatType, string.Join(", ", distinctTypes.Select(t => $"'{t}'")));

                availableSeats = availableSeats
                    .Where(s => string.Equals(s.SeatType, seatType, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                _logger.LogInformation("AdjacentSeatsTool: after type filter: {Count} seats", availableSeats.Count);
            }

            // Group theo hàng, tìm cụm ghế liên tiếp
            var groups = new List<object>();
            var rows = availableSeats
                .GroupBy(s => s.RowLabel)
                .OrderBy(g => g.Key);

            foreach (var row in rows)
            {
                var sorted = row.OrderBy(s => s.SeatNumber).ToList();

                _logger.LogInformation("AdjacentSeatsTool: Row {Row}: seats=[{Seats}]",
                    row.Key, string.Join(", ", sorted.Select(s => $"{s.RowLabel}{s.SeatNumber}")));

                for (int i = 0; i <= sorted.Count - count; i++)
                {
                    // Kiểm tra count ghế liên tiếp (SeatNumber liên tục)
                    var isConsecutive = true;
                    for (int j = 1; j < count; j++)
                    {
                        if (sorted[i + j].SeatNumber != sorted[i].SeatNumber + j)
                        {
                            isConsecutive = false;
                            break;
                        }
                    }

                    if (isConsecutive)
                    {
                        var seatGroup = sorted.Skip(i).Take(count).ToList();
                        var first = seatGroup.First();
                        var last = seatGroup.Last();

                        groups.Add(new
                        {
                            seatIds = seatGroup.Select(s => s.SeatID).ToArray(),
                            label = count == 1
                                ? $"{first.RowLabel}{first.SeatNumber}"
                                : $"{first.RowLabel}{first.SeatNumber}-{last.RowLabel}{last.SeatNumber}",
                            seatType = first.SeatType,
                            row = first.RowLabel
                        });

                        if (groups.Count >= 5) break;
                    }
                }

                if (groups.Count >= 5) break;
            }

            _logger.LogInformation("AdjacentSeatsTool: found {GroupCount} groups", groups.Count);

            return new
            {
                ok = true,
                showtime = new
                {
                    showtimeId = showtime.ShowtimeID,
                    movieTitle = showtime.Movie?.Title ?? "",
                    roomName = showtime.Room?.RoomName ?? "",
                    startTime = showtime.StartTime.ToString("HH:mm")
                },
                requestedCount = count,
                requestedType = seatType ?? "tất cả",
                groups = groups,
                hint = groups.Count == 0 ? "Không tìm thấy cụm ghế phù hợp. Bạn thử đổi loại ghế hoặc số lượng nhé." : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tìm ghế kề nhau cho suất chiếu");
            return new { ok = false, error = "QUERY_ERROR", message = "Đã có lỗi xảy ra khi tìm ghế." };
        }
    }
}
