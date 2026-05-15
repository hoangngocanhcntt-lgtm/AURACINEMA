using AuraCinema.Domain.Interfaces.Services;
using AuraCinema.Domain.Models.Booking;
using AuraCinema.Infrastructure.Data;
using AuraCinema.Web.ViewModels.Booking;
using AuraCinema.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Net.payOS.Types;
using System.Security.Claims;
using System.Text.Json;

namespace AuraCinema.Web.Controllers;

[Authorize]
public class BookingController : Controller
{
    private readonly IBookingService _bookingService;
    private readonly AppDbContext _db;

    public BookingController(IBookingService bookingService, AppDbContext db)
    {
        _bookingService = bookingService;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> SelectSeats(int showtimeId)
    {
        try
        {
            var (showtime, seats, soldOrHeldSeatIds) = await _bookingService.GetShowtimeSeatLayoutAsync(showtimeId);

            var rows = seats.GroupBy(s => s.RowLabel)
                .Select(g => new SeatRowViewModel
                {
                    RowLabel = g.Key,
                    Seats = g.Select(s => new SeatItemViewModel
                    {
                        SeatID = s.SeatID,
                        SeatNumber = s.SeatNumber,
                        SeatType = s.SeatType
                    }).ToList()
                }).ToList();

            var configs = await _db.PriceConfigs.ToDictionaryAsync(c => c.ConfigCode, c => c.SurchargeAmount);

            var vm = new SelectSeatsViewModel
            {
                ShowtimeID = showtime.ShowtimeID,
                MovieTitle = showtime.Movie.Title,
                RoomName = showtime.Room.RoomName,
                ShowtimeLabel = $"{showtime.StartTime:HH:mm} - {showtime.StartTime:dd/MM/yyyy}",
                MoviePoster = showtime.Movie.Poster,
                Rows = rows,
                SoldOrHeldSeatIds = soldOrHeldSeatIds,
                BasePrice = configs.GetValueOrDefault("BASE", 70000),
                VipSurcharge = configs.GetValueOrDefault("SEAT_VIP", 20000),
                CoupleSurcharge = configs.GetValueOrDefault("SEAT_COUPLE", 50000),
                DaySurcharge = (showtime.StartTime.DayOfWeek == DayOfWeek.Saturday || showtime.StartTime.DayOfWeek == DayOfWeek.Sunday) ? configs.GetValueOrDefault("DAY_WEEKEND", 15000) : 0
            };

            return View(vm);
        }
        catch (Exception)
        {
            return RedirectToAction("Index", "Movies");
        }
    }

    [HttpGet]
    public async Task<IActionResult> AddServices(int showtimeId, string seatIds)
    {
        if (string.IsNullOrEmpty(seatIds)) return RedirectToAction("SelectSeats", new { showtimeId });

        var ids = seatIds.Split(',').Select(int.Parse).ToList();
        
        var showtime = await _db.Showtimes
            .Include(s => s.Movie)
            .FirstOrDefaultAsync(s => s.ShowtimeID == showtimeId);
        
        if (showtime == null) return NotFound();

        var seats = await _db.Seats.Where(s => ids.Contains(s.SeatID)).ToListAsync();
        var services = await _db.Services.Where(s => s.Status == "Hoat dong").ToListAsync();

        var vm = new AddServicesViewModel
        {
            ShowtimeID = showtimeId,
            MovieTitle = showtime.Movie.Title,
            ShowtimeLabel = $"{showtime.StartTime:HH:mm} - {showtime.StartTime:dd/MM/yyyy}",
            SeatList = string.Join(", ", seats.Select(s => $"{s.RowLabel}{s.SeatNumber}")),
            SelectedSeatIds = ids,
            AvailableServices = services
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmBooking([FromBody] ConfirmBookingRequest request)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out int userId))
            return Unauthorized(new { success = false, message = "Vui lòng đăng nhập lại." });

        var (success, message, orderId) = await _bookingService.CreateHoldOrderAsync(userId, request.ShowtimeId, request.SeatIds, request.Services);

        if (success)
        {
            return Ok(new { success = true, redirectUrl = Url.Action("Checkout", new { orderId = orderId }) });
        }

        return BadRequest(new { success = false, message = message });
    }

