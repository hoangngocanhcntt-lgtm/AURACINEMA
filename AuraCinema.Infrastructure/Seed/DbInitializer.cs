using AuraCinema.Domain.Entities;
using AuraCinema.Infrastructure.Data;
using AuraCinema.Domain.Helpers;
using Microsoft.EntityFrameworkCore;

namespace AuraCinema.Infrastructure.Seed;

public static class DbInitializer
{
    public static async Task SeedAsync(AppDbContext db)
    {
        await db.Database.MigrateAsync();

        // 1. Users
        if (!await db.Users.AnyAsync(u => u.Email == "admin@auracinema.vn"))
        {
            db.Users.Add(new User {
                UserCode = CodeGenerator.GenerateUserCode(),
                FullName = "Admin Aura",
                Email    = "admin@auracinema.vn",
                Password = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                Phone    = "0900000000",
                Role     = "Admin",
                Status   = "Hoat dong"
            });
        }

        if (!await db.Users.AnyAsync(u => u.Email == "staff@auracinema.vn"))
        {
            db.Users.Add(new User {
                UserCode = CodeGenerator.GenerateUserCode(),
                FullName = "Nhân viên rạp",
                Email    = "staff@auracinema.vn",
                Password = BCrypt.Net.BCrypt.HashPassword("Staff@123"),
                Phone    = "0911111111",
                Role     = "Staff",
                Status   = "Hoat dong"
            });
        }

        if (!await db.Users.AnyAsync(u => u.Email == "customer@gmail.com"))
        {
            db.Users.Add(new User {
                UserCode = CodeGenerator.GenerateUserCode(),
                FullName = "Khách hàng thân thiết",
                Email    = "customer@gmail.com",
                Password = BCrypt.Net.BCrypt.HashPassword("User@123"),
                Phone    = "0922222222",
                Role     = "Customer",
                Status   = "Hoat dong"
            });
        }
        await db.SaveChangesAsync();

        // Vá lỗi: Bổ sung UserCode cho các tài khoản cũ chưa có UserCode
        var usersWithoutCode = await db.Users.Where(u => string.IsNullOrEmpty(u.UserCode)).ToListAsync();
        if (usersWithoutCode.Any())
        {
            foreach (var user in usersWithoutCode)
            {
                user.UserCode = CodeGenerator.GenerateUserCode();
            }
            await db.SaveChangesAsync();
        }

        // 2. Rooms & Seats
        if (!await db.Rooms.AnyAsync())
        {
            var rows = new[] { "A","B","C","D","E","F","G" };
            for (int r = 1; r <= 3; r++)
            {
                var room = new Room { RoomCode = CodeGenerator.GenerateRoomCode(), RoomName = $"Phong {r}", Capacity = 50, Status = "Hoat dong" };
                db.Rooms.Add(room);
                await db.SaveChangesAsync();
                
                var seats = new List<Seat>();
                foreach (var row in rows)
                {
                    for (int n = 1; n <= 10; n++)
                    {
                        if (seats.Count >= 50) break;

                        string type = (row == "D" || row == "E" || row == "F") ? "VIP"
                                    : (row == "G") ? "Doi" : "Thuong";
                        
                        seats.Add(new Seat { 
                            SeatCode = $"{room.RoomCode}-{row}{n}",
                            RoomID = room.RoomID, 
                            RowLabel = row, 
                            SeatNumber = n, 
                            SeatType = type
                        });
                    }
                    if (seats.Count >= 50) break;
                }
                db.Seats.AddRange(seats);
            }
            await db.SaveChangesAsync();
        }

        // 3. PriceConfigs
        if (!await db.PriceConfigs.AnyAsync())
        {
            db.PriceConfigs.AddRange(
                new PriceConfig { ConfigType = "BASE_PRICE", ConfigCode = "BASE", ConfigName = "Giá vé cơ bản", SurchargeAmount = 70000 },
                new PriceConfig { ConfigType = "SEAT_SURCHARGE", ConfigCode = "SEAT_VIP", ConfigName = "Phụ thu ghế VIP", SurchargeAmount = 20000 },
                new PriceConfig { ConfigType = "SEAT_SURCHARGE", ConfigCode = "SEAT_COUPLE", ConfigName = "Phụ thu ghế Đôi", SurchargeAmount = 50000 },
                new PriceConfig { ConfigType = "DAY_SURCHARGE", ConfigCode = "DAY_WEEKEND", ConfigName = "Phụ thu cuối tuần", SurchargeAmount = 15000 },
                new PriceConfig { ConfigType = "DAY_SURCHARGE", ConfigCode = "DAY_HOLIDAY", ConfigName = "Phụ thu ngày lễ", SurchargeAmount = 30000 }
            );
            await db.SaveChangesAsync();
        }

        // 4. Movies
        if (!await db.Movies.AnyAsync())
        {
            db.Movies.AddRange(
                new Movie { MovieCode = CodeGenerator.GenerateMovieCode(), Title = "Inception", Genre = "Sci-Fi", Director = "Christopher Nolan", Actors = "Leonardo DiCaprio", Duration = 148, ReleaseDate = new DateOnly(2010,7,16), Poster = "https://m.media-amazon.com/images/M/MV5BMjAxMzY3NjcxNF5BMl5BanBnXkFtZTcwNTI5OTM0Mw@@._V1_FMjpg_UX1000_.jpg", Trailer = "https://www.youtube.com/watch?v=YoHD9XEInc0", Status = "Dang chieu" },
                new Movie { MovieCode = CodeGenerator.GenerateMovieCode(), Title = "Avengers: Endgame", Genre = "Action", Director = "Russo Brothers", Actors = "Robert Downey Jr.", Duration = 181, ReleaseDate = new DateOnly(2019,4,26), Poster = "https://m.media-amazon.com/images/M/MV5BMTc5MDE2ODcwNV5BMl5BanBnXkFtZTgwMzI2NzQ2NzM@._V1_.jpg", Trailer = "https://www.youtube.com/watch?v=TcMBFSGVi1c", Status = "Dang chieu" },
                new Movie { MovieCode = CodeGenerator.GenerateMovieCode(), Title = "The Dark Knight", Genre = "Action", Director = "Christopher Nolan", Actors = "Christian Bale", Duration = 152, ReleaseDate = new DateOnly(2008,7,18), Poster = "https://m.media-amazon.com/images/M/MV5BMTMxNTMwODM0NF5BMl5BanBnXkFtZTcwODAyMTk2Mw@@._V1_.jpg", Trailer = "https://www.youtube.com/watch?v=EXeTwQWrcwY", Status = "Dang chieu" },
                new Movie { MovieCode = CodeGenerator.GenerateMovieCode(), Title = "Parasite", Genre = "Thriller", Director = "Bong Joon-ho", Actors = "Song Kang-ho", Duration = 132, ReleaseDate = new DateOnly(2019,10,11), Poster = "https://m.media-amazon.com/images/M/MV5BYWZjMjk3ZTItODQ2ZC00NTY5LWE0ZDYtZTI3MjcwN2Q5NTVkXkEyXkFqcGdeQXVyODk4OTc3MTY@._V1_.jpg", Trailer = "https://www.youtube.com/watch?v=5xH0HfJHsaY", Status = "Sap chieu" },
                new Movie { MovieCode = CodeGenerator.GenerateMovieCode(), Title = "Oppenheimer", Genre = "Biography", Director = "Christopher Nolan", Actors = "Cillian Murphy", Duration = 180, ReleaseDate = new DateOnly(2023,7,21), Poster = "https://m.media-amazon.com/images/M/MV5BMDBmYTZjNjUtN2M1MS00MTQ2LTk2ODgtNjc2xw2086d9db._V1_FMjpg_UX1000_.jpg", Trailer = "https://www.youtube.com/watch?v=uYPbbksJxIg", Status = "Dang chieu" },
                new Movie { MovieCode = CodeGenerator.GenerateMovieCode(), Title = "Godzilla x Kong", Genre = "Sci-Fi", Director = "Adam Wingard", Actors = "Rebecca Hall", Duration = 115, ReleaseDate = new DateOnly(2024,3,29), Poster = "https://m.media-amazon.com/images/M/MV5BMjY5MjAyMDQtNmNlNC00OTFjLWExZGUtYmZlZTk4M2Y4MzYyXkEyXkFqcGdeQXVyMTUzMTg2ODkz._V1_FMjpg_UX1000_.jpg", Trailer = "https://www.youtube.com/watch?v=lV1OOlGwExM", Status = "Dang chieu" }
            );
            await db.SaveChangesAsync();
        }

        // 5. Services
        if (!await db.Services.AnyAsync())
        {
            db.Services.AddRange(
                new Service { ServiceCode = CodeGenerator.GenerateServiceCode(), ServiceName = "Bắp rang ngọt (S)", Price = 35000, Status = "Hoat dong", Image = "https://salt.tikicdn.com/cache/w1200/ts/product/55/e2/77/b0a454790176378e9b093f412f866465.jpg" },
                new Service { ServiceCode = CodeGenerator.GenerateServiceCode(), ServiceName = "Bắp rang ngọt (L)", Price = 55000, Status = "Hoat dong", Image = "https://salt.tikicdn.com/ts/product/d7/80/76/01e9d1877f8047970d47372d8a6797a2.jpg" },
                new Service { ServiceCode = CodeGenerator.GenerateServiceCode(), ServiceName = "Coca Cola (L)", Price = 25000, Status = "Hoat dong", Image = "https://cdn.tgdd.vn/Products/Images/2443/76467/bhx/nuoc-ngot-coca-cola-chai-1-5-lit-202308151528574100.jpg" },
                new Service { ServiceCode = CodeGenerator.GenerateServiceCode(), ServiceName = "Combo Đôi (2 Nước + 1 Bắp L)", Price = 99000, Status = "Hoat dong", Image = "https://iguov8nhvyobj.vcdn.cloud/media/catalog/product/cache/1/image/1800x/040ec09b1e35df139433887a97daa66f/m/y/my-combo_1.png" }
            );
            await db.SaveChangesAsync();
        }

        // 6. Promotions
        if (!await db.Promotions.AnyAsync())
        {
            db.Promotions.AddRange(
                new Promotion {
                    PromoCode = CodeGenerator.GeneratePromoCode(),
                    Title = "AURA10", DiscountValue = 10000,
                    MinAmount = 100000,
                    Condition = "Hóa đơn tối thiểu 100.000đ",
                    StartDate = DateTime.Now.AddDays(-30), EndDate = DateTime.Now.AddMonths(3),
                    Status = "Hoat dong"
                },
                new Promotion {
                    PromoCode = CodeGenerator.GeneratePromoCode(),
                    Title = "AURA20", DiscountValue = 20000,
                    MinAmount = 200000,
                    Condition = "Hóa đơn tối thiểu 200.000đ",
                    StartDate = DateTime.Now.AddDays(-10), EndDate = DateTime.Now.AddMonths(2),
                    Status = "Hoat dong"
                },
                new Promotion {
                    PromoCode = CodeGenerator.GeneratePromoCode(),
                    Title = "AURA50", DiscountValue = 50000,
                    MinAmount = 500000,
                    Condition = "Hóa đơn tối thiểu 500.000đ",
                    StartDate = DateTime.Now.AddDays(-5), EndDate = DateTime.Now.AddMonths(1),
                    Status = "Hoat dong"
                }
            );
            await db.SaveChangesAsync();
        }

        // 7. Showtimes (Seed for the next 7 days)
        if (!await db.Showtimes.AnyAsync())
        {
            var movies = await db.Movies.Where(m => m.Status == "Dang chieu").ToListAsync();
            var rooms = await db.Rooms.ToListAsync();
            var random = new Random();

            for (int i = 0; i < 7; i++) // 7 days
            {
                var date = DateTime.Today.AddDays(i);
                foreach (var room in rooms)
                {
                    // 3 showtimes per room per day
                    var times = new[] { 9, 14, 19 };
                    foreach (var hour in times)
                    {
                        var movie = movies[random.Next(movies.Count)];
                        db.Showtimes.Add(new Showtime {
                            MovieID = movie.MovieID,
                            RoomID = room.RoomID,
                            StartTime = date.AddHours(hour),
                            EndTime = date.AddHours(hour).AddMinutes(movie.Duration),
                            Status = "Sap chieu"
                        });
                    }
                }
            }
            await db.SaveChangesAsync();
        }

        // 8. Fake Orders for Dashboard visuals (Paid orders in the last 3 days)
        if (!await db.Orders.AnyAsync(o => o.Status == "Da thanh toan" || o.Status == "Đã thanh toán"))
        {
            var customer = await db.Users.FirstOrDefaultAsync(u => u.Role == "Customer");
            var showtimes = await db.Showtimes.Include(s => s.Room).Take(10).ToListAsync();
            var services = await db.Services.Take(2).ToListAsync();
            var random = new Random();

            foreach (var st in showtimes)
            {
                // Create 1-3 orders per showtime
                int numOrders = random.Next(1, 4);
                for (int j = 0; j < numOrders; j++)
                {
                    var seat = await db.Seats.Where(s => s.RoomID == st.RoomID).OrderBy(x => Guid.NewGuid()).FirstOrDefaultAsync();
                    if (seat == null) continue;

                    var order = new Order {
                        OrderCode = CodeGenerator.GenerateOrderCode(),
                        UserID = customer!.UserID,
                        ShowtimeID = st.ShowtimeID,
                        TotalAmount = 95000,
                        FinalAmount = 95000,
                        Status = "Đã thanh toán",
                        HoldExpiryTime = DateTime.Now.AddHours(1),
                        PayOSTransID = "FAKE-" + Guid.NewGuid().ToString().Substring(0, 8),
                        QrCode = Guid.NewGuid().ToString()
                    };
                    db.Orders.Add(order);
                    await db.SaveChangesAsync();

                    db.OrderSeats.Add(new OrderSeat {
                        OrderID = order.OrderID,
                        SeatID = seat.SeatID,
                        Price = 70000,
                        Status = "Da ban"
                    });

                    // Add one service
                    var svc = services[random.Next(services.Count)];
                    db.OrderServices.Add(new OrderService {
                        OrderID = order.OrderID,
                        ServiceID = svc.ServiceID,
                        Price = svc.Price,
                        Quantity = 1
                    });
                }
            }
            await db.SaveChangesAsync();
        }

        // 9. Seed June 2026 Showtimes (200 showtimes from 1/6 to 25/6)
        var startDate = new DateTime(2026, 6, 1);
        var endDate = new DateTime(2026, 6, 25);
        int juneShowtimeCount = await db.Showtimes.CountAsync(s => s.StartTime >= startDate && s.StartTime <= endDate.AddDays(1));
        if (juneShowtimeCount < 200)
        {
            // Clean up existing showtimes in this range first to avoid duplication/conflicts
            var existingJuneShowtimes = await db.Showtimes.Where(s => s.StartTime >= startDate && s.StartTime <= endDate.AddDays(1)).ToListAsync();
            db.Showtimes.RemoveRange(existingJuneShowtimes);
            await db.SaveChangesAsync();

            var movies = await db.Movies.Where(m => m.Status != "Ngung chieu").ToListAsync();
            var rooms = await db.Rooms.Where(r => r.Status == "Hoat dong").ToListAsync();
            if (movies.Any() && rooms.Count >= 3)
            {
                var random = new Random();
                var showtimesToAdd = new List<Showtime>();
                for (int day = 0; day < 25; day++)
                {
                    var currentDate = startDate.AddDays(day);
                    
                    // Room 1: 3 showtimes (9:00, 13:00, 17:00)
                    var room1 = rooms[0];
                    var times1 = new[] { 9, 13, 17 };
                    foreach (var hour in times1)
                    {
                        var movie = movies[random.Next(movies.Count)];
                        var start = currentDate.AddHours(hour);
                        var end = start.AddMinutes(movie.Duration);
                        showtimesToAdd.Add(new Showtime {
                            ShowtimeCode = CodeGenerator.GenerateShowtimeCode(),
                            MovieID = movie.MovieID,
                            RoomID = room1.RoomID,
                            StartTime = start,
                            EndTime = end,
                            Status = "Đã lên lịch"
                        });
                    }
                    
                    // Room 2: 3 showtimes (10:00, 14:00, 18:00)
                    var room2 = rooms[1];
                    var times2 = new[] { 10, 14, 18 };
                    foreach (var hour in times2)
                    {
                        var movie = movies[random.Next(movies.Count)];
                        var start = currentDate.AddHours(hour);
                        var end = start.AddMinutes(movie.Duration);
                        showtimesToAdd.Add(new Showtime {
                            ShowtimeCode = CodeGenerator.GenerateShowtimeCode(),
                            MovieID = movie.MovieID,
                            RoomID = room2.RoomID,
                            StartTime = start,
                            EndTime = end,
                            Status = "Đã lên lịch"
                        });
                    }
                    
                    // Room 3: 2 showtimes (11:00, 15:00)
                    var room3 = rooms[2];
                    var times3 = new[] { 11, 15 };
                    foreach (var hour in times3)
                    {
                        var movie = movies[random.Next(movies.Count)];
                        var start = currentDate.AddHours(hour);
                        var end = start.AddMinutes(movie.Duration);
                        showtimesToAdd.Add(new Showtime {
                            ShowtimeCode = CodeGenerator.GenerateShowtimeCode(),
                            MovieID = movie.MovieID,
                            RoomID = room3.RoomID,
                            StartTime = start,
                            EndTime = end,
                            Status = "Đã lên lịch"
                        });
                    }
                }
                db.Showtimes.AddRange(showtimesToAdd);
            }
        }

        await db.SaveChangesAsync();
    }
}
