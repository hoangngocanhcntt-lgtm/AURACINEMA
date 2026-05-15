using Microsoft.EntityFrameworkCore;
using AuraCinema.Domain.Entities;

namespace AuraCinema.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<Showtime> Showtimes => Set<Showtime>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<PriceConfig> PriceConfigs => Set<PriceConfig>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderSeat> OrderSeats => Set<OrderSeat>();
    public DbSet<OrderService> OrderServices => Set<OrderService>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        mb.Entity<User>(e => {
            e.HasKey(x => x.UserID);
            e.Property(x => x.Email).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Password).HasMaxLength(255).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(10);
            e.Property(x => x.Role).HasMaxLength(20);
            e.Property(x => x.OtpCode).HasMaxLength(6);
            e.Property(x => x.Status).HasMaxLength(20);
        });
        mb.Entity<Movie>(e => {
            e.HasKey(x => x.MovieID);
            e.Property(x => x.Title).HasMaxLength(255).IsRequired();
            e.Property(x => x.Status).HasMaxLength(50);
        });
        mb.Entity<Room>(e => {
            e.HasKey(x => x.RoomID);
            e.Property(x => x.RoomName).HasMaxLength(50).IsRequired();
            e.Property(x => x.Status).HasMaxLength(50);
        });
        mb.Entity<Seat>(e => {
            e.HasKey(x => x.SeatID);
            e.Property(x => x.RowLabel).HasMaxLength(2).IsRequired();
            e.Property(x => x.SeatType).HasMaxLength(20);
            e.HasOne(x => x.Room).WithMany(r => r.Seats)
             .HasForeignKey(x => x.RoomID).OnDelete(DeleteBehavior.Restrict);
        });
        mb.Entity<Showtime>(e => {
            e.HasKey(x => x.ShowtimeID);
            e.HasOne(x => x.Movie).WithMany(m => m.Showtimes)
             .HasForeignKey(x => x.MovieID).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Room).WithMany(r => r.Showtimes)
             .HasForeignKey(x => x.RoomID).OnDelete(DeleteBehavior.Restrict);
        });
        mb.Entity<Service>(e => {
            e.HasKey(x => x.ServiceID);
            e.Property(x => x.ServiceName).HasMaxLength(100).IsRequired();
        });
        mb.Entity<Promotion>(e => {
            e.HasKey(x => x.PromoID);
            e.Property(x => x.Title).HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.Title).IsUnique();
            e.Property(x => x.Condition).HasMaxLength(255);
        });
        mb.Entity<PriceConfig>(e => {
            e.HasKey(x => x.ConfigID);
            e.Property(x => x.ConfigCode).HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.ConfigCode).IsUnique();
            e.Property(x => x.ConfigType).HasMaxLength(50);
            e.Property(x => x.ConfigName).HasMaxLength(100);
        });
        mb.Entity<Order>(e => {
            e.HasKey(x => x.OrderID);
            e.Property(x => x.Status).HasMaxLength(50);
            e.HasOne(x => x.User).WithMany(u => u.Orders)
             .HasForeignKey(x => x.UserID).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Showtime).WithMany(s => s.Orders)
             .HasForeignKey(x => x.ShowtimeID).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Promotion).WithMany(p => p.Orders)
             .HasForeignKey(x => x.PromoID).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => new { x.ShowtimeID, x.Status, x.HoldExpiryTime });
        });
        mb.Entity<OrderSeat>(e => {
            e.HasKey(x => new { x.OrderID, x.SeatID });
            e.HasOne(x => x.Order).WithMany(o => o.OrderSeats)
             .HasForeignKey(x => x.OrderID).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Seat).WithMany(s => s.OrderSeats)
             .HasForeignKey(x => x.SeatID).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Status).HasMaxLength(20);
            e.HasIndex(x => new { x.SeatID, x.OrderID });
        });
        mb.Entity<OrderService>(e => {
            e.HasKey(x => new { x.OrderID, x.ServiceID });
            e.HasOne(x => x.Order).WithMany(o => o.OrderServices)
             .HasForeignKey(x => x.OrderID).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Service).WithMany(s => s.OrderServices)
             .HasForeignKey(x => x.ServiceID).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
