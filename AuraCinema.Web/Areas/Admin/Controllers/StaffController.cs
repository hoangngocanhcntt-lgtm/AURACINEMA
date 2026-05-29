using AuraCinema.Domain.Entities;
using AuraCinema.Domain.Helpers;
using AuraCinema.Infrastructure.Data;
using AuraCinema.Web.Areas.Admin.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCinema.Web.Areas.Admin.Controllers;

[Authorize(Roles = "Admin")]
public class StaffController : AdminBaseController
{
    private readonly AppDbContext _db;

    public StaffController(AppDbContext db)
    {
        _db = db;
    }

    // 1. Danh sách nhân viên
    public async Task<IActionResult> Index(int page = 1)
    {
        var query = _db.Users.Where(u => u.Role == "Staff");

        int pageSize = 6;
        int totalRecords = await query.CountAsync();
        ViewBag.TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
        ViewBag.CurrentPage = page;

        var staffList = await query.OrderByDescending(u => u.UserID)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return View(staffList);
    }

    // 2. Thêm nhân viên mới - GET
    public IActionResult Create()
    {
        return View(new StaffCreateViewModel());
    }

    // 2. Thêm nhân viên mới - POST
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StaffCreateViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        // Kiểm tra email trùng
        var emailExists = await _db.Users.AnyAsync(u => u.Email == model.Email.ToLower().Trim());
        if (emailExists)
        {
            ModelState.AddModelError("Email", "Email này đã được sử dụng.");
            return View(model);
        }

        var newStaff = new User
        {
            UserCode = CodeGenerator.GenerateUserCode(),
            FullName = model.FullName.Trim(),
            Email = model.Email.ToLower().Trim(),
            Password = BCrypt.Net.BCrypt.HashPassword(model.Password),
            Phone = model.Phone.Trim(),
            Role = "Staff",
            Status = "Hoat dong"
        };

        _db.Users.Add(newStaff);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Đã thêm nhân viên {newStaff.FullName} thành công!";
        return RedirectToAction(nameof(Index));
    }

    // 3. Khóa/Mở khóa tài khoản nhân viên
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var staff = await _db.Users.FindAsync(id);
        if (staff == null) return NotFound();

        // Chỉ cho phép thao tác trên tài khoản Nhân viên
        if (staff.Role != "Staff")
        {
            TempData["Error"] = "Không thể thao tác trên tài khoản này.";
            return RedirectToAction(nameof(Index));
        }

        if (staff.Status == "Hoat dong")
        {
            staff.Status = "Da khoa";
            TempData["Success"] = $"Đã khóa tài khoản nhân viên {staff.FullName}!";
        }
        else
        {
            staff.Status = "Hoat dong";
            TempData["Success"] = $"Đã mở khóa tài khoản nhân viên {staff.FullName}!";
        }

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // 4. Sửa mật khẩu nhân viên - GET
    public async Task<IActionResult> ChangePassword(int id)
    {
        var staff = await _db.Users.FindAsync(id);
        if (staff == null || staff.Role != "Staff") return NotFound();

        var model = new StaffChangePasswordViewModel
        {
            UserID = staff.UserID,
            FullName = staff.FullName
        };
        return View(model);
    }

    // 4. Sửa mật khẩu nhân viên - POST
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(StaffChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var staff = await _db.Users.FindAsync(model.UserID);
        if (staff == null || staff.Role != "Staff") return NotFound();

        staff.Password = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Đã đổi mật khẩu cho nhân viên {staff.FullName} thành công!";
        return RedirectToAction(nameof(Index));
    }
}
