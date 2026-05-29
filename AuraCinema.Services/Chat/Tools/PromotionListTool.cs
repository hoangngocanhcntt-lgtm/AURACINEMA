using System.Text.Json;
using AuraCinema.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuraCinema.Services.Chat.Tools;

public class PromotionListTool : IChatTool
{
    private readonly AppDbContext _db;
    private readonly ILogger<PromotionListTool> _logger;

    public PromotionListTool(AppDbContext db, ILogger<PromotionListTool> logger)
    {
        _db = db;
        _logger = logger;
    }

    public string Name => "list_promotions";

    public string Description => "Liệt kê khuyến mãi đang hoạt động.";

    public object Schema => new
    {
        type = "object",
        properties = new
        {
            minOrderAmount = new { type = "integer", description = "Lọc các promo có MinAmount <= số này" }
        }
    };

    public async Task<object> ExecuteAsync(JsonElement args, ChatToolContext ctx, CancellationToken ct)
    {
        try
        {
            var now = DateTime.Now;
            var query = _db.Promotions
                .Where(p => p.Status == "Hoat dong" && p.StartDate <= now && p.EndDate >= now);

            if (args.TryGetProperty("minOrderAmount", out var minAmtProp) && minAmtProp.ValueKind == JsonValueKind.Number && minAmtProp.TryGetInt32(out var minOrderAmount))
            {
                query = query.Where(p => p.MinAmount <= minOrderAmount);
            }

            var list = await query
                .OrderBy(p => p.MinAmount)
                .Take(10)
                .Select(p => new
                {
                    promoCode = p.PromoCode,
                    title = p.Title,
                    discountValue = p.DiscountValue,
                    minAmount = p.MinAmount,
                    condition = p.Condition,
                    endDate = p.EndDate.ToString("dd/MM/yyyy")
                })
                .ToListAsync(ct);

            if (list.Count == 0)
            {
                return new
                {
                    ok = true,
                    count = 0,
                    promotions = Array.Empty<object>(),
                    hint = "Hiện chưa có khuyến mãi áp dụng cho mức tiền này nha bạn."
                };
            }

            return new
            {
                ok = true,
                count = list.Count,
                promotions = list
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi query danh sách khuyến mãi");
            return new { ok = false, error = "QUERY_ERROR", message = "Đã có lỗi xảy ra khi lấy danh sách khuyến mãi." };
        }
    }
}
