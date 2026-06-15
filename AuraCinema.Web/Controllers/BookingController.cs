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
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using QRCoder;

namespace AuraCinema.Web.Controllers;

[Authorize]
public class BookingController : Controller
{
    private readonly IBookingService _bookingService;
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<BookingController> _logger;

    public BookingController(IBookingService bookingService, AppDbContext db, IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<BookingController> logger)
    {
        _bookingService = bookingService;
        _db = db;
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
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

            var configsList = await _db.PriceConfigs.ToListAsync();
            var configs = configsList.ToDictionary(c => c.ConfigCode.Trim(), c => c.ActiveSurchargeAmount);

            var vm = new SelectSeatsViewModel
            {
                ShowtimeID = showtime.ShowtimeID,
                MovieTitle = showtime.Movie.Title,
                RoomName = showtime.Room.RoomName,
                ShowtimeLabel = $"{showtime.StartTime:HH:mm} - {showtime.StartTime:dd/MM/yyyy}",
                MoviePoster = showtime.Movie.Poster,
                Rows = rows,
                SoldOrHeldSeatIds = soldOrHeldSeatIds,
                BasePrice = ResolveConfig(configs, "BASE_PRICE", "BASE"),
                VipSurcharge = ResolveConfig(configs, "VIP_SURCHARGE", "SEAT_VIP"),
                CoupleSurcharge = ResolveConfig(configs, "COUPLE_SURCHARGE", "SEAT_COUPLE"),
                DaySurcharge = (showtime.StartTime.DayOfWeek == DayOfWeek.Saturday || showtime.StartTime.DayOfWeek == DayOfWeek.Sunday) ? ResolveConfig(configs, "WEEKEND_SURCHARGE", "DAY_WEEKEND") : 0,
                EveningSurcharge = (showtime.StartTime.Hour >= 18) ? ResolveConfig(configs, "EVENING_SURCHARGE", "DAY_EVENING") : 0,
                EarlySurcharge = (showtime.Movie.Status == "Sap chieu") ? ResolveConfig(configs, "EARLY_SURCHARGE", "EARLY_SURCHARGE") : 0
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
    public async Task<IActionResult> Checkout(int orderId, int? promoId = null)
    {
        var order = await _bookingService.GetOrderByIdAsync(orderId);
        
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (order == null || order.UserID.ToString() != userIdString)
            return NotFound();

        if (order.Status != "Chờ thanh toán" || order.HoldExpiryTime <= DateTime.Now)
        {
            TempData["Error"] = "Đơn hàng đã hết hạn hoặc đã được xử lý.";
            return RedirectToAction("SelectSeats", new { showtimeId = order.ShowtimeID });
        }

        // Apply promo logic if promoId is provided
        if (promoId.HasValue)
        {
            var (applySuccess, applyMsg) = await _bookingService.ApplyPromotionAsync(orderId, promoId.Value);
            if (!applySuccess)
            {
                ViewBag.PromoError = applyMsg;
            }
            // Refresh order after applying promo
            order = await _bookingService.GetOrderByIdAsync(orderId);
        }
        else if (Request.Query.ContainsKey("removePromo"))
        {
            await _bookingService.ApplyPromotionAsync(orderId, null);
            order = await _bookingService.GetOrderByIdAsync(orderId);
        }

        var availablePromos = await _bookingService.GetAvailablePromotionsAsync(order!.TotalAmount);

        var vm = new CheckoutViewModel
        {
            OrderID = order.OrderID,
            MovieTitle = order.Showtime.Movie.Title,
            RoomName = order.Showtime.Room.RoomName,
            ShowtimeLabel = $"{order.Showtime.StartTime:HH:mm} - {order.Showtime.StartTime:dd/MM/yyyy}",
            MoviePoster = order.Showtime.Movie.Poster,
            SeatList = string.Join(", ", order.OrderSeats.Select(os => $"{os.Seat.RowLabel}{os.Seat.SeatNumber}")),
            BaseTotal = order.TotalAmount,
            Discount = order.Promotion?.DiscountValue ?? 0,
            FinalTotal = order.FinalAmount,
            HoldExpiryTime = order.HoldExpiryTime,
            AvailablePromotions = availablePromos,
            SelectedPromoID = order.PromoID
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessCheckout(int orderId)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var order = await _bookingService.GetOrderByIdAsync(orderId);

        // Bảo mật: Kiểm tra đơn hàng có tồn tại và thuộc về người dùng hiện tại không
        if (order == null || order.UserID.ToString() != userIdString)
        {
            return NotFound();
        }

        // Kiểm tra trạng thái và hết hạn
        if (order.Status != "Chờ thanh toán" || order.HoldExpiryTime <= DateTime.Now)
        {
            TempData["Error"] = "Đơn hàng đã hết hạn hoặc đã được thanh toán.";
            return RedirectToAction("SelectSeats", new { showtimeId = order.ShowtimeID });
        }

        var cancelUrl = Url.Action("PaymentCancel", "Booking", new { orderId = orderId }, Request.Scheme);
        var returnUrl = Url.Action("PaymentSuccess", "Booking", new { orderId = orderId }, Request.Scheme);

        var (success, checkoutUrl, errorMsg) = await _bookingService.GeneratePayOSPaymentUrlAsync(orderId, cancelUrl!, returnUrl!);

        if (success)
        {
            return Redirect(checkoutUrl);
        }

        TempData["Error"] = errorMsg ?? "Có lỗi xảy ra khi tạo cổng thanh toán. Vui lòng thử lại.";
        return RedirectToAction("Checkout", new { orderId = orderId });
    }

    [HttpGet]
    public async Task<IActionResult> PaymentSuccess(int orderId)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var order = await _bookingService.GetOrderByIdAsync(orderId);

        if (order == null || order.UserID.ToString() != userIdString)
        {
            return NotFound();
        }

        // Active Check: Chủ động kiểm tra trạng thái từ PayOS ngay khi khách quay lại
        bool isPaid = await _bookingService.CheckPaymentStatusAsync(orderId);
        
        if (isPaid)
        {
            return RedirectToAction("Success", new { orderId = orderId });
        }

        // Nếu chưa thấy thanh toán (có thể do delay), hiển thị trang chờ
        return View("WaitingConfirmation", order);
    }

    [HttpGet]
    public async Task<IActionResult> Success(int orderId)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var order = await _bookingService.GetOrderByIdAsync(orderId);

        if (order == null || order.UserID.ToString() != userIdString)
        {
            return NotFound();
        }

        if (order.Status != "Đã thanh toán")
        {
            return RedirectToAction("PaymentSuccess", new { orderId = orderId });
        }

        // Tạo mã QR cho vé
        using (var qrGenerator = new QRCodeGenerator())
        {
            using (var qrCodeData = qrGenerator.CreateQrCode(order.OrderCode, QRCodeGenerator.ECCLevel.Q))
            {
                using (var qrCode = new Base64QRCode(qrCodeData))
                {
                    ViewBag.QrCodeBase64 = qrCode.GetGraphic(20);
                }
            }
        }

        return View("PaymentResult", order);
    }

    [HttpGet]
    public async Task<IActionResult> PaymentCancel(int orderId)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var order = await _bookingService.GetOrderByIdAsync(orderId);

        // Bảo mật
        if (order == null || order.UserID.ToString() != userIdString)
        {
            return NotFound();
        }

        await _bookingService.CancelOrderAsync(orderId);
        TempData["Info"] = "Thanh toán đã bị hủy.";
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
                else
                {
                    Console.WriteLine($"Webhook: Order with PayOSTransID {orderCodeStr} not found.");
                }
            }
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Webhook Error: {ex.Message}");
            return BadRequest(new { success = false });
        }
    }

    [HttpGet("api/booking/order-status")]
    [AllowAnonymous]
    public async Task<IActionResult> GetOrderStatus(int id, bool simulateRefund = false)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderID == id);
        if (order == null) return NotFound();

        if (simulateRefund)
        {
            order.Status = "Cần hoàn tiền";
            await _db.SaveChangesAsync();
        }

        return Ok(new { status = order.Status, payOSTransID = order.PayOSTransID });
    }

    [HttpGet]
    public async Task<IActionResult> RefundRequest(int orderId)
    {
        // 1. Kiểm tra đơn hàng có tồn tại không
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderID == orderId);
        if (order == null) return NotFound();

        // 2. Chỉ cho phép nhập form nếu trạng thái là "Cần hoàn tiền"
        if (order.Status != "Cần hoàn tiền")
        {
            TempData["Error"] = "Đơn hàng này không ở trạng thái cần hoàn tiền.";
            return RedirectToAction("Index", "Home");
        }

        return View(order);
    }

    [HttpPost]
    public async Task<IActionResult> SubmitRefundRequest(int orderId, string bankName, string accountNumber, string accountName)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderID == orderId && o.Status == "Cần hoàn tiền");

        if (order == null)
        {
            return BadRequest("Đơn hàng không hợp lệ hoặc không ở trạng thái cần hoàn tiền.");
        }

        try
        {
            // 1. Lấy thông tin cấu hình Kênh Chi
            var payoutConfig = _config.GetSection("PayOS_Payout");
            string clientId = (payoutConfig["ClientId"] ?? "").Trim();
            string apiKey = (payoutConfig["ApiKey"] ?? "").Trim();
            string checksumKey = (payoutConfig["ChecksumKey"] ?? "").Trim();

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(apiKey))
            {
                _logger.LogError("REFUND ERROR: PayOS Payout configuration missing.");
                return BadRequest("Hệ thống chưa được cấu hình Kênh Chi PayOS.");
            }

            _logger.LogInformation("START REFUND (FIX DESCRIPTION): Order {OrderId}", orderId);

            // 2. Chuẩn bị dữ liệu gửi đi
            string referenceId = $"REF{order.OrderID}{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            int amount = order.FinalAmount;
            string description = "Hoan tien AuraCinema"; 
            string toBin = GetBankBin(bankName).Trim();
            string cleanAccountNumber = accountNumber.Trim();

            // PayOS Payout chỉ chấp nhận 5 trường: amount, description, referenceId, toAccountNumber, toBin
            var rawDataForSignature = new Dictionary<string, string>
            {
                { "amount", amount.ToString() },
                { "description", description },
                { "referenceId", referenceId },
                { "toAccountNumber", cleanAccountNumber },
                { "toBin", toBin }
            };
            string signature = CreateSignature(rawDataForSignature, checksumKey);

            // Body JSON chỉ chứa 5 trường (KHÔNG có toAccountName, KHÔNG có signature)
            var payoutData = new
            {
                amount = amount,
                description = description,
                referenceId = referenceId,
                toAccountNumber = cleanAccountNumber,
                toBin = toBin
            };

            // 3. Gọi API PayOS Payout
            var client = _httpClientFactory.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api-merchant.payos.vn/v1/payouts");
            
            // Headers xác thực
            request.Headers.Add("x-client-id", clientId);
            request.Headers.Add("x-api-key", apiKey);
            request.Headers.Add("x-idempotency-key", referenceId);
            request.Headers.Add("x-signature", signature); // Signature đặt ở Header, KHÔNG ở Body
            
            var jsonContent = JsonSerializer.Serialize(payoutData);
            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _logger.LogInformation("SENDING PAYOUT: URL={Url}, Signature={Sig}, Payload={Payload}", request.RequestUri, signature, jsonContent);
            
            var response = await client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("PAYOS RESPONSE: Status={Status}, Body={Body}", response.StatusCode, responseBody);

            var responseData = JsonSerializer.Deserialize<JsonElement>(responseBody);
            string code = responseData.TryGetProperty("code", out var codeProp) ? codeProp.GetString() ?? "" : "";
            string desc = responseData.TryGetProperty("desc", out var descProp) ? descProp.GetString() ?? "" : "";

            if (response.IsSuccessStatusCode && code == "00")
            {
                // API trả về thành công thực sự
                var refund = new RefundRequest
                {
                    OrderID = order.OrderID,
                    BankName = bankName,
                    AccountNumber = accountNumber,
                    AccountName = accountName,
                    ResolvedAt = DateTime.Now
                };

                _db.RefundRequests.Add(refund);
                order.Status = "Đã hoàn tiền";
                await _db.SaveChangesAsync();

                return View("RefundSuccess", order);
            }
            else
            {
                // Phân tích lỗi từ PayOS
                string errorMsg = desc;
                
                // Việt hóa một số lỗi phổ biến
                if (code == "601")
                    errorMsg = "Lỗi xác thực: API Key của Kênh Chi không hợp lệ hoặc đã hết hạn.";
                else if (code == "602" || errorMsg.Contains("Account not found"))
                    errorMsg = "Số tài khoản không tồn tại hoặc không hợp lệ tại ngân hàng này.";
                else if (string.IsNullOrEmpty(errorMsg))
                    errorMsg = "Không thể thực hiện hoàn tiền tự động (Lỗi: " + code + ").";

                TempData["Error"] = errorMsg;
                return RedirectToAction("RefundRequest", new { orderId = orderId });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "REFUND EXCEPTION: {Msg}", ex.Message);
            TempData["Error"] = $"Lỗi hệ thống: {ex.Message}";
            return RedirectToAction("RefundRequest", new { orderId = orderId });
        }
    }

    private string CreateSignature(Dictionary<string, string> data, string checksumKey)
    {
        var sortedData = data.OrderBy(x => x.Key);
        // PayOS SDK mặc định dùng encodeURIComponent cho cả key và value
        var queryString = string.Join("&", sortedData.Select(x => 
            $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}"));
        
        _logger.LogInformation("SIGNATURE STRING (URL-ENCODED): {QueryString}", queryString);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(checksumKey));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(queryString));
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
    }

    private string GetBankBin(string bankName)
    {
        return bankName switch
        {
            "Vietcombank" => "970436",
            "VietinBank" => "970415",
            "MB Bank" => "970422",
            "BIDV" => "970418",
            "Techcombank" => "970407",
            "Agribank" => "970405",
            "TPBank" => "970423",
            "VPBank" => "970432",
            "ACB" => "970416",
            "Sacombank" => "970403",
            _ => "970436"
        };
    }

    private static int ResolveConfig(Dictionary<string, int> configs, string primaryKey, string fallbackKey)
    {
        if (configs.TryGetValue(primaryKey, out var val)) return val;
        if (configs.TryGetValue(fallbackKey, out var fallbackVal)) return fallbackVal;
        return 0;
    }
}
