using Microsoft.EntityFrameworkCore;
using AuraCinemaWeb.Models;

namespace AuraCinemaWeb.Data
{
    public class AuraCinemaDbContext : DbContext
    {
        public AuraCinemaDbContext(DbContextOptions<AuraCinemaDbContext> options)
            : base(options) { }

        // ── DbSets ──────────────────────────────────────────────────────────
        public DbSet<User>        Users        { get; set; }
        public DbSet<Movie>       Movies       { get; set; }
        public DbSet<Room>        Rooms        { get; set; }
        public DbSet<Seat>        Seats        { get; set; }
        public DbSet<Service>     Services     { get; set; }
        public DbSet<Promotion>   Promotions   { get; set; }
        public DbSet<Showtime>    Showtimes    { get; set; }
        public DbSet<Order>       Orders       { get; set; }
        public DbSet<OrderSeat>   OrderSeats   { get; set; }
        public DbSet<OrderService> OrderServices { get; set; }
        public DbSet<PriceConfig> PriceConfigs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ── Composite PKs ────────────────────────────────────────────────
            modelBuilder.Entity<OrderSeat>()
                .HasKey(os => new { os.OrderID, os.SeatID });

            modelBuilder.Entity<OrderService>()
                .HasKey(os => new { os.OrderID, os.ServiceID });

            // ── Unique indexes ───────────────────────────────────────────────
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email).IsUnique();

            modelBuilder.Entity<Room>()
                .HasIndex(r => r.RoomName).IsUnique();

            modelBuilder.Entity<Seat>()
                .HasIndex(s => new { s.RoomID, s.RowLabel, s.SeatNumber }).IsUnique();

            // ── Relationships ────────────────────────────────────────────────

            // Seat → Room
            modelBuilder.Entity<Seat>()
                .HasOne(s => s.Room)
                .WithMany(r => r.Seats)
                .HasForeignKey(s => s.RoomID)
                .OnDelete(DeleteBehavior.Restrict);

            // Showtime → Movie
            modelBuilder.Entity<Showtime>()
                .HasOne(st => st.Movie)
                .WithMany(m => m.Showtimes)
                .HasForeignKey(st => st.MovieID)
                .OnDelete(DeleteBehavior.Restrict);

            // Showtime → Room
            modelBuilder.Entity<Showtime>()
                .HasOne(st => st.Room)
                .WithMany(r => r.Showtimes)
                .HasForeignKey(st => st.RoomID)
                .OnDelete(DeleteBehavior.Restrict);

            // Order → User
            modelBuilder.Entity<Order>()
                .HasOne(o => o.User)
                .WithMany(u => u.Orders)
                .HasForeignKey(o => o.UserID)
                .OnDelete(DeleteBehavior.Restrict);

            // Order → Showtime
            modelBuilder.Entity<Order>()
                .HasOne(o => o.Showtime)
                .WithMany(st => st.Orders)
                .HasForeignKey(o => o.ShowtimeID)
                .OnDelete(DeleteBehavior.Restrict);

            // Order → Promotion (nullable)
            modelBuilder.Entity<Order>()
                .HasOne(o => o.Promotion)
                .WithMany(p => p.Orders)
                .HasForeignKey(o => o.PromoID)
                .OnDelete(DeleteBehavior.SetNull);

            // OrderSeat → Order
            modelBuilder.Entity<OrderSeat>()
                .HasOne(os => os.Order)
                .WithMany(o => o.OrderSeats)
                .HasForeignKey(os => os.OrderID)
                .OnDelete(DeleteBehavior.Cascade);

            // OrderSeat → Seat
            modelBuilder.Entity<OrderSeat>()
                .HasOne(os => os.Seat)
                .WithMany(s => s.OrderSeats)
                .HasForeignKey(os => os.SeatID)
                .OnDelete(DeleteBehavior.Restrict);

            // OrderService → Order
            modelBuilder.Entity<OrderService>()
                .HasOne(os => os.Order)
                .WithMany(o => o.OrderServices)
                .HasForeignKey(os => os.OrderID)
                .OnDelete(DeleteBehavior.Cascade);

            // OrderService → Service
            modelBuilder.Entity<OrderService>()
                .HasOne(os => os.Service)
                .WithMany(s => s.OrderServices)
                .HasForeignKey(os => os.ServiceID)
                .OnDelete(DeleteBehavior.Restrict);

