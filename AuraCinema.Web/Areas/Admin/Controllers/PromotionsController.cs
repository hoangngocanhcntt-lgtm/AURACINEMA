using AuraCinema.Domain.Entities;
using AuraCinema.Infrastructure.Data;
using AuraCinema.Web.Areas.Admin.ViewModels;
using AuraCinema.Domain.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCinema.Web.Areas.Admin.Controllers;

[Authorize(Roles = "Admin")]
public class PromotionsController : AdminBaseController
{
    private readonly AppDbContext _db;

    public PromotionsController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? searchCode, int page = 1)
    {
        var query = _db.Promotions.AsQueryable();

        if (!string.IsNullOrEmpty(searchCode))
            query = query.Where(p => p.PromoCode.Contains(searchCode));

        ViewBag.SearchCode = searchCode;

        int pageSize = 6;
        int totalRecords = await query.CountAsync();
        ViewBag.TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
        ViewBag.CurrentPage = page;

        var promos = await query.OrderByDescending(p => p.PromoID)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return View(promos);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new PromotionFormViewModel { PromoCode = CodeGenerator.GeneratePromoCode() });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PromotionFormViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var promo = new Promotion
        {
            PromoCode = string.IsNullOrEmpty(model.PromoCode) ? CodeGenerator.GeneratePromoCode() : model.PromoCode,
            Title = model.Title,
            DiscountValue = model.DiscountValue,
            MinAmount = model.MinAmount,
            Condition = model.Condition,
            StartDate = model.StartDate,
            EndDate = model.EndDate,
            Status = model.Status
        };

        _db.Promotions.Add(promo);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Đã thêm chương trình khuyến mãi mới!";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var p = await _db.Promotions.FindAsync(id);
        if (p == null) return NotFound();

        var vm = new PromotionFormViewModel
        {
            PromoID = p.PromoID,
            PromoCode = p.PromoCode,
            Title = p.Title,
            DiscountValue = p.DiscountValue,
            MinAmount = p.MinAmount,
            Condition = p.Condition,
            StartDate = p.StartDate,
            EndDate = p.EndDate,
            Status = p.Status
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PromotionFormViewModel model)
    {
        if (id != model.PromoID) return BadRequest();
        if (!ModelState.IsValid) return View(model);

        var p = await _db.Promotions.FindAsync(id);
        if (p == null) return NotFound();

        p.Title = model.Title;
        p.DiscountValue = model.DiscountValue;
        p.MinAmount = model.MinAmount;
        p.Condition = model.Condition;
        p.StartDate = model.StartDate;
        p.EndDate = model.EndDate;
        p.Status = model.Status;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã cập nhật chương trình khuyến mãi!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var p = await _db.Promotions.Include(p => p.Orders).FirstOrDefaultAsync(p => p.PromoID == id);
        if (p == null) return NotFound();

        if (p.Orders != null && p.Orders.Any())
        {
            p.Status = "Het han";
            await _db.SaveChangesAsync();
            TempData["Info"] = "Khuyến mãi đã được sử dụng trong đơn hàng nên chỉ được đóng lại.";
        }
        else
        {
            _db.Promotions.Remove(p);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Đã xóa chương trình khuyến mãi thành công!";
        }

        return RedirectToAction(nameof(Index));
    }
}
