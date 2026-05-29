using AuraCinema.Infrastructure.Data;
using AuraCinema.Web.ViewModels.MyTickets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System.Security.Claims;

namespace AuraCinema.Web.Controllers;

[Authorize]
public class MyTicketsController : Controller
{
    private readonly AppDbContext _db;

    public MyTicketsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int userId)) return RedirectToAction("Login", "Account");

        var orders = await _db.Orders
            .Include(o => o.Showtime).ThenInclude(s => s.Movie)
            .Include(o => o.Showtime).ThenInclude(s => s.Room)
            .Include(o => o.OrderSeats).ThenInclude(os => os.Seat)
            .Where(o => o.UserID == userId)
            .OrderByDescending(o => o.OrderID)
            .ToListAsync();

        var vm = orders.Select(o => new TicketItemViewModel
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
            },
            HoldExpiryTime = o.HoldExpiryTime
        }).ToList();

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int userId)) return RedirectToAction("Login", "Account");

        var order = await _db.Orders
            .Include(o => o.Showtime).ThenInclude(s => s.Movie)
            .Include(o => o.Showtime).ThenInclude(s => s.Room)
            .Include(o => o.OrderSeats).ThenInclude(os => os.Seat)
            .Include(o => o.OrderServices).ThenInclude(os => os.Service)
            .Include(o => o.Promotion)
            .FirstOrDefaultAsync(o => o.OrderID == id && o.UserID == userId);

        if (order == null) return NotFound();

        var statusFriendly = order.Status switch
        {
            "Da thanh toan" or "Đã thanh toán" => "Đã thanh toán",
            "Cho thanh toan" or "Chờ thanh toán" => "Chờ thanh toán",
            "Da su dung" or "Đã sử dụng" => "Đã sử dụng",
            "can hoan tien" or "Cần hoàn tiền" => "Cần hoàn tiền",
            "da huy" or "Đã hủy" => "Đã hủy",
            "Đã hoàn tiền" or "da hoan tien" => "Đã hoàn tiền",
            _ => order.Status
        };

        var vm = new TicketDetailViewModel
        {
            OrderID = order.OrderID,
            OrderCode = order.OrderCode,
            MovieTitle = order.Showtime.Movie.Title,
            Poster = order.Showtime.Movie.Poster,
            RoomName = order.Showtime.Room.RoomName,
            Showtime = order.Showtime.StartTime,
            SeatList = string.Join(", ", order.OrderSeats.Select(s => $"{s.Seat.RowLabel}{s.Seat.SeatNumber}")),
            FinalAmount = order.FinalAmount,
            Status = statusFriendly,
            CreatedAt = order.HoldExpiryTime.AddMinutes(-10), // Updated from -5 to -10 to match 10m hold
            PayOSTransID = order.PayOSTransID,
            PromotionTitle = order.Promotion?.Title,
            BaseAmount = order.TotalAmount,
            Discount = order.Promotion?.DiscountValue ?? 0,
            ServiceList = order.OrderServices.Select(os => $"{os.Service.ServiceName} x{os.Quantity}").ToList()
        };

        // Generate QR Code if Paid
        if (statusFriendly == "Đã thanh toán")
        {
            string qrData = order.OrderCode; // Changed from AURA-{order.OrderID} to OrderCode
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(qrData, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new Base64QRCode(qrCodeData);
            vm.QrCodeBase64 = qrCode.GetGraphic(20);
        }

        return View(vm);
    }
}
