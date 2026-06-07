using System.Text.Json;
using AuraCinema.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuraCinema.Services.Chat.Tools;

public class PriceInfoTool : IChatTool
{
    private readonly AppDbContext _db;
    private readonly ILogger<PriceInfoTool> _logger;

    public PriceInfoTool(AppDbContext db, ILogger<PriceInfoTool> logger)
    {
        _db = db;
        _logger = logger;
    }

    public string Name => "get_price_config";

    public string Description => "Lấy bảng giá vé hiện hành: giá gốc và các phụ thu (VIP, Couple, cuối tuần, suất tối). Trả về giá đã format sẵn dạng VND.";

    public object Schema => new
    {
        type = "object",
        properties = new { }
    };

    public async Task<object> ExecuteAsync(JsonElement args, ChatToolContext ctx, CancellationToken ct)
    {
        try
        {
            var configs = await _db.PriceConfigs
                .ToDictionaryAsync(c => c.ConfigCode.Trim(), c => c.SurchargeAmount, ct);

            // Hỗ trợ cả bộ ConfigCode cũ (BASE, SEAT_VIP...) lẫn bộ mới (BASE_PRICE, VIP_SURCHARGE...)
            int basePrice = Resolve(configs, "BASE_PRICE", "BASE");
            int vipSurcharge = Resolve(configs, "VIP_SURCHARGE", "SEAT_VIP");
            int coupleSurcharge = Resolve(configs, "COUPLE_SURCHARGE", "SEAT_COUPLE");
            int weekendSurcharge = Resolve(configs, "WEEKEND_SURCHARGE", "DAY_WEEKEND");
            int eveningSurcharge = Resolve(configs, "EVENING_SURCHARGE", "DAY_EVENING");

            return new
            {
                ok = true,
                basePrice = FormatVnd(basePrice),
                vipSurcharge = FormatVnd(vipSurcharge),
                coupleSurcharge = FormatVnd(coupleSurcharge),
                weekendSurcharge = FormatVnd(weekendSurcharge),
                eveningSurcharge = FormatVnd(eveningSurcharge),
                notes = new[]
                {
                    "Giá gốc áp cho ghế Thường, ngày thường, suất trước 18h.",
                    "Cuối tuần (T7, CN): cộng thêm phụ thu cuối tuần.",
                    "Suất chiếu từ 18h: cộng thêm phụ thu suất tối.",
                    "Ghế VIP và ghế Couple có phụ thu riêng cộng thêm vào giá gốc."
                },
                instruction = "QUAN TRỌNG: Trả lời khách CHÍNH XÁC các con số trên, KHÔNG được thay đổi hay làm tròn."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy thông tin bảng giá");
            return new { ok = false, error = "QUERY_ERROR", message = "Đã có lỗi xảy ra khi lấy bảng giá." };
        }
    }

    /// <summary>Tìm giá theo ConfigCode chính, nếu không có thì thử ConfigCode phụ (backwards compat).</summary>
    private static int Resolve(Dictionary<string, int> configs, string primaryKey, string fallbackKey)
    {
        if (configs.TryGetValue(primaryKey, out var val)) return val;
        if (configs.TryGetValue(fallbackKey, out var fallbackVal)) return fallbackVal;
        return 0;
    }

    private static string FormatVnd(int amount)
    {
        return $"{amount:N0}đ".Replace(",", ".");
    }
}
