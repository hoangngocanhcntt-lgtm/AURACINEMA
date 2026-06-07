using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using AuraCinema.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using AuraCinema.Domain.Entities;

var services = new ServiceCollection();
services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=AuraCinemaDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"));
var sp = services.BuildServiceProvider();
var db = sp.GetRequiredService<AppDbContext>();

var orders = db.Orders.Include(o => o.OrderSeats).OrderByDescending(o => o.OrderID).Take(5).ToList();
foreach (var o in orders) {
    Console.WriteLine($"Order {o.OrderID} | Status: {o.Status} | Time: {o.HoldExpiryTime} | Seats: {string.Join(", ", o.OrderSeats.Select(s => s.SeatID))}");
}