    [HttpGet]
    public async Task<IActionResult> Checkout(int orderId, string? promoCode = null)
    {
        var order = await _bookingService.GetOrderByIdAsync(orderId);
        
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (order == null || order.UserID.ToString() != userIdString)
            return NotFound();

        if (order.Status != "Cho thanh toan" || order.HoldExpiryTime <= DateTime.UtcNow)
        {
            TempData["Error"] = "Đơn hàng đã hết hạn hoặc đã được xử lý.";
            return RedirectToAction("SelectSeats", new { showtimeId = order.ShowtimeID });
        }

        // Apply promo logic
        int finalTotal = order.TotalAmount;
        int discount = 0;
        string? promoError = null;

        if (!string.IsNullOrEmpty(promoCode))
        {
            var promo = await _db.Promotions
                .FirstOrDefaultAsync(p => p.Title == promoCode && p.Status == "Hoat dong" && p.StartDate <= DateTime.Now && p.EndDate >= DateTime.Now);

            if (promo != null)
            {
                discount = promo.DiscountValue;
                finalTotal -= discount;
                if (finalTotal < 0) finalTotal = 0;
                
                order.PromoID = promo.PromoID;
                order.FinalAmount = finalTotal;
                await _db.SaveChangesAsync();
            }
            else
            {
                promoError = "Mã khuyến mãi không hợp lệ hoặc đã hết hạn.";
            }
        }

        var vm = new CheckoutViewModel
        {
            OrderID = order.OrderID,
            MovieTitle = order.Showtime.Movie.Title,
            RoomName = order.Showtime.Room.RoomName,
            ShowtimeLabel = $"{order.Showtime.StartTime:HH:mm} - {order.Showtime.StartTime:dd/MM/yyyy}",
            MoviePoster = order.Showtime.Movie.Poster,
            SeatList = string.Join(", ", order.OrderSeats.Select(os => $"{os.Seat.RowLabel}{os.Seat.SeatNumber}")),
            BaseTotal = order.TotalAmount,
            Discount = discount,
            FinalTotal = finalTotal,
            HoldExpiryTime = order.HoldExpiryTime,
            PromoCode = promoCode,
            PromoError = promoError
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessCheckout(int orderId)
    {
        var cancelUrl = Url.Action("PaymentCancel", "Booking", new { orderId = orderId }, Request.Scheme);
        var returnUrl = Url.Action("PaymentSuccess", "Booking", new { orderId = orderId }, Request.Scheme);

        var (success, checkoutUrl) = await _bookingService.GeneratePayOSPaymentUrlAsync(orderId, cancelUrl!, returnUrl!);

        if (success)
        {
            return Redirect(checkoutUrl);
        }

        TempData["Error"] = "Có lỗi xảy ra khi tạo cổng thanh toán. Vui lòng thử lại.";
        return RedirectToAction("Checkout", new { orderId = orderId });
    }

    [HttpGet]
    public async Task<IActionResult> PaymentSuccess(int orderId)
    {
        await Task.Delay(2000);
        var order = await _bookingService.GetOrderByIdAsync(orderId);
        if (order?.Status == "Da thanh toan")
        {
            return View("PaymentResult", order);
        }
        return RedirectToAction("Index", "Movies");
    }

    [HttpGet]
    public async Task<IActionResult> PaymentCancel(int orderId)
    {
        await _bookingService.CancelOrderAsync(orderId);
        TempData["Error"] = "Thanh toán đã bị hủy.";
        return RedirectToAction("Index", "Movies");
    }

    [HttpPost]
    [AllowAnonymous]
    [Route("api/payos/webhook")]
    public async Task<IActionResult> Webhook([FromBody] WebhookType payload)
    {
        try
        {
            if (payload.data.code == "00" || payload.data.desc == "success")
            {
                string orderCodeStr = payload.data.orderCode.ToString();
                var order = await _db.Orders.FirstOrDefaultAsync(o => o.PayOSTransID == orderCodeStr);
                if (order != null)
                {
                    await _bookingService.ProcessSuccessfulPaymentAsync(order.OrderID, payload.data.reference);
                }
            }
            return Ok(new { success = true });
        }
        catch (Exception)
        {
            return BadRequest(new { success = false });
        }
    }
}
