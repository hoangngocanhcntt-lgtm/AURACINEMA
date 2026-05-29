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

    public async Task<IActionResult> Index(DateTime? searchDate, string? searchStatus, int page = 1)
    {
        var query = _db.Orders
            .Include(o => o.User)
            .Include(o => o.Showtime)
                .ThenInclude(s => s.Movie)
            .AsQueryable();

        if (searchDate.HasValue)
        {
            var start = searchDate.Value.Date;
            var end = start.AddDays(1);
            query = query.Where(o => o.HoldExpiryTime >= start && o.HoldExpiryTime < end);
        }

        if (!string.IsNullOrEmpty(searchStatus))
        {
            query = query.Where(o => o.Status == searchStatus);
        }

        ViewBag.SearchDate = searchDate?.ToString("yyyy-MM-dd");
        ViewBag.SearchStatus = searchStatus;

        int pageSize = 6;
        int totalRecords = await query.CountAsync();
        ViewBag.TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
        ViewBag.CurrentPage = page;

        var orders = await query.OrderByDescending(o => o.OrderID)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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
