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

    public string Description => "Lấy bảng giá vé hiện hành: giá gốc và các phụ thu.";

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

            return new
            {
                ok = true,
                basePrice = configs.GetValueOrDefault("BASE_PRICE", 70000),
                vipSurcharge = configs.GetValueOrDefault("VIP_SURCHARGE", 20000),
                coupleSurcharge = configs.GetValueOrDefault("COUPLE_SURCHARGE", 50000),
                weekendSurcharge = configs.GetValueOrDefault("WEEKEND_SURCHARGE", 15000),
                eveningSurcharge = configs.GetValueOrDefault("EVENING_SURCHARGE", 10000),
                notes = new[]
                {
                    "Giá gốc áp cho ghế Thường, ngày thường, suất trước 18h.",
                    "Cuối tuần (T7, CN): cộng thêm phụ thu cuối tuần.",
                    "Suất chiếu từ 18h: cộng thêm phụ thu suất tối.",
                    "Ghế VIP và ghế Couple có phụ thu riêng."
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy thông tin bảng giá");
            return new { ok = false, error = "QUERY_ERROR", message = "Đã có lỗi xảy ra khi lấy bảng giá." };
        }
    }
}
