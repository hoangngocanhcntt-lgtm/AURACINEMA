using AuraCinema.Domain.Entities;
using AuraCinema.Infrastructure.Data;
using AuraCinema.Web.Areas.Admin.ViewModels;
using AuraCinema.Domain.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCinema.Web.Areas.Admin.Controllers;

[Authorize(Roles = "Admin")]
public class MoviesController : AdminBaseController
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public MoviesController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? searchCode, string? searchTitle)
    {
        var query = _db.Movies.AsQueryable();

        if (!string.IsNullOrEmpty(searchCode))
            query = query.Where(m => m.MovieCode.Contains(searchCode));

        if (!string.IsNullOrEmpty(searchTitle))
            query = query.Where(m => m.Title.Contains(searchTitle));

        ViewBag.SearchCode = searchCode;
        ViewBag.SearchTitle = searchTitle;

        var movies = await query.OrderByDescending(m => m.MovieID).ToListAsync();
        return View(movies);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var movie = await _db.Movies.FirstOrDefaultAsync(m => m.MovieID == id);
        if (movie == null) return NotFound();
        return View(movie);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new MovieFormViewModel { 
            MovieCode = CodeGenerator.GenerateMovieCode(),
            ReleaseDate = DateOnly.FromDateTime(DateTime.Today) 
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(MovieFormViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        if (string.IsNullOrEmpty(model.MovieCode))
        {
            model.MovieCode = CodeGenerator.GenerateMovieCode();
        }

        if (await _db.Movies.AnyAsync(m => m.MovieCode == model.MovieCode))
        {
            // If the auto-generated code somehow exists (rare), regenerate
            model.MovieCode = CodeGenerator.GenerateMovieCode(); 
        }

        string posterPath = "/images/placeholder-movie.jpg";

        // Handle File Upload
        if (model.PosterFile != null && model.PosterFile.Length > 0)
        {
            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "posters");
            Directory.CreateDirectory(uploadsFolder);
            
            var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(model.PosterFile.FileName);
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);
            
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await model.PosterFile.CopyToAsync(stream);
            }
            posterPath = "/uploads/posters/" + uniqueFileName;
        }

        var movie = new Movie
        {
            MovieCode = model.MovieCode,
            Title = model.Title,
            Genre = model.Genre,
            Director = model.Director,
            Actors = model.Actors,
            Duration = model.Duration,
            ReleaseDate = model.ReleaseDate,
            Trailer = model.Trailer,
            Status = model.Status,
            Poster = posterPath
        };

        _db.Movies.Add(movie);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Đã thêm bộ phim mới thành công!";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var movie = await _db.Movies.FindAsync(id);
        if (movie == null) return NotFound();

        var vm = new MovieFormViewModel
        {
            MovieID = movie.MovieID,
            MovieCode = movie.MovieCode,
            Title = movie.Title,
            Genre = movie.Genre,
            Director = movie.Director,
            Actors = movie.Actors,
            Duration = movie.Duration,
            ReleaseDate = movie.ReleaseDate,
            Trailer = movie.Trailer,
            Status = movie.Status,
            CurrentPoster = movie.Poster
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, MovieFormViewModel model)
    {
        if (id != model.MovieID) return BadRequest();
        if (!ModelState.IsValid) return View(model);

        if (await _db.Movies.AnyAsync(m => m.MovieCode == model.MovieCode && m.MovieID != id))
        {
            ModelState.AddModelError("MovieCode", "Mã phim đã tồn tại ở một phim khác.");
            return View(model);
        }

        var movie = await _db.Movies.FindAsync(id);
        if (movie == null) return NotFound();

        movie.MovieCode = model.MovieCode;
        movie.Title = model.Title;
        movie.Genre = model.Genre;
        movie.Director = model.Director;
        movie.Actors = model.Actors;
        movie.Duration = model.Duration;
        movie.ReleaseDate = model.ReleaseDate;
        movie.Trailer = model.Trailer;
        movie.Status = model.Status;

        // Handle File Upload replacing old poster
        if (model.PosterFile != null && model.PosterFile.Length > 0)
        {
            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "posters");
            Directory.CreateDirectory(uploadsFolder);
            
            var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(model.PosterFile.FileName);
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);
            
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await model.PosterFile.CopyToAsync(stream);
            }
            movie.Poster = "/uploads/posters/" + uniqueFileName;
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã cập nhật thông tin phim thành công!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var movie = await _db.Movies.FindAsync(id);
        if (movie == null) return NotFound();

        // Soft delete
        movie.Status = "Ngung chieu";
        await _db.SaveChangesAsync();

        TempData["Info"] = "Đã chuyển trạng thái phim thành 'Ngừng chiếu'!";
        return RedirectToAction(nameof(Index));
    }
}
