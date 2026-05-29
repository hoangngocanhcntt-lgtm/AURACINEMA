using AuraCinema.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json")
    .Build();

var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
optionsBuilder.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));

using (var db = new AppDbContext(optionsBuilder.Options))
{
    var prices = await db.PriceConfigs.ToListAsync();
    Console.WriteLine("--- CURRENT PRICE CONFIGURATIONS ---");
    foreach (var p in prices)
    {
        Console.WriteLine($"{p.ConfigName} ({p.ConfigCode}): {p.SurchargeAmount:N0} VNĐ");
    }
    Console.WriteLine("-------------------------------------");
}
