using AuraCinema.Domain.Entities;
using AuraCinema.Infrastructure.Data;
using AuraCinema.Web.Areas.Admin.ViewModels;
using AuraCinema.Domain.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCinema.Web.Areas.Admin.Controllers;

[Authorize(Roles = "Admin")]
public class ServicesController : AdminBaseController
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public ServicesController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public async Task<IActionResult> Index(string? searchCode, int page = 1)
    {
        var query = _db.Services.Where(s => s.Status != "Ngung kinh doanh");

        if (!string.IsNullOrEmpty(searchCode))
            query = query.Where(s => s.ServiceCode.Contains(searchCode));

        ViewBag.SearchCode = searchCode;

        int pageSize = 6;
        int totalRecords = await query.CountAsync();
        ViewBag.TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
        ViewBag.CurrentPage = page;

        var services = await query.OrderByDescending(s => s.ServiceID)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return View(services);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new ServiceFormViewModel { ServiceCode = CodeGenerator.GenerateServiceCode() });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ServiceFormViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        string imagePath = "/images/popcorn-default.jpg";

        // Handle File Upload
        if (model.ImageFile != null && model.ImageFile.Length > 0)
        {
            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "services");
            Directory.CreateDirectory(uploadsFolder);
            
            var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(model.ImageFile.FileName);
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);
            
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await model.ImageFile.CopyToAsync(stream);
            }
            imagePath = "/uploads/services/" + uniqueFileName;
        }
        else if (!string.IsNullOrEmpty(model.Image))
        {
            imagePath = model.Image;
        }

        var service = new Service
        {
            ServiceCode = string.IsNullOrEmpty(model.ServiceCode) ? CodeGenerator.GenerateServiceCode() : model.ServiceCode,
            ServiceName = model.ServiceName,
            Price = model.Price,
            Image = imagePath,
            Status = model.Status
        };

        _db.Services.Add(service);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Đã thêm dịch vụ/đồ ăn mới!";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var s = await _db.Services.FindAsync(id);
        if (s == null) return NotFound();

        var vm = new ServiceFormViewModel
        {
            ServiceID = s.ServiceID,
            ServiceCode = s.ServiceCode,
            ServiceName = s.ServiceName,
            Price = s.Price,
            Image = s.Image,
            Status = s.Status
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ServiceFormViewModel model)
    {
        if (id != model.ServiceID) return BadRequest();
        if (!ModelState.IsValid) return View(model);

        var s = await _db.Services.FindAsync(id);
        if (s == null) return NotFound();

        s.ServiceName = model.ServiceName;
        s.Price = model.Price;
        s.Status = model.Status;

        // Handle File Upload
        if (model.ImageFile != null && model.ImageFile.Length > 0)
        {
            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "services");
            Directory.CreateDirectory(uploadsFolder);
            
            var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(model.ImageFile.FileName);
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);
            
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await model.ImageFile.CopyToAsync(stream);
            }
            s.Image = "/uploads/services/" + uniqueFileName;
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã cập nhật dịch vụ!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var s = await _db.Services.Include(s => s.OrderServices).FirstOrDefaultAsync(s => s.ServiceID == id);
        if (s == null) return NotFound();

        if (s.OrderServices != null && s.OrderServices.Any())
        {
            s.Status = "Ngung kinh doanh";
            await _db.SaveChangesAsync();
            TempData["Info"] = "Dịch vụ đã có đơn hàng nên chỉ được chuyển sang 'Ngừng kinh doanh'.";
        }
        else
        {
            _db.Services.Remove(s);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Đã xóa dịch vụ thành công!";
        }

        return RedirectToAction(nameof(Index));
    }
}