            // ── Data Seeding ─────────────────────────────────────────────────
            SeedData(modelBuilder);
        }

        private static void SeedData(ModelBuilder modelBuilder)
        {
            // ── Users ────────────────────────────────────────────────────────
            // Lưu ý: Password đã được hash bằng BCrypt (thay bằng hash thực tế khi deploy)
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    UserID   = "1",
                    FullName = "Administrator",
                    Email    = "admin@auracinema.vn",
                    Password = "$2a$12$adminHashedPasswordPlaceholder",
                    Phone    = "0901000001",
                    Role     = "Admin",
                    Status   = "Active"
                },
                new User
                {
                    UserID   = "2",
                    FullName = "Nguyễn Văn Nhân Viên",
                    Email    = "staff@auracinema.vn",
                    Password = "$2a$12$staffHashedPasswordPlaceholder",
                    Phone    = "0901000002",
                    Role     = "Staff",
                    Status   = "Active"
                },
                new User
                {
                    UserID   = "3",
                    FullName = "Trần Thị Khách Hàng",
                    Email    = "customer@gmail.com",
                    Password = "$2a$12$customerHashedPasswordPlaceholder",
                    Phone    = "0912345678",
                    Role     = "Customer",
                    Status   = "Active"
                }
            );

            // ── Movies ───────────────────────────────────────────────────────
            modelBuilder.Entity<Movie>().HasData(
                new Movie
                {
                    MovieID     = "1",
                    Title       = "Avengers: Endgame",
                    Genre       = "Hành động, Khoa học viễn tưởng",
                    Director    = "Anthony Russo, Joe Russo",
                    Actors      = "Robert Downey Jr., Chris Evans, Scarlett Johansson",
                    Duration    = 181,
                    ReleaseDate = new DateOnly(2024, 4, 26),
                    Poster      = "/images/movies/avengers-endgame.jpg",
                    Trailer     = "https://www.youtube.com/watch?v=TcMBFSGVi1c",
                    Status      = "NowShowing"
                },
                new Movie
                {
                    MovieID     = "2",
                    Title       = "Inception",
                    Genre       = "Hành động, Giật gân",
                    Director    = "Christopher Nolan",
                    Actors      = "Leonardo DiCaprio, Joseph Gordon-Levitt, Ellen Page",
                    Duration    = 148,
                    ReleaseDate = new DateOnly(2024, 3, 15),
                    Poster      = "/images/movies/inception.jpg",
                    Trailer     = "https://www.youtube.com/watch?v=YoHD9XEInc0",
                    Status      = "NowShowing"
                },
                new Movie
                {
                    MovieID     = "3",
                    Title       = "The Batman",
                    Genre       = "Hành động, Tội phạm",
                    Director    = "Matt Reeves",
                    Actors      = "Robert Pattinson, Zoë Kravitz, Paul Dano",
                    Duration    = 176,
                    ReleaseDate = new DateOnly(2024, 5, 20),
                    Poster      = "/images/movies/the-batman.jpg",
                    Trailer     = "https://www.youtube.com/watch?v=mqqft2x_Aa4",
                    Status      = "ComingSoon"
                },
                new Movie
                {
                    MovieID     = "4",
                    Title       = "Spider-Man: No Way Home",
                    Genre       = "Hành động, Phiêu lưu",
                    Director    = "Jon Watts",
                    Actors      = "Tom Holland, Zendaya, Benedict Cumberbatch",
                    Duration    = 148,
                    ReleaseDate = new DateOnly(2023, 12, 17),
                    Poster      = "/images/movies/spiderman-nwh.jpg",
                    Trailer     = "https://www.youtube.com/watch?v=JfVOs4VSpmA",
                    Status      = "NowShowing"
                },
                new Movie
                {
                    MovieID     = "5",
                    Title       = "Interstellar",
                    Genre       = "Khoa học viễn tưởng, Chính kịch",
                    Director    = "Christopher Nolan",
                    Actors      = "Matthew McConaughey, Anne Hathaway, Jessica Chastain",
                    Duration    = 169,
                    ReleaseDate = new DateOnly(2024, 6, 1),
                    Poster      = "/images/movies/interstellar.jpg",
                    Trailer     = "https://www.youtube.com/watch?v=zSWdZVtXT7E",
                    Status      = "ComingSoon"
                }
            );

            // ── Rooms ────────────────────────────────────────────────────────
            modelBuilder.Entity<Room>().HasData(
                new Room { RoomID = "1", RoomName = "Phòng 1 - 2D",   Capacity = 100, Status = "Active" },
                new Room { RoomID = "2", RoomName = "Phòng 2 - 3D",   Capacity = 80,  Status = "Active" },
                new Room { RoomID = "3", RoomName = "Phòng 3 - IMAX", Capacity = 120, Status = "Active" }
            );

            // ── Seats (Room 1: hàng A-E, 5 ghế mỗi hàng) ───────────────────
            var seats = new List<Seat>();
            int seatId = 1;
            var rows = new[] { "A", "B", "C", "D", "E" };

            foreach (var roomId in new[] { 1, 2, 3 })
            {
                int cols = roomId == 2 ? 4 : 5; // Room 2 nhỏ hơn
                foreach (var row in rows)
                {
                    for (int num = 1; num <= cols; num++)
                    {
                        string seatType = row == "E" ? "VIP" : (row == "A" ? "Couple" : "Standard");
                        seats.Add(new Seat
                        {
                            SeatID     = (seatId++).ToString(),
                            RoomID     = roomId.ToString(),
                            RowLabel   = row,
                            SeatNumber = num,
                            SeatType   = seatType,
                            Status     = "Active"
                        });
                    }
                }
            }
            modelBuilder.Entity<Seat>().HasData(seats);

            // ── Services ─────────────────────────────────────────────────────
            modelBuilder.Entity<Service>().HasData(
                new Service { ServiceID = "1", ServiceName = "Bắp rang bơ lớn",          Price = 55000,  Image = "/images/services/popcorn-large.jpg",  Status = "Active" },
                new Service { ServiceID = "2", ServiceName = "Bắp rang bơ vừa",          Price = 45000,  Image = "/images/services/popcorn-medium.jpg", Status = "Active" },
                new Service { ServiceID = "3", ServiceName = "Nước ngọt Pepsi lớn",      Price = 35000,  Image = "/images/services/pepsi-large.jpg",    Status = "Active" },
                new Service { ServiceID = "4", ServiceName = "Nước ngọt Pepsi vừa",      Price = 25000,  Image = "/images/services/pepsi-medium.jpg",   Status = "Active" },
                new Service { ServiceID = "5", ServiceName = "Combo 1 (Bắp lớn + Pepsi lớn)", Price = 80000,  Image = "/images/services/combo1.jpg",        Status = "Active" },
                new Service { ServiceID = "6", ServiceName = "Combo 2 (2 Bắp vừa + 2 Pepsi vừa)", Price = 130000, Image = "/images/services/combo2.jpg",  Status = "Active" }
            );

            // ── Promotions ───────────────────────────────────────────────────
            modelBuilder.Entity<Promotion>().HasData(
                new Promotion
                {
                    PromoID       = "1",
                    Title         = "SUMMER2025 - Giảm 10%",
                    DiscountValue = 10,
                    Condition     = "Áp dụng cho tất cả đơn hàng từ 100.000đ trở lên",
                    StartDate     = new DateTime(2025, 6, 1),
                    EndDate       = new DateTime(2025, 8, 31),
                    Status        = "Active"
                },
                new Promotion
                {
                    PromoID       = "2",
                    Title         = "NEWUSER - Giảm 50.000đ",
                    DiscountValue = 50000,
                    Condition     = "Chỉ áp dụng cho lần đặt vé đầu tiên",
                    StartDate     = new DateTime(2025, 1, 1),
                    EndDate       = new DateTime(2025, 12, 31),
                    Status        = "Active"
                },
                new Promotion
                {
                    PromoID       = "3",
                    Title         = "STUDENT - Giảm 20%",
                    DiscountValue = 20,
                    Condition     = "Áp dụng cho suất chiếu trước 12:00 các ngày trong tuần",
                    StartDate     = new DateTime(2025, 1, 1),
                    EndDate       = new DateTime(2025, 12, 31),
                    Status        = "Active"
                }
            );

            // ── Showtimes ────────────────────────────────────────────────────
            modelBuilder.Entity<Showtime>().HasData(
                new Showtime { ShowtimeID = "1", MovieID = "1", RoomID = "1", StartTime = new DateTime(2025, 5, 1,  9,  0, 0), EndTime = new DateTime(2025, 5,  1, 12,  1, 0) },
                new Showtime { ShowtimeID = "2", MovieID = "1", RoomID = "1", StartTime = new DateTime(2025, 5, 1,  13, 30, 0), EndTime = new DateTime(2025, 5,  1, 16, 31, 0) },
                new Showtime { ShowtimeID = "3", MovieID = "2", RoomID = "2", StartTime = new DateTime(2025, 5, 1,  10, 0, 0),  EndTime = new DateTime(2025, 5,  1, 12, 28, 0) },
                new Showtime { ShowtimeID = "4", MovieID = "2", RoomID = "2", StartTime = new DateTime(2025, 5, 1,  14, 0, 0),  EndTime = new DateTime(2025, 5,  1, 16, 28, 0) },
                new Showtime { ShowtimeID = "5", MovieID = "4", RoomID = "3", StartTime = new DateTime(2025, 5, 2,  9,  0, 0),  EndTime = new DateTime(2025, 5,  2, 11, 28, 0) },
                new Showtime { ShowtimeID = "6", MovieID = "4", RoomID = "3", StartTime = new DateTime(2025, 5, 2,  15, 0, 0),  EndTime = new DateTime(2025, 5,  2, 17, 28, 0) }
            );

            // ── PriceConfigs ─────────────────────────────────────────────────
            modelBuilder.Entity<PriceConfig>().HasData(
                // Giá cơ bản
                new PriceConfig
                {
                    ConfigID      = "1",
                    ConfigType    = "Base",
                    SeatType      = "Standard",
                    ConfigCode    = "BASE_STD",
                    ConfigName    = "Giá cơ bản ghế thường",
                    DayOfWeek     = null,
                    StartTime     = null,
                    EndTime       = null,
                    Amount        = 75000,
                    EffectiveDate = new DateOnly(2025, 1, 1)
                },
                new PriceConfig
                {
                    ConfigID      = "2",
                    ConfigType    = "Base",
                    SeatType      = "VIP",
                    ConfigCode    = "BASE_VIP",
                    ConfigName    = "Giá cơ bản ghế VIP",
                    DayOfWeek     = null,
                    StartTime     = null,
                    EndTime       = null,
                    Amount        = 120000,
                    EffectiveDate = new DateOnly(2025, 1, 1)
                },
                new PriceConfig
                {
                    ConfigID      = "3",
                    ConfigType    = "Base",
                    SeatType      = "Couple",
                    ConfigCode    = "BASE_CPL",
                    ConfigName    = "Giá cơ bản ghế đôi",
                    DayOfWeek     = null,
                    StartTime     = null,
                    EndTime       = null,
                    Amount        = 200000,
                    EffectiveDate = new DateOnly(2025, 1, 1)
                },
                // Phụ thu cuối tuần
                new PriceConfig
                {
                    ConfigID      = "4",
                    ConfigType    = "Surcharge",
                    SeatType      = null,
                    ConfigCode    = "SURCHARGE_WEEKEND",
                    ConfigName    = "Phụ thu cuối tuần",
                    DayOfWeek     = "Weekend",
                    StartTime     = null,
                    EndTime       = null,
                    Amount        = 20000,
                    EffectiveDate = new DateOnly(2025, 1, 1)
                },
                // Phụ thu suất tối
                new PriceConfig
                {
                    ConfigID      = "5",
                    ConfigType    = "Surcharge",
                    SeatType      = null,
                    ConfigCode    = "SURCHARGE_EVENING",
                    ConfigName    = "Phụ thu suất tối (sau 20:00)",
                    DayOfWeek     = null,
                    StartTime     = new TimeOnly(20, 0),
                    EndTime       = new TimeOnly(23, 59),
                    Amount        = 15000,
                    EffectiveDate = new DateOnly(2025, 1, 1)
                },
                // Giảm giá suất sáng sớm
                new PriceConfig
                {
                    ConfigID      = "6",
                    ConfigType    = "Discount",
                    SeatType      = null,
                    ConfigCode    = "DISCOUNT_MORNING",
                    ConfigName    = "Giảm giá suất sáng (trước 12:00)",
                    DayOfWeek     = "Weekday",
                    StartTime     = new TimeOnly(0, 0),
                    EndTime       = new TimeOnly(11, 59),
                    Amount        = 10000,
                    EffectiveDate = new DateOnly(2025, 1, 1)
                }
            );
        }
    }
}
