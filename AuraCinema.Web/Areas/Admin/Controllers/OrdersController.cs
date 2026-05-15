using AuraCinema.Domain.Entities;
using AuraCinema.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCinema.Web.Areas.Admin.Controllers;

[Authorize(Roles = "Admin,Staff")]
public class OrdersController : AdminBaseController
{
    private readonly AppDbContext _db;

    public OrdersController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var orders = await _db.Orders
            .Include(o => o.User)
            .Include(o => o.Showtime)
                .ThenInclude(s => s.Movie)
            .OrderByDescending(o => o.OrderID)
            .ToListAsync();
        return View(orders);
    }

    public async Task<IActionResult> Details(int id)
    {
        var order = await _db.Orders
            .Include(o => o.User)
            .Include(o => o.Showtime)
                .ThenInclude(s => s.Movie)
            .Include(o => o.Showtime)
                .ThenInclude(s => s.Room)
            .Include(o => o.Promotion)
            .Include(o => o.OrderSeats)
                .ThenInclude(os => os.Seat)
            .Include(o => o.OrderServices)
                .ThenInclude(os => os.Service)
            .FirstOrDefaultAsync(o => o.OrderID == id);

        if (order == null) return NotFound();

        return View(order);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, string status)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order == null) return NotFound();

        order.Status = status;
        await _db.SaveChangesAsync();

        TempData["Success"] = "Đã cập nhật trạng thái đơn hàng!";
        return RedirectToAction(nameof(Details), new { id = id });
    }
}
