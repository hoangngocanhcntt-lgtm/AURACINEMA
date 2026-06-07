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

    public async Task<IActionResult> Index(string? searchCode, DateTime? searchDate, int? roomId, int page = 1)
    {
        var query = _db.Showtimes
            .Include(s => s.Movie)
            .Include(s => s.Room)
            .Where(s => s.Status != "Đã hủy" && s.Status != "Đã kết thúc");

        if (!string.IsNullOrEmpty(searchCode))
            query = query.Where(s => s.ShowtimeCode.Contains(searchCode));

        if (searchDate.HasValue)
        {
            var start = searchDate.Value.Date;
            var end = start.AddDays(1);
            query = query.Where(s => s.StartTime >= start && s.StartTime < end);
        }
        else
        {
            query = query.Where(s => s.EndTime > DateTime.Now);
        }

        if (roomId.HasValue)
        {
            query = query.Where(s => s.RoomID == roomId.Value);
        }

        ViewBag.SearchCode = searchCode;
        ViewBag.SearchDate = searchDate?.ToString("yyyy-MM-dd");
        ViewBag.SelectedRoomId = roomId;
        ViewBag.RoomList = await _db.Rooms.Select(r => new SelectListItem { Value = r.RoomID.ToString(), Text = r.RoomName }).ToListAsync();

        int pageSize = 6;
        int totalRecords = await query.CountAsync();
        ViewBag.TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
        ViewBag.CurrentPage = page;

        var showtimes = await query.OrderByDescending(s => s.StartTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return View(showtimes);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var allowedDate = DateTime.Today.AddDays(5);
        var vm = new ShowtimeFormViewModel { 
            ShowtimeCode = CodeGenerator.GenerateShowtimeCode(),
            StartTime = allowedDate.AddHours(8) // Default 8 AM
        };
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

        var allowedDate = DateTime.Today.AddDays(5);
        if (model.StartTime.Date < allowedDate)
        {
            ModelState.AddModelError("StartTime", $"Chỉ được phép thêm suất chiếu từ ngày thứ 5 trở đi (từ {allowedDate:dd/MM/yyyy}).");
            await PopulateLists(model);
            return View(model);
        }

        var room = await _db.Rooms.FindAsync(model.RoomID);
        if (room == null || room.Status != "Hoat dong")
        {
            ModelState.AddModelError("RoomID", "Phòng chiếu không tồn tại hoặc đã ngừng hoạt động, không thể xếp lịch.");
            await PopulateLists(model);
            return View(model);
        }

        var movie = await _db.Movies.FindAsync(model.MovieID);
        if (movie == null)
        {
            await PopulateLists(model);
            return View(model);
        }
        var endTime = model.StartTime.AddMinutes(movie.Duration);
        if (await IsRoomConflict(model.RoomID, model.StartTime, endTime, null))
        {
            ModelState.AddModelError("StartTime", "Suất chiếu bị trùng lịch hoặc quá sát với suất chiếu khác trong cùng phòng (cần cách nhau ít nhất 10 phút).");
            await PopulateLists(model);
            return View(model);
        }

        var showtime = new Showtime
        {
            ShowtimeCode = string.IsNullOrEmpty(model.ShowtimeCode) ? CodeGenerator.GenerateShowtimeCode() : model.ShowtimeCode,
            MovieID = model.MovieID,
            RoomID = model.RoomID,
            StartTime = model.StartTime,
            EndTime = endTime, 
            Status = GetStatusByTime(model.StartTime, endTime)
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

        var allowedDate = DateTime.Today.AddDays(5);
        if (model.StartTime.Date < allowedDate)
        {
            ModelState.AddModelError("StartTime", $"Chỉ được phép sửa suất chiếu từ ngày thứ 5 trở đi (từ {allowedDate:dd/MM/yyyy}).");
            await PopulateLists(model);
            return View(model);
        }

        var s = await _db.Showtimes.FindAsync(id);
        if (s == null) return NotFound();

        var room = await _db.Rooms.FindAsync(model.RoomID);
        if (room == null || room.Status != "Hoat dong")
        {
            ModelState.AddModelError("RoomID", "Phòng chiếu không tồn tại hoặc đã ngừng hoạt động, không thể xếp lịch.");
            await PopulateLists(model);
            return View(model);
        }

        var movie = await _db.Movies.FindAsync(model.MovieID);
        if (movie == null)
        {
            await PopulateLists(model);
            return View(model);
        }
        var endTime = model.StartTime.AddMinutes(movie.Duration);
        if (await IsRoomConflict(model.RoomID, model.StartTime, endTime, id))
        {
            ModelState.AddModelError("StartTime", "Suất chiếu bị trùng lịch hoặc quá sát với suất chiếu khác trong cùng phòng (cần cách nhau ít nhất 10 phút).");
            await PopulateLists(model);
            return View(model);
        }

        s.MovieID = model.MovieID;
        s.RoomID = model.RoomID;
        s.StartTime = model.StartTime;
        s.EndTime = endTime;
        s.Status = GetStatusByTime(s.StartTime, s.EndTime);

        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã cập nhật suất chiếu thành công!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var s = await _db.Showtimes.Include(s => s.Orders).FirstOrDefaultAsync(s => s.ShowtimeID == id);
        if (s == null) return NotFound();

        var allowedDate = DateTime.Today.AddDays(5);
        if (s.StartTime.Date < allowedDate || s.Status != "Đã lên lịch")
        {
            TempData["Error"] = "Chỉ được phép xóa các suất chiếu trong tương lai (từ ngày thứ 5 trở đi) và đang ở trạng thái 'Đã lên lịch'.";
            return RedirectToAction(nameof(Index));
        }

        if (s.Orders != null && s.Orders.Any())
        {
            s.Status = "Đã hủy";
            await _db.SaveChangesAsync();
            TempData["Info"] = "Suất chiếu đã có đơn hàng nên chỉ được chuyển sang trạng thái 'Đã hủy'.";
        }
        else
        {
            _db.Showtimes.Remove(s);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Đã xóa suất chiếu thành công!";
        }

        return RedirectToAction(nameof(Index));
    }

    private string GetStatusByTime(DateTime start, DateTime end)
    {
        var now = DateTime.Now;
        var today = now.Date;

        if (now > end) return "Đã kết thúc";
        if (now >= start && now <= end) return "Đang chiếu";
        if (start.Date > today.AddDays(4)) return "Đã lên lịch";
        return "Đang mở bán";
    }

    private async Task<bool> IsRoomConflict(int roomId, DateTime start, DateTime end, int? currentShowtimeId)
    {
        // Buffer 10 phút
        var bufferStart = start.AddMinutes(-10);
        var bufferEnd = end.AddMinutes(10);

        return await _db.Showtimes
            .Where(s => s.RoomID == roomId && s.Status != "Đã hủy")
            .Where(s => !currentShowtimeId.HasValue || s.ShowtimeID != currentShowtimeId.Value)
            .AnyAsync(s => (start < s.EndTime.AddMinutes(10)) && (end > s.StartTime.AddMinutes(-10)));
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
