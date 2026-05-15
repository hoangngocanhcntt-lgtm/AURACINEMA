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

    public async Task<IActionResult> Index()
    {
        var promos = await _db.Promotions.OrderByDescending(p => p.PromoID).ToListAsync();
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
        var p = await _db.Promotions.FindAsync(id);
        if (p == null) return NotFound();

        p.Status = "Het han";
        await _db.SaveChangesAsync();

        TempData["Info"] = "Chương trình khuyến mãi đã được đóng.";
        return RedirectToAction(nameof(Index));
    }
}
