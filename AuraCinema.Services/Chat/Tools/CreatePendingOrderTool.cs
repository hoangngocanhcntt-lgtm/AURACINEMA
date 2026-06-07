using System.Text.Json;
using AuraCinema.Domain.Interfaces.Services;
using AuraCinema.Domain.Models.Booking;
using AuraCinema.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AuraCinema.Services.Chat.Tools;

public class CreatePendingOrderTool : IChatTool
{
    private readonly IBookingService _bookingService;
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;
    private readonly ILogger<CreatePendingOrderTool> _logger;

    public CreatePendingOrderTool(IBookingService bookingService, IConfiguration config, AppDbContext db, ILogger<CreatePendingOrderTool> logger)
    {
        _bookingService = bookingService;
        _config = config;
        _db = db;
        _logger = logger;
    }

    public string Name => "create_pending_order";

    public string Description => "Tạo đơn hàng, giữ ghế tạm thời và trả về link trang Checkout để khách tự chọn khuyến mãi rồi thanh toán.";

    public object Schema => new
    {
        type = "object",
        properties = new
        {
            showtimeId = new { type = "integer", description = "ID suất chiếu" },
            seatIds = new { type = "array", items = new { type = "integer" }, description = "Danh sách ID ghế đã chọn (nếu có)" },
            seatLabels = new { type = "array", items = new { type = "string" }, description = "Danh sách mã ghế (ví dụ: 'D1', 'D2') (tùy chọn)" },
            services = new
            {
                type = "array",
                description = "Danh sách dịch vụ kèm theo (tùy chọn)",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        serviceId = new { type = "integer" },
                        quantity = new { type = "integer" }
                    }
                }
            }
        },
        required = new[] { "showtimeId" }
    };

    public async Task<object> ExecuteAsync(JsonElement args, ChatToolContext ctx, CancellationToken ct)
    {
        try
        {
            // 1. Auth check
            if (ctx.UserId == null)
            {
                return new { ok = false, error = "AUTH_REQUIRED", message = "Bạn cần đăng nhập trước khi đặt vé nha." };
            }

            // 2. Parse showtimeId
            if (!args.TryGetAny(out var stProp, "showtimeId", "showtime_id") || !stProp.TryGetInt32(out var showtimeId))
            {
                return new { ok = false, error = "INVALID_ARGS", message = "Cần cung cấp showtimeId." };
            }

            // 2b. Validate showtime tồn tại ngay lập tức
            var showtimeEntity = await _db.Showtimes.FindAsync(new object[] { showtimeId }, ct);
            if (showtimeEntity == null)
            {
                _logger.LogWarning("Chat: create_pending_order gọi với showtimeId={ShowtimeId} nhưng KHÔNG TỒN TẠI trong DB. LLM có thể đã bịa ID.", showtimeId);
                return new { ok = false, error = "INVALID_SHOWTIME", message = $"Suất chiếu với ID {showtimeId} không tồn tại. Hãy sử dụng đúng showtimeId từ kết quả get_showtimes hoặc get_available_adjacent_seats trước đó. Kiểm tra lại BOOKING_CONTEXT." };
            }

            // 3. Parse seatIds and seatLabels
            var seatIds = new List<int>();
            if (args.TryGetAny(out var seatsProp, "seatIds", "seat_ids") && seatsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in seatsProp.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var sid))
                        seatIds.Add(sid);
                }
            }

            var seatLabels = new List<string>();
            if (args.TryGetAny(out var labelsProp, "seatLabels", "seat_labels") && labelsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in labelsProp.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(item.GetString()))
                        seatLabels.Add(item.GetString()!.ToUpper().Trim());
                }
            }

            if (seatIds.Count == 0 && seatLabels.Count == 0)
            {
                return new { ok = false, error = "INVALID_ARGS", message = "Cần cung cấp danh sách ID ghế (seatIds) hoặc mã ghế (seatLabels). Hãy dùng seatIds từ kết quả get_available_adjacent_seats hoặc seatLabels như ['D1','D2','D3','D4']." };
            }

            // If seatLabels are provided, look up their IDs
            if (seatLabels.Count > 0)
            {
                var resolvedIds = await _db.Seats
                    .Where(s => s.RoomID == showtimeEntity.RoomID && seatLabels.Contains(s.RowLabel + s.SeatNumber.ToString()))
                    .Select(s => s.SeatID)
                    .ToListAsync(ct);
                
                if (resolvedIds.Count == 0)
                {
                    _logger.LogWarning("Chat: Không resolve được seatLabels [{Labels}] cho RoomID={RoomId}", string.Join(",", seatLabels), showtimeEntity.RoomID);
                    return new { ok = false, error = "INVALID_SEATS", message = $"Không tìm thấy ghế [{string.Join(", ", seatLabels)}] trong phòng chiếu. Hãy dùng đúng seatIds từ kết quả get_available_adjacent_seats." };
                }

                // Thêm những ID vừa tìm được vào danh sách (không thêm trùng)
                foreach (var rid in resolvedIds)
                {
                    if (!seatIds.Contains(rid)) seatIds.Add(rid);
                }
            }

            // Validate seatIds thuộc đúng phòng chiếu
            if (seatIds.Count > 0)
            {
                var validSeatCount = await _db.Seats
                    .Where(s => s.RoomID == showtimeEntity.RoomID && seatIds.Contains(s.SeatID))
                    .CountAsync(ct);
                
                if (validSeatCount == 0)
                {
                    _logger.LogWarning("Chat: seatIds [{SeatIds}] KHÔNG thuộc RoomID={RoomId} của showtimeId={ShowtimeId}. LLM có thể đã bịa seatIds.", 
                        string.Join(",", seatIds), showtimeEntity.RoomID, showtimeId);
                    return new { ok = false, error = "INVALID_SEATS", message = $"Các ghế với ID [{string.Join(", ", seatIds)}] không thuộc phòng chiếu này. Hãy dùng đúng seatIds từ kết quả get_available_adjacent_seats hoặc truyền seatLabels (VD: ['D1','D2','D3','D4'])." };
                }
                
                if (validSeatCount != seatIds.Count)
                {
                    _logger.LogWarning("Chat: Chỉ {Valid}/{Total} seatIds hợp lệ cho RoomID={RoomId}", validSeatCount, seatIds.Count, showtimeEntity.RoomID);
                    // Lọc chỉ giữ lại seatIds hợp lệ
                    var validSeatIds = await _db.Seats
                        .Where(s => s.RoomID == showtimeEntity.RoomID && seatIds.Contains(s.SeatID))
                        .Select(s => s.SeatID)
                        .ToListAsync(ct);
                    seatIds = validSeatIds;
                }
            }

            if (seatIds.Count == 0)
            {
                return new { ok = false, error = "INVALID_ARGS", message = "Không tìm thấy ghế nào hợp lệ. Hãy dùng đúng seatIds từ kết quả get_available_adjacent_seats hoặc truyền seatLabels." };
            }

            // 4. Parse services (optional)
            var serviceSelections = new List<ServiceSelection>();
            if (args.TryGetAny(out var svcProp, "services") && svcProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in svcProp.EnumerateArray())
                {
                    int svcId = 0, qty = 1;
                    if (item.TryGetAny(out var sidProp, "serviceId", "service_id") && sidProp.TryGetInt32(out var parsedSid))
                        svcId = parsedSid;
                    if (item.TryGetProperty("quantity", out var qProp) && qProp.TryGetInt32(out var parsedQty))
                        qty = parsedQty;

                    if (svcId > 0 && qty > 0)
                        serviceSelections.Add(new ServiceSelection { ServiceID = svcId, Quantity = qty });
                }
            }

            // 5. Tạo đơn hàng
            _logger.LogInformation("Chat: Tạo đơn hàng cho user {UserId}, showtime {ShowtimeId}, seats [{SeatIds}], services [{Services}]",
                ctx.UserId.Value, showtimeId, string.Join(",", seatIds), 
                string.Join(",", serviceSelections.Select(s => $"{s.ServiceID}x{s.Quantity}")));

            var (success, message, orderId) = await _bookingService.CreateHoldOrderAsync(
                ctx.UserId.Value, showtimeId, seatIds, serviceSelections);

            if (!success)
            {
                return new { ok = false, error = "BOOKING_FAILED", message = message };
            }

            // 6. Trả về link Checkout (KHÔNG sinh link PayOS — để khách tự chọn khuyến mãi trên trang Checkout)
            int holdMinutes = int.TryParse(_config["BookingHoldMinutes"], out int mins) ? mins : 10;

            return new
            {
                ok = true,
                orderId = orderId,
                checkoutUrl = $"/Booking/Checkout?orderId={orderId}",
                holdMinutes = holdMinutes,
                message = $"Đơn hàng đã tạo thành công. Ghế được giữ trong {holdMinutes} phút."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tạo đơn hàng qua chat");
            return new { ok = false, error = "SYSTEM_ERROR", message = "Đã có lỗi xảy ra khi tạo đơn hàng." };
        }
    }
}
