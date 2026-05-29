using AuraCinema.Infrastructure.Data;
using AuraCinema.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((hostingContext, config) =>
    {
        config.AddJsonFile("d:/AURACINEMA/appsettings.json", optional: false, reloadOnChange: true);
    })
    .ConfigureServices((hostContext, services) =>
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(hostContext.Configuration.GetConnectionString("DefaultConnection")));
    })
    .Build();

using var scope = host.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

var room4 = await db.Rooms.Include(r => r.Seats).FirstOrDefaultAsync(r => r.RoomName.Contains("4"));

if (room4 != null)
{
    Console.WriteLine($"Found Room: {room4.RoomName} (ID: {room4.RoomID}) with {room4.Seats.Count} seats.");
    if (room4.Seats.Count == 0)
    {
        var rows = new[] { "A", "B", "C", "D", "E" };
        var seats = new List<Seat>();
        foreach (var row in rows)
        {
            for (int n = 1; n <= 10; n++)
            {
                string type = (row == "D" || row == "E") ? "VIP" : "Thuong";
                seats.Add(new Seat
                {
                    SeatCode = $"{room4.RoomCode}-{row}{n}",
                    RoomID = room4.RoomID,
                    RowLabel = row,
                    SeatNumber = n,
                    SeatType = type
                });
            }
        }
        db.Seats.AddRange(seats);
        await db.SaveChangesAsync();
        Console.WriteLine($"Generated 50 seats for Room {room4.RoomName}.");
    }
}
else
{
    Console.WriteLine("Room 4 not found.");
}
