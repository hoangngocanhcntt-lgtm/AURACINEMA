namespace AuraCinema.Domain.Entities;
public class Order {
    public int OrderID { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public int UserID { get; set; }
    public int ShowtimeID { get; set; }
    public int? PromoID { get; set; }
    public int TotalAmount { get; set; }
    public int FinalAmount { get; set; }
    public string? PayOSTransID { get; set; }
    public string? QrCode { get; set; }
    public DateTime HoldExpiryTime { get; set; }
    public DateTime? CheckInTime { get; set; }
    public string Status { get; set; } = "Cho thanh toan";
    public User User { get; set; } = null!;
    public Showtime Showtime { get; set; } = null!;
    public Promotion? Promotion { get; set; }
    public ICollection<OrderSeat> OrderSeats { get; set; } = new List<OrderSeat>();
    public ICollection<OrderService> OrderServices { get; set; } = new List<OrderService>();
    public RefundRequest? RefundRequest { get; set; }
}
