using AuraCinema.Domain.Models.Booking;

namespace AuraCinema.Domain.Interfaces.Services;

using AuraCinema.Domain.Entities;

public interface IBookingService
{
    // Lấy thông tin phòng chiếu và trạng thái ghế cho một suất chiếu cụ thể
    Task<(Showtime Showtime, List<Seat> Seats, List<int> SoldOrHeldSeatIds)> GetShowtimeSeatLayoutAsync(int showtimeId);

    // Tính toán tổng tiền dựa trên các ghế đã chọn, dịch vụ và các phụ phí
    Task<(int TotalAmount, int FinalAmount, string PriceDetails)> CalculatePriceAsync(int showtimeId, List<int> seatIds, List<ServiceSelection> services, string? promoCode = null);

    // Tạo đơn hàng, khóa ghế trong 10 phút
    Task<(bool Success, string Message, int OrderId)> CreateHoldOrderAsync(int userId, int showtimeId, List<int> seatIds, List<ServiceSelection> services, string? promoCode = null);

    // Huỷ đơn hàng khi hết hạn giữ ghế (Job hoặc user tự huỷ)
    Task<bool> CancelOrderAsync(int orderId);

    // Sinh link thanh toán PayOS
    Task<(bool Success, string CheckoutUrl, string? ErrorMessage)> GeneratePayOSPaymentUrlAsync(int orderId, string cancelUrl, string returnUrl);

    // Xử lý Webhook từ PayOS thành công
    Task<bool> ProcessSuccessfulPaymentAsync(int orderId, string transactionId);

    // Kiểm tra chủ động trạng thái đơn hàng từ PayOS
    Task<bool> CheckPaymentStatusAsync(int orderId);
    
    // Lấy danh sách khuyến mãi phù hợp với tổng tiền đơn hàng
    Task<List<Promotion>> GetAvailablePromotionsAsync(int totalAmount);

    // Áp dụng khuyến mãi vào đơn hàng
    Task<(bool Success, string Message)> ApplyPromotionAsync(int orderId, int? promoId);

    // Lấy thông tin đơn hàng
    Task<Order?> GetOrderByIdAsync(int orderId);
}
