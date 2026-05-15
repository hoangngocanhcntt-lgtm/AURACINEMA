using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AuraCinemaWeb.Data;
using AuraCinemaWeb.Models;

namespace AuraCinemaWeb.Controllers
{
    public class MoviesController : Controller
    {
        private readonly AuraCinemaDbContext _context;

        public MoviesController(AuraCinemaDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var movies = await _context.Movies.ToListAsync();
            return View(movies);
        }
    }
}
