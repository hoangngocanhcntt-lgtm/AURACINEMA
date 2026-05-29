using AuraCinema.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCinema.Web.Areas.Admin.Controllers;

[Authorize(Roles = "Admin")]
public class CustomersController : AdminBaseController
{
    private readonly AppDbContext _db;

    public CustomersController(AppDbContext db)
    {
        _db = db;
    }

    // 1. Danh sách khách hàng & tìm kiếm theo userCode
    public async Task<IActionResult> Index(string? searchUserCode, int page = 1)
    {
        var query = _db.Users
            .Where(u => u.Role == "Customer" || u.Role == "Khach hang")
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchUserCode))
        {
            query = query.Where(u => u.UserCode.Contains(searchUserCode.Trim()));
        }

        ViewBag.SearchUserCode = searchUserCode;

        int pageSize = 6;
        int totalRecords = await query.CountAsync();
        ViewBag.TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
        ViewBag.CurrentPage = page;

        var customers = await query.OrderByDescending(u => u.UserID)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return View(customers);
    }

    // 2. Khóa/mở khóa tài khoản khách hàng
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id, string? searchUserCode)
    {
        var customer = await _db.Users.FindAsync(id);
        if (customer == null) return NotFound();

        // Chỉ cho phép khóa/mở khóa tài khoản Khách hàng
        if (customer.Role != "Customer" && customer.Role != "Khach hang")
        {
            TempData["Error"] = "Không thể thao tác trên tài khoản này.";
            return RedirectToAction(nameof(Index), new { searchUserCode });
        }

        if (customer.Status == "Hoat dong")
        {
            customer.Status = "Da khoa";
            TempData["Success"] = $"Đã khóa tài khoản khách hàng {customer.FullName}!";
        }
        else
        {
            customer.Status = "Hoat dong";
            TempData["Success"] = $"Đã mở khóa tài khoản khách hàng {customer.FullName}!";
        }

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { searchUserCode });
    }

    // 3. Xem lịch sử đơn hàng theo từng khách hàng
    public async Task<IActionResult> OrderHistory(int id)
    {
        var customer = await _db.Users.FirstOrDefaultAsync(u => u.UserID == id);
        if (customer == null) return NotFound();

        var orders = await _db.Orders
            .Where(o => o.UserID == id)
            .Include(o => o.Showtime)
                .ThenInclude(s => s.Movie)
            .Include(o => o.Showtime)
                .ThenInclude(s => s.Room)
            .Include(o => o.OrderSeats)
                .ThenInclude(os => os.Seat)
            .Include(o => o.OrderServices)
                .ThenInclude(os => os.Service)
            .OrderByDescending(o => o.OrderID)
            .ToListAsync();

        ViewBag.Customer = customer;
        return View(orders);
    }
}
