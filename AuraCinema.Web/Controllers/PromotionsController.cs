using AuraCinema.Infrastructure.Data;
using AuraCinema.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCinema.Web.Controllers;

public class PromotionsController : Controller
{
    private readonly AppDbContext _db;
    public PromotionsController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var promos = await _db.Promotions
            .Where(p => p.Status == "Hoat dong" && p.EndDate > DateTime.Now)
            .OrderBy(p => p.EndDate)
            .Select(p => new PromotionViewModel
            {
                PromoID       = p.PromoID,
                Title         = p.Title,
                DiscountValue = p.DiscountValue,
                Condition     = p.Condition,
                StartDate     = p.StartDate,
                EndDate       = p.EndDate,
                Status        = p.Status
            })
            .ToListAsync();

        return View(promos);
    }
}
