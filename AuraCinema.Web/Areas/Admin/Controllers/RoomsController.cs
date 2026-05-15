using AuraCinema.Domain.Entities;
using AuraCinema.Infrastructure.Data;
using AuraCinema.Web.Areas.Admin.ViewModels;
using AuraCinema.Domain.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCinema.Web.Areas.Admin.Controllers;

[Authorize(Roles = "Admin")]
public class RoomsController : AdminBaseController
{
    private readonly AppDbContext _db;

    public RoomsController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var rooms = await _db.Rooms.OrderByDescending(r => r.RoomID).ToListAsync();
        return View(rooms);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new RoomFormViewModel { RoomCode = CodeGenerator.GenerateRoomCode() });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RoomFormViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var room = new Room
        {
            RoomCode = string.IsNullOrEmpty(model.RoomCode) ? CodeGenerator.GenerateRoomCode() : model.RoomCode,
            RoomName = model.RoomName,
            Capacity = model.Capacity,
            Status = model.Status
        };

        _db.Rooms.Add(room);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Đã thêm phòng chiếu mới thành công!";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var room = await _db.Rooms.FindAsync(id);
        if (room == null) return NotFound();

        var vm = new RoomFormViewModel
        {
            RoomID = room.RoomID,
            RoomCode = room.RoomCode,
            RoomName = room.RoomName,
            Capacity = room.Capacity,
            Status = room.Status
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, RoomFormViewModel model)
    {
        if (id != model.RoomID) return BadRequest();
        if (!ModelState.IsValid) return View(model);

        var room = await _db.Rooms.FindAsync(id);
        if (room == null) return NotFound();

        room.RoomName = model.RoomName;
        room.Capacity = model.Capacity;
        room.Status = model.Status;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã cập nhật thông tin phòng thành công!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var room = await _db.Rooms.FindAsync(id);
        if (room == null) return NotFound();

        // Soft delete logic
        room.Status = "Ngung hoat dong";
        await _db.SaveChangesAsync();

        TempData["Info"] = "Phòng chiếu đã được chuyển sang trạng thái ngừng hoạt động.";
        return RedirectToAction(nameof(Index));
    }
}
