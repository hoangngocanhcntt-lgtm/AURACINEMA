using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using AuraCinema.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using AuraCinema.Domain.Entities;

var services = new ServiceCollection();
services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=AuraCinemaDb;Trusted_Connection=True;MultipleActiveResultSets=true"));

var sp = services.BuildServiceProvider();
using var db = sp.GetRequiredService<AppDbContext>();

var configs = db.PriceConfigs.ToList();
foreach(var c in configs)
{
    if (c.ConfigCode == "BASE_PRICE") c.SurchargeAmount = 50000;
    if (c.ConfigCode == "VIP_SURCHARGE") c.SurchargeAmount = 15000;
    if (c.ConfigCode == "COUPLE_SURCHARGE") c.SurchargeAmount = 20000;
    if (c.ConfigCode == "WEEKEND_SURCHARGE") c.SurchargeAmount = 10000;
    if (c.ConfigCode == "EVENING_SURCHARGE") c.SurchargeAmount = 10000;
}
db.SaveChanges();
Console.WriteLine("Price configs updated.");
