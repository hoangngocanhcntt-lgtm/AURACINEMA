using AuraCinema.Domain.Entities;
using AuraCinema.Infrastructure.Data;
using AuraCinema.Web.Areas.Admin.ViewModels;
using AuraCinema.Domain.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AuraCinema.Web.Areas.Admin.Controllers;

[Authorize(Roles = "Admin")]
public class ShowtimesController : AdminBaseController
{
    private readonly AppDbContext _db;

    public ShowtimesController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var showtimes = await _db.Showtimes
            .Include(s => s.Movie)
            .Include(s => s.Room)
            .OrderByDescending(s => s.StartTime)
            .ToListAsync();
        return View(showtimes);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var vm = new ShowtimeFormViewModel { ShowtimeCode = CodeGenerator.GenerateShowtimeCode() };
        await PopulateLists(vm);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ShowtimeFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateLists(model);
            return View(model);
        }

        var showtime = new Showtime
        {
            ShowtimeCode = string.IsNullOrEmpty(model.ShowtimeCode) ? CodeGenerator.GenerateShowtimeCode() : model.ShowtimeCode,
            MovieID = model.MovieID,
            RoomID = model.RoomID,
            StartTime = model.StartTime,
            Status = model.Status
        };

        _db.Showtimes.Add(showtime);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Đã thêm suất chiếu mới thành công!";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var s = await _db.Showtimes.FindAsync(id);
        if (s == null) return NotFound();

        var vm = new ShowtimeFormViewModel
        {
            ShowtimeID = s.ShowtimeID,
            ShowtimeCode = s.ShowtimeCode,
            MovieID = s.MovieID,
            RoomID = s.RoomID,
            StartTime = s.StartTime,
            Status = s.Status
        };
        await PopulateLists(vm);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ShowtimeFormViewModel model)
    {
        if (id != model.ShowtimeID) return BadRequest();
        if (!ModelState.IsValid)
        {
            await PopulateLists(model);
            return View(model);
        }

        var s = await _db.Showtimes.FindAsync(id);
        if (s == null) return NotFound();

        s.MovieID = model.MovieID;
        s.RoomID = model.RoomID;
        s.StartTime = model.StartTime;
        s.Status = model.Status;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã cập nhật suất chiếu thành công!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var s = await _db.Showtimes.FindAsync(id);
        if (s == null) return NotFound();

        // Soft delete logic
        s.Status = "Da huy";
        await _db.SaveChangesAsync();

        TempData["Info"] = "Suất chiếu đã được chuyển sang trạng thái đã hủy.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateLists(ShowtimeFormViewModel vm)
    {
        vm.MovieList = await _db.Movies
            .Where(m => m.Status != "Ngung chieu")
            .Select(m => new SelectListItem { Value = m.MovieID.ToString(), Text = m.Title })
            .ToListAsync();

        vm.RoomList = await _db.Rooms
            .Where(r => r.Status == "Hoat dong")
            .Select(r => new SelectListItem { Value = r.RoomID.ToString(), Text = r.RoomName })
            .ToListAsync();
    }
}
