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
    public async Task<IActionResult> ProcessScan([FromBody] ScanRequest request)
    {
        if (string.IsNullOrEmpty(request.QrCodeData) || !request.QrCodeData.StartsWith("ORD-"))
        {
            return BadRequest(new { success = false, message = "Mã QR không hợp lệ. Vui lòng quét mã do Aura Cinema cung cấp." });
        }

        var orderCode = request.QrCodeData;

        var order = await _db.Orders
            .Include(o => o.User)
            .Include(o => o.Showtime).ThenInclude(s => s.Movie)
            .FirstOrDefaultAsync(o => o.OrderCode == orderCode);

        if (order == null)
        {
            return NotFound(new { success = false, message = "Không tìm thấy vé trong hệ thống." });
        }

        if (order.Status == "Da su dung")
        {
            return Ok(new { 
                success = false, 
                message = "Vé này ĐÃ ĐƯỢC SỬ DỤNG trước đó!", 
                orderInfo = new { id = order.OrderCode, movie = order.Showtime.Movie.Title, customer = order.User.FullName } 
            });
        }

        if (order.Status != "Da thanh toan")
        {
            return BadRequest(new { 
                success = false, 
                message = $"Vé đang ở trạng thái '{order.Status}' và không thể sử dụng." 
            });
        }

        // Validate showtime (e.g. check if it's the correct day)
        // Optionally: if (order.Showtime.StartTime.Date != DateTime.Today) { ... }

        // Mark as used
        order.Status = "Da su dung";
        await _db.SaveChangesAsync();

        return Ok(new { 
            success = true, 
            message = "Check-in vé THÀNH CÔNG!",
            orderInfo = new { 
                id = order.OrderCode, 
                movie = order.Showtime.Movie.Title, 
                customer = order.User.FullName,
                printUrl = Url.Action("PrintTicket", new { orderCode = order.OrderCode })
            }
        });
    }

    [HttpGet]
    public async Task<IActionResult> PrintTicket(string orderCode)
    {
        var order = await _db.Orders
            .Include(o => o.User)
            .Include(o => o.OrderSeats).ThenInclude(os => os.Seat)
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
