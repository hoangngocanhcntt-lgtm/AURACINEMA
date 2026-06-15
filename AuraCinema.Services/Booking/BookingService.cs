using AuraCinema.Domain.Entities;
using AuraCinema.Domain.Interfaces.Services;
using AuraCinema.Domain.Models.Booking;
using AuraCinema.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Net.payOS;
using Net.payOS.Types;

namespace AuraCinema.Services.Booking;

public class BookingService : IBookingService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IEmailService _emailService;
    private readonly ILogger<BookingService> _logger;

    public BookingService(AppDbContext db, IConfiguration config, IEmailService emailService, ILogger<BookingService> logger)
    {
        _db = db;
        _config = config;
        _emailService = emailService;
        _logger = logger;
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
                         (os.Order.Status == "Đã thanh toán" || 
                         (os.Order.Status == "Chờ thanh toán" && os.Order.HoldExpiryTime > DateTime.Now)))
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

        var configsList = await _db.PriceConfigs.ToListAsync();
        var configs = configsList.ToDictionary(c => c.ConfigCode.Trim(), c => c.ActiveSurchargeAmount);
        
        int basePrice = ResolveConfig(configs, "BASE_PRICE", "BASE");
        int daySurcharge = (showtime.StartTime.DayOfWeek == DayOfWeek.Saturday || showtime.StartTime.DayOfWeek == DayOfWeek.Sunday) ? ResolveConfig(configs, "WEEKEND_SURCHARGE", "DAY_WEEKEND") : 0;
        int eveningSurcharge = (showtime.StartTime.Hour >= 18) ? ResolveConfig(configs, "EVENING_SURCHARGE", "DAY_EVENING") : 0;
        
        int totalAmount = 0;
        var details = new Dictionary<string, object>();

        int doubleSeatCount = 0;
        // Ghế
        foreach (var seat in seats)
        {
            int seatPrice = basePrice + daySurcharge + eveningSurcharge;
            if (seat.SeatType == "VIP") seatPrice += ResolveConfig(configs, "VIP_SURCHARGE", "SEAT_VIP");
            else if (seat.SeatType == "Doi") doubleSeatCount++;
            
            totalAmount += seatPrice;
        }

        // Add surcharge per pair of double seats
        int couplePairs = (int)Math.Ceiling(doubleSeatCount / 2.0);
        totalAmount += couplePairs * ResolveConfig(configs, "COUPLE_SURCHARGE", "SEAT_COUPLE");

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
                Status = "Chờ thanh toán",
                HoldExpiryTime = DateTime.Now.AddMinutes(holdMinutes),
            };

            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            // 3. Lưu OrderSeats
            var showtime = await _db.Showtimes.Include(s => s.Movie).FirstOrDefaultAsync(s => s.ShowtimeID == showtimeId);
            var seats = await _db.Seats.Where(s => seatIds.Contains(s.SeatID)).ToListAsync();
            var configsList = await _db.PriceConfigs.ToListAsync();
            var configs = configsList.ToDictionary(c => c.ConfigCode.Trim(), c => c.ActiveSurchargeAmount);
            int basePrice = ResolveConfig(configs, "BASE_PRICE", "BASE");
            int daySurcharge = (showtime!.StartTime.DayOfWeek == DayOfWeek.Saturday || showtime.StartTime.DayOfWeek == DayOfWeek.Sunday) ? ResolveConfig(configs, "WEEKEND_SURCHARGE", "DAY_WEEKEND") : 0;
            int eveningSurcharge = (showtime.StartTime.Hour >= 18) ? ResolveConfig(configs, "EVENING_SURCHARGE", "DAY_EVENING") : 0;
            int earlySurcharge = (showtime.Movie.Status == "Sap chieu") ? ResolveConfig(configs, "EARLY_SURCHARGE", "EARLY_SURCHARGE") : 0;

            int doubleSeatCount = 0;
            foreach (var seat in seats)
            {
                int seatPrice = basePrice + daySurcharge + eveningSurcharge + earlySurcharge;
                if (seat.SeatType == "VIP") seatPrice += ResolveConfig(configs, "VIP_SURCHARGE", "SEAT_VIP");
                else if (seat.SeatType == "Doi") 
                {
                    doubleSeatCount++;
                    // Phụ thu ghế đôi chỉ được cộng vào ghế số lẻ của cặp (VD: ghế thứ 1, 3, 5...) để tổng giá của cặp là đúng
                    if (doubleSeatCount % 2 != 0)
                    {
                        seatPrice += ResolveConfig(configs, "COUPLE_SURCHARGE", "SEAT_COUPLE");
                    }
                }

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
        if (order == null || order.Status == "Đã thanh toán" || order.Status == "Đã hủy")
            return false;

        order.Status = "Đã hủy";
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<(bool Success, string CheckoutUrl, string? ErrorMessage)> GeneratePayOSPaymentUrlAsync(int orderId, string cancelUrl, string returnUrl)
    {
        var order = await _db.Orders
            .Include(o => o.Showtime)
                .ThenInclude(s => s.Movie)
            .FirstOrDefaultAsync(o => o.OrderID == orderId);

        if (order == null) return (false, string.Empty, "Không tìm thấy đơn hàng.");

        // PayOS yêu cầu tối thiểu 2000 VND
        if (order.FinalAmount < 2000)
        {
            return (false, string.Empty, "Số tiền thanh toán tối thiểu là 2.000đ. Vui lòng kiểm tra lại đơn hàng.");
        }

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

        // Description không được chứa ký tự đặc biệt như #
        var paymentData = new PaymentData(
            orderCode: orderCode,
            amount: order.FinalAmount,
            description: $"Thanh toan ve {orderId}",
            items: items,
            cancelUrl: cancelUrl,
            returnUrl: returnUrl
        );

        try
        {
            var result = await payOS.createPaymentLink(paymentData);
            return (true, result.checkoutUrl, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PayOS Error: {ex.Message}");
            return (false, string.Empty, $"Lỗi từ PayOS: {ex.Message}");
        }
    }

    public async Task<bool> CheckPaymentStatusAsync(int orderId)
    {
        var order = await _db.Orders.FindAsync(orderId);
        if (order == null || string.IsNullOrEmpty(order.PayOSTransID)) return false;

        if (order.Status == "Đã thanh toán") return true;

        var payOS = new PayOS(
            _config["PayOS:ClientId"] ?? "",
            _config["PayOS:ApiKey"] ?? "",
            _config["PayOS:ChecksumKey"] ?? ""
        );

        try
        {
            var paymentInfo = await payOS.getPaymentLinkInformation(long.Parse(order.PayOSTransID));
            
            if (paymentInfo.status == "PAID")
            {
                await ProcessSuccessfulPaymentAsync(orderId, paymentInfo.transactions.LastOrDefault()?.reference ?? "ACTIVE_CHECK");
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking payment status for Order {OrderId}", orderId);
        }

        return false;
    }

    public async Task<bool> ProcessSuccessfulPaymentAsync(int orderId, string transactionId)
    {
        var order = await _db.Orders
            .Include(o => o.User)
            .Include(o => o.OrderSeats)
            .FirstOrDefaultAsync(o => o.OrderID == orderId);

        if (order == null)
        {
            _logger.LogWarning("Webhook payment received for non-existent Order ID: {OrderId}", orderId);
            return false;
        }

        // Xử lý trường hợp thanh toán muộn (Đơn hàng đã bị hủy hoặc đã quá hạn giữ ghế)
        // Thêm 5-10 giây buffer cho các trường hợp ngân hàng delay phản hồi
        if (order.Status == "Đã hủy" || order.HoldExpiryTime.AddSeconds(10) < DateTime.Now)
        {
            _logger.LogCritical("LATE PAYMENT DETECTED: Order {OrderId} is expired. Status: {Status}, Expiry: {Expiry}. Redirecting to Refund.", 
                orderId, order.Status, order.HoldExpiryTime);
            
            order.Status = "Cần hoàn tiền";
            order.PayOSTransID = transactionId;
            await _db.SaveChangesAsync();
            
            return true;
        }

        _logger.LogInformation("Processing successful payment for Order {OrderId}. PayOS Ref: {Ref}", orderId, transactionId);
        
        order.Status = "Đã thanh toán";
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

    public async Task<List<Promotion>> GetAvailablePromotionsAsync(int totalAmount)
    {
        return await _db.Promotions
            .Where(p => p.Status == "Hoat dong" && 
                        p.StartDate <= DateTime.Now && 
                        p.EndDate >= DateTime.Now &&
                        p.MinAmount <= totalAmount)
            .OrderByDescending(p => p.DiscountValue)
            .ToListAsync();
    }

    public async Task<(bool Success, string Message)> ApplyPromotionAsync(int orderId, int? promoId)
    {
        var order = await _db.Orders.FindAsync(orderId);
        if (order == null) return (false, "Không tìm thấy đơn hàng.");

        if (order.Status != "Chờ thanh toán") 
            return (false, "Đơn hàng không ở trạng thái có thể áp dụng khuyến mãi.");

        if (promoId == null)
        {
            order.PromoID = null;
            order.FinalAmount = order.TotalAmount;
            await _db.SaveChangesAsync();
            return (true, "Đã gỡ bỏ khuyến mãi.");
        }

        var promo = await _db.Promotions.FindAsync(promoId);
        if (promo == null || promo.Status != "Hoat dong" || promo.StartDate > DateTime.Now || promo.EndDate < DateTime.Now)
            return (false, "Khuyến mãi không hợp lệ hoặc đã hết hạn.");

        if (promo.MinAmount > order.TotalAmount)
            return (false, $"Đơn hàng chưa đạt giá trị tối thiểu {promo.MinAmount:N0}đ để áp dụng khuyến mãi này.");

        order.PromoID = promo.PromoID;
        order.FinalAmount = order.TotalAmount - promo.DiscountValue;
        if (order.FinalAmount < 0) order.FinalAmount = 0;

        await _db.SaveChangesAsync();
        return (true, "Áp dụng khuyến mãi thành công.");
    }

    /// <summary>Tìm giá theo ConfigCode chính, nếu không có thì thử ConfigCode cũ (backwards compat với seed data cũ).</summary>
    private static int ResolveConfig(Dictionary<string, int> configs, string primaryKey, string fallbackKey)
    {
        if (configs.TryGetValue(primaryKey, out var val)) return val;
        if (configs.TryGetValue(fallbackKey, out var fallbackVal)) return fallbackVal;
        return 0;
    }
}
