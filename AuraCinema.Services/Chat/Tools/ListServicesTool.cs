using System.Text.Json;
using AuraCinema.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuraCinema.Services.Chat.Tools;

public class ListServicesTool : IChatTool
{
    private readonly AppDbContext _db;
    private readonly ILogger<ListServicesTool> _logger;

    public ListServicesTool(AppDbContext db, ILogger<ListServicesTool> logger)
    {
        _db = db;
        _logger = logger;
    }

    public string Name => "list_services";

    public string Description => "Liệt kê dịch vụ bắp nước (F&B) đang phục vụ tại rạp.";

    public object Schema => new
    {
        type = "object",
        properties = new { }
    };

    public async Task<object> ExecuteAsync(JsonElement args, ChatToolContext ctx, CancellationToken ct)
    {
        try
        {
            var services = await _db.Services
                .Where(s => s.Status == "Hoat dong")
                .OrderBy(s => s.Price)
                .Select(s => new
                {
                    serviceId = s.ServiceID,
                    serviceName = s.ServiceName,
                    price = FormatVnd(s.Price),
                    image = s.Image
                })
                .ToListAsync(ct);

            if (services.Count == 0)
            {
                return new
                {
                    ok = true,
                    count = 0,
                    services = Array.Empty<object>(),
                    hint = "Hiện tại chưa có dịch vụ nào."
                };
            }

            return new
            {
                ok = true,
                count = services.Count,
                services = services
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy danh sách dịch vụ");
            return new { ok = false, error = "QUERY_ERROR", message = "Đã có lỗi xảy ra khi lấy danh sách dịch vụ." };
        }
    }

    private static string FormatVnd(int amount)
    {
        return $"{amount:N0}đ".Replace(",", ".");
    }
}
