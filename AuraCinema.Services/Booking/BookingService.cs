using AuraCinema.Domain.Entities;
using AuraCinema.Domain.Interfaces.Services;
using AuraCinema.Domain.Models.Booking;
using AuraCinema.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Net.payOS;
using Net.payOS.Types;

namespace AuraCinema.Services.Booking;

public class BookingService : IBookingService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IEmailService _emailService;

    public BookingService(AppDbContext db, IConfiguration config, IEmailService emailService)
    {
        _db = db;
        _config = config;
        _emailService = emailService;
    }

    public async Task<(Showtime Showtime, List<Seat> Seats, List<int> SoldOrHeldSeatIds)> GetShowtimeSeatLayoutAsync(int showtimeId)
    {
        var showtime = await _db.Showtimes
            .Include(s => s.Movie)
            .Include(s => s.Room)
            .FirstOrDefaultAsync(s => s.ShowtimeID == showtimeId);

        if (showtime == null)
            throw new Exception("Showtime not found");

        var seats = await _db.Seats
            .Where(s => s.RoomID == showtime.RoomID)
            .OrderBy(s => s.RowLabel).ThenBy(s => s.SeatNumber)
            .ToListAsync();

        var soldOrHeldOrderSeats = await _db.OrderSeats
            .Include(os => os.Order)
            .Where(os => os.Order.ShowtimeID == showtimeId &&
                         (os.Order.Status == "Da thanh toan" || 
                         (os.Order.Status == "Cho thanh toan" && os.Order.HoldExpiryTime > DateTime.UtcNow)))
            .ToListAsync();

        var soldOrHeldSeatIds = soldOrHeldOrderSeats.Select(os => os.SeatID).ToList();

        return (showtime, seats, soldOrHeldSeatIds);
    }

    public async Task<(int TotalAmount, int FinalAmount, string PriceDetails)> CalculatePriceAsync(int showtimeId, List<int> seatIds, List<ServiceSelection> selectedServices, string? promoCode = null)
    {
        var showtime = await _db.Showtimes.FindAsync(showtimeId);
        if (showtime == null) throw new Exception("Showtime not found");

        var seats = await _db.Seats.Where(s => seatIds.Contains(s.SeatID)).ToListAsync();
        
        var svcIds = selectedServices.Where(s => s.Quantity > 0).Select(s => s.ServiceID).ToList();
        var services = await _db.Services.Where(s => svcIds.Contains(s.ServiceID)).ToListAsync();

        var configs = await _db.PriceConfigs.ToDictionaryAsync(c => c.ConfigCode, c => c.SurchargeAmount);
        
        int basePrice = configs.GetValueOrDefault("BASE", 70000);
        int daySurcharge = (showtime.StartTime.DayOfWeek == DayOfWeek.Saturday || showtime.StartTime.DayOfWeek == DayOfWeek.Sunday) ? configs.GetValueOrDefault("DAY_WEEKEND", 15000) : 0;
        
        int totalAmount = 0;
        var details = new Dictionary<string, object>();

        // Ghế
        foreach (var seat in seats)
        {
            int seatPrice = basePrice + daySurcharge;
            if (seat.SeatType == "VIP") seatPrice += configs.GetValueOrDefault("SEAT_VIP", 20000);
            else if (seat.SeatType == "Doi") seatPrice += configs.GetValueOrDefault("SEAT_COUPLE", 50000);
            totalAmount += seatPrice;
        }

        // Dịch vụ
        foreach (var svc in services)
        {
            var qty = selectedServices.First(s => s.ServiceID == svc.ServiceID).Quantity;
            totalAmount += svc.Price * qty;
        }

        details.Add("SeatsCount", seats.Count);
        details.Add("BaseTotal", totalAmount);

        int finalAmount = totalAmount;
        if (!string.IsNullOrEmpty(promoCode))
        {
            var promo = await _db.Promotions
                .FirstOrDefaultAsync(p => p.Title == promoCode && p.Status == "Hoat dong" && p.StartDate <= DateTime.Now && p.EndDate >= DateTime.Now);
                
            if (promo != null)
            {
                finalAmount -= promo.DiscountValue;
                if (finalAmount < 0) finalAmount = 0;
                details.Add("Discount", -promo.DiscountValue);
            }
        }

        return (totalAmount, finalAmount, JsonSerializer.Serialize(details));
    }

    public async Task<(bool Success, string Message, int OrderId)> CreateHoldOrderAsync(int userId, int showtimeId, List<int> seatIds, List<ServiceSelection> selectedServices, string? promoCode = null)
    {
        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            // 1. Kiểm tra lại ghế có còn trống không (Double-check trước khi lock)
            var (_, _, soldOrHeldSeatIds) = await GetShowtimeSeatLayoutAsync(showtimeId);
            
            if (seatIds.Any(id => soldOrHeldSeatIds.Contains(id)))
            {
                return (false, "Một số ghế đã bị người khác đặt trong lúc bạn chọn dịch vụ. Vui lòng chọn ghế khác.", 0);
            }

            // 2. Tính toán giá
            var (total, final, _) = await CalculatePriceAsync(showtimeId, seatIds, selectedServices, promoCode);

            int? promoId = null;
            if (!string.IsNullOrEmpty(promoCode))
            {
                var promo = await _db.Promotions.FirstOrDefaultAsync(p => p.Title == promoCode);
                promoId = promo?.PromoID;
            }

            int holdMinutes = int.TryParse(_config["BookingHoldMinutes"], out int mins) ? mins : 10;
            string generatedOrderCode = AuraCinema.Domain.Helpers.CodeGenerator.GenerateOrderCode();

            var order = new Order
            {
                OrderCode = generatedOrderCode,
                UserID = userId,
                ShowtimeID = showtimeId,
                PromoID = promoId,
                TotalAmount = total,
                FinalAmount = final,
                Status = "Cho thanh toan",
                HoldExpiryTime = DateTime.UtcNow.AddMinutes(holdMinutes),
            };

            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            // 3. Lưu OrderSeats
            var showtime = await _db.Showtimes.FindAsync(showtimeId);
            var seats = await _db.Seats.Where(s => seatIds.Contains(s.SeatID)).ToListAsync();
            var configs = await _db.PriceConfigs.ToDictionaryAsync(c => c.ConfigCode, c => c.SurchargeAmount);
            int basePrice = configs.GetValueOrDefault("BASE", 70000);
            int daySurcharge = (showtime!.StartTime.DayOfWeek == DayOfWeek.Saturday || showtime.StartTime.DayOfWeek == DayOfWeek.Sunday) ? configs.GetValueOrDefault("DAY_WEEKEND", 15000) : 0;

            foreach (var seat in seats)
            {
                int seatPrice = basePrice + daySurcharge;
                if (seat.SeatType == "VIP") seatPrice += configs.GetValueOrDefault("SEAT_VIP", 20000);
                else if (seat.SeatType == "Doi") seatPrice += configs.GetValueOrDefault("SEAT_COUPLE", 50000);

                _db.OrderSeats.Add(new OrderSeat
                {
                    OrderID = order.OrderID,
                    SeatID = seat.SeatID,
                    Price = seatPrice,
                    Status = "Tam khoa"
                });
            }

            // 4. Lưu OrderServices
            var svcIds = selectedServices.Where(s => s.Quantity > 0).Select(s => s.ServiceID).ToList();
            var services = await _db.Services.Where(s => svcIds.Contains(s.ServiceID)).ToListAsync();
            foreach (var svc in services)
            {
                var qty = selectedServices.First(s => s.ServiceID == svc.ServiceID).Quantity;
                _db.OrderServices.Add(new OrderService
                {
                    OrderID = order.OrderID,
                    ServiceID = svc.ServiceID,
                    Price = svc.Price,
                    Quantity = qty
                });
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return (true, "Giữ ghế thành công.", order.OrderID);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return (false, $"Lỗi hệ thống: {ex.Message}", 0);
        }
    }

    public async Task<bool> CancelOrderAsync(int orderId)
    {
        var order = await _db.Orders.FindAsync(orderId);
        if (order == null || order.Status == "Da thanh toan" || order.Status == "Da huy")
            return false;

        order.Status = "Da huy";
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<(bool Success, string CheckoutUrl)> GeneratePayOSPaymentUrlAsync(int orderId, string cancelUrl, string returnUrl)
    {
        var order = await _db.Orders
            .Include(o => o.Showtime)
                .ThenInclude(s => s.Movie)
            .FirstOrDefaultAsync(o => o.OrderID == orderId);

        if (order == null) return (false, string.Empty);

        var payOS = new PayOS(
            _config["PayOS:ClientId"] ?? "",
            _config["PayOS:ApiKey"] ?? "",
            _config["PayOS:ChecksumKey"] ?? ""
        );

        long orderCode = long.Parse($"{orderId}{DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString().Substring(6)}");
        order.PayOSTransID = orderCode.ToString();
        await _db.SaveChangesAsync();

        var item = new ItemData($"Ve xem phim {order.Showtime.Movie.Title}", 1, order.FinalAmount);
        var items = new List<ItemData> { item };

        var paymentData = new PaymentData(
            orderCode: orderCode,
            amount: order.FinalAmount,
            description: $"Thanh toan ve #{orderId}",
            items: items,
            cancelUrl: cancelUrl,
            returnUrl: returnUrl
        );

        try
        {
            var result = await payOS.createPaymentLink(paymentData);
            return (true, result.checkoutUrl);
        }
        catch (Exception)
        {
            return (false, string.Empty);
        }
    }

    public async Task<bool> ProcessSuccessfulPaymentAsync(int orderId, string transactionId)
    {
        var order = await _db.Orders
            .Include(o => o.User)
            .Include(o => o.OrderSeats)
            .FirstOrDefaultAsync(o => o.OrderID == orderId);

        if (order == null || order.Status == "Da thanh toan")
            return true;

        order.Status = "Da thanh toan";
        order.PayOSTransID = transactionId;
        order.QrCode = Guid.NewGuid().ToString();

        foreach (var os in order.OrderSeats)
        {
            os.Status = "Da ban";
        }

        await _db.SaveChangesAsync();

        if (order.User != null)
        {
            await _emailService.SendTicketConfirmationAsync(order.User.Email, order.User.FullName, order.OrderID);
        }

        return true;
    }

    public async Task<Order?> GetOrderByIdAsync(int id)
    {
        return await _db.Orders
            .Include(o => o.User)
            .Include(o => o.Showtime)
                .ThenInclude(s => s.Movie)
            .Include(o => o.Showtime)
                .ThenInclude(s => s.Room)
            .Include(o => o.Promotion)
            .Include(o => o.OrderSeats)
                .ThenInclude(os => os.Seat)
            .Include(o => o.OrderServices)
                .ThenInclude(os => os.Service)
            .FirstOrDefaultAsync(o => o.OrderID == id);
    }
}
