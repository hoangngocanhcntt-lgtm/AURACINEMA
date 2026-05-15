using AuraCinema.Domain.Entities;
using AuraCinema.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCinema.Web.Areas.Admin.Controllers;

[Authorize(Roles = "Admin")]
public class SeatsController : AdminBaseController
{
    private readonly AppDbContext _db;

    public SeatsController(AppDbContext db)
    {
        _db = db;
    }

    // GET: Admin/Seats/Room/5
    public async Task<IActionResult> Room(int id)
    {
        var room = await _db.Rooms
            .Include(r => r.Seats.OrderBy(s => s.RowLabel).ThenBy(s => s.SeatNumber))
            .FirstOrDefaultAsync(r => r.RoomID == id);

        if (room == null) return NotFound();

        return View(room);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateType(int seatId, string type)
    {
        var seat = await _db.Seats.FindAsync(seatId);
        if (seat == null) return NotFound();

        seat.SeatType = type;
        await _db.SaveChangesAsync();

        return Ok();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BatchUpdate(int roomId, string rowLabel, string type)
    {
        var seats = await _db.Seats
            .Where(s => s.RoomID == roomId && s.RowLabel == rowLabel)
            .ToListAsync();

        foreach (var s in seats)
        {
            if (!string.IsNullOrEmpty(type)) s.SeatType = type;
        }

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Room), new { id = roomId });
    }
}
