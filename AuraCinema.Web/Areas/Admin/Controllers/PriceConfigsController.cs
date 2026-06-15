using AuraCinema.Infrastructure.Data;
using AuraCinema.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCinema.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class PriceConfigsController : AdminBaseController
{
    private readonly AppDbContext _db;

    public PriceConfigsController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var requiredCodes = new[] { "BASE_PRICE", "WEEKEND_SURCHARGE", "EVENING_SURCHARGE", "VIP_SURCHARGE", "COUPLE_SURCHARGE", "EARLY_SURCHARGE" };
        var configs = await _db.PriceConfigs.Where(c => requiredCodes.Contains(c.ConfigCode)).ToListAsync();

        bool hasChanges = false;
        foreach (var code in requiredCodes)
        {
            var config = configs.FirstOrDefault(c => c.ConfigCode == code);
            if (config == null)
            {
                var newConfig = new PriceConfig
                {
                    ConfigCode = code,
                    ConfigType = code.Contains("PRICE") ? "Base" : "Surcharge",
                    ConfigName = GetDefaultName(code),
                    SurchargeAmount = 0,
                    EffectiveDate = DateTime.Now
                };
                _db.PriceConfigs.Add(newConfig);
                configs.Add(newConfig);
                hasChanges = true;
            }
            else if (config.EffectiveDate == null)
            {
                config.EffectiveDate = DateTime.Now;
                hasChanges = true;
            }
        }
        
        if (hasChanges)
        {
            await _db.SaveChangesAsync();
        }

        return View(configs.OrderBy(c => Array.IndexOf(requiredCodes, c.ConfigCode)).ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePrices(List<PriceUpdateViewModel> items, DateTime? globalEffectiveDate)
    {
        Console.WriteLine($"UpdatePrices called with {items?.Count ?? 0} items, globalEffectiveDate={globalEffectiveDate}");
        if (items != null && items.Any())
        {
            var today = DateTime.Today;
            var minValidDate = today.AddDays(5);
            bool hasError = false;

            foreach (var item in items)
            {
                var config = await _db.PriceConfigs.FirstOrDefaultAsync(c => c.ConfigCode.Trim() == item.ConfigCode.Trim());
                if (config != null)
                {
                    // Nếu giá trị thay đổi so với giá đang hiển thị trên form
                    int currentValue = config.NewSurchargeAmount ?? config.SurchargeAmount;
                    if (currentValue != item.Amount)
                    {
                        if (!globalEffectiveDate.HasValue || globalEffectiveDate.Value.Date < minValidDate)
                        {
                            hasError = true;
                        }
                        else 
                        {
                            config.NewSurchargeAmount = Math.Max(0, item.Amount);
                            config.EffectiveDate = globalEffectiveDate.Value.Date;
                        }
                    }
                }
            }

            if (hasError)
            {
                TempData["DateError"] = "Vui lòng chọn ngày áp dụng chung hợp lệ (cách hiện tại ít nhất 5 ngày) vì bạn có thay đổi giá trị.";
                TempData["Error"] = "Cập nhật thất bại. Chưa chọn ngày áp dụng hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            if (_db.ChangeTracker.HasChanges())
            {
                await _db.SaveChangesAsync();
                TempData["Success"] = "Cập nhật cấu hình giá vé thành công!";
            }
            else
            {
                TempData["Success"] = "Không có thay đổi nào cần lưu.";
            }
        }
        else
        {
            TempData["Error"] = "Không nhận được dữ liệu cập nhật.";
        }
        
        return RedirectToAction(nameof(Index));
    }

    private string GetDefaultName(string code)
    {
        return code switch
        {
            "BASE_PRICE" => "Giá vé thường",
            "WEEKEND_SURCHARGE" => "Phụ thu cuối tuần",
            "EVENING_SURCHARGE" => "Phụ thu tối",
            "VIP_SURCHARGE" => "Phụ thu ghế VIP",
            "COUPLE_SURCHARGE" => "Phụ thu ghế couple",
            "EARLY_SURCHARGE" => "Phụ thu suất chiếu sớm",
            _ => code
        };
    }
}

public class PriceUpdateViewModel
{
    public string ConfigCode { get; set; } = string.Empty;
    public int Amount { get; set; }
    public DateTime? EffectiveDate { get; set; }
}
