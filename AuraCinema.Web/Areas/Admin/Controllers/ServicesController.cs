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

    public ServicesController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var services = await _db.Services.OrderByDescending(s => s.ServiceID).ToListAsync();
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

        var service = new Service
        {
            ServiceCode = string.IsNullOrEmpty(model.ServiceCode) ? CodeGenerator.GenerateServiceCode() : model.ServiceCode,
            ServiceName = model.ServiceName,
            Price = model.Price,
            Image = model.Image ?? "/images/popcorn-default.jpg",
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
        s.Image = model.Image ?? s.Image;
        s.Status = model.Status;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã cập nhật dịch vụ!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var s = await _db.Services.FindAsync(id);
        if (s == null) return NotFound();

        s.Status = "Ngung kinh doanh";
        await _db.SaveChangesAsync();

        TempData["Info"] = "Dịch vụ đã được ngừng kinh doanh.";
        return RedirectToAction(nameof(Index));
    }
}
