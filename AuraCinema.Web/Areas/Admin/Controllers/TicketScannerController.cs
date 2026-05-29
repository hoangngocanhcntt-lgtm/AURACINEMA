using AuraCinema.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCinema.Web.Areas.Admin.Controllers;

[Authorize(Roles = "Staff")]
public class TicketScannerController : AdminBaseController
{
    private readonly AppDbContext _db;

    public TicketScannerController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyTicket(string qrCode)
    {
        if (string.IsNullOrEmpty(qrCode) || !qrCode.StartsWith("ORD-"))
        {
            return Json(new { success = false, message = "Mã QR không hợp lệ. Vui lòng quét mã do Aura Cinema cung cấp." });
        }

        var order = await _db.Orders
            .Include(o => o.User)
            .Include(o => o.OrderSeats).ThenInclude(os => os.Seat)
            .Include(o => o.OrderServices).ThenInclude(os => os.Service)
            .Include(o => o.Showtime).ThenInclude(s => s.Movie)
            .Include(o => o.Showtime).ThenInclude(s => s.Room)
            .FirstOrDefaultAsync(o => o.OrderCode == qrCode);

        if (order == null)
        {
            return Json(new { success = false, message = "Không tìm thấy vé trong hệ thống." });
        }

        string statusClean = order.Status?.Trim().ToLower() ?? "";
        if (statusClean != "da thanh toan" && statusClean != "đã thanh toán")
        {
            string friendlyStatus = statusClean switch
            {
                "cho thanh toan" or "chờ thanh toán" => "Chờ thanh toán",
                "da thanh toan" or "đã thanh toán" => "Đã thanh toán",
                "da su dung" or "đã sử dụng" => "Đã sử dụng",
                "can hoan tien" or "cần hoàn tiền" => "Cần hoàn tiền",
                "da huy" or "đã hủy" => "Đã hủy",
                _ => order.Status
            };
            return Json(new { success = false, message = $"Vé không hợp lệ! Trạng thái hiện tại: {friendlyStatus}" });
        }

        var seatLabels = order.OrderSeats.Any() 
            ? string.Join(", ", order.OrderSeats.Select(os => $"{os.Seat.RowLabel}{os.Seat.SeatNumber}"))
            : "Chưa xác định";

        var services = order.OrderServices.Select(os => new {
            name = os.Service.ServiceName,
            quantity = os.Quantity
        }).ToList();

        return Json(new {
            success = true,
            orderInfo = new {
                id = order.OrderCode,
                movie = order.Showtime.Movie.Title,
                customer = order.User.FullName,
                room = order.Showtime.Room.RoomName,
                seats = seatLabels,
                startTime = order.Showtime.StartTime.ToString("dd/MM/yyyy HH:mm"),
                amount = order.FinalAmount.ToString("N0") + " đ",
                services = services,
                printUrl = Url.Action("PrintTicket", new { orderCode = order.OrderCode })
            }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmCheckIn(string qrCode)
    {
        if (string.IsNullOrEmpty(qrCode) || !qrCode.StartsWith("ORD-"))
        {
            return Json(new { success = false, message = "Mã QR không hợp lệ." });
        }

        var order = await _db.Orders
            .Include(o => o.User)
            .Include(o => o.Showtime).ThenInclude(s => s.Movie)
            .FirstOrDefaultAsync(o => o.OrderCode == qrCode);

        if (order == null)
        {
            return Json(new { success = false, message = "Không tìm thấy vé trong hệ thống." });
        }

        string statusClean = order.Status?.Trim().ToLower() ?? "";
        if (statusClean != "da thanh toan" && statusClean != "đã thanh toán")
        {
            string friendlyStatus = statusClean switch
            {
                "cho thanh toan" or "chờ thanh toán" => "Chờ thanh toán",
                "da thanh toan" or "đã thanh toán" => "Đã thanh toán",
                "da su dung" or "đã sử dụng" => "Đã sử dụng",
                "can hoan tien" or "cần hoàn tiền" => "Cần hoàn tiền",
                "da huy" or "đã hủy" => "Đã hủy",
                _ => order.Status
            };
            return Json(new { success = false, message = $"Không thể check-in. Vé đang ở trạng thái: {friendlyStatus}" });
        }

        order.Status = "Đã sử dụng";
        await _db.SaveChangesAsync();

        return Json(new { 
            success = true, 
            message = "Xác nhận cho khách vào rạp thành công!",
            orderInfo = new {
                id = order.OrderCode,
                movie = order.Showtime.Movie.Title,
                customer = order.User.FullName
            }
        });
    }

    [HttpGet]
    public async Task<IActionResult> PrintTicket(string orderCode)
    {
        var order = await _db.Orders
            .Include(o => o.User)
            .Include(o => o.OrderSeats).ThenInclude(os => os.Seat)
            .Include(o => o.OrderServices).ThenInclude(os => os.Service)
            .Include(o => o.Showtime).ThenInclude(s => s.Movie)
            .Include(o => o.Showtime).ThenInclude(s => s.Room)
            .FirstOrDefaultAsync(o => o.OrderCode == orderCode);

        if (order == null) return NotFound();

        return View(order);
    }
}

public class ScanRequest
{
    public string QrCodeData { get; set; } = string.Empty;
}
