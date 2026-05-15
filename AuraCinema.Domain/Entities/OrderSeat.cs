namespace AuraCinema.Domain.Entities;
public class OrderSeat {
    public int OrderID { get; set; }
    public int SeatID { get; set; }
    public int Price { get; set; }
    public string Status { get; set; } = "Tam khoa"; // Tam khoa, Da ban
    public Order Order { get; set; } = null!;
    public Seat Seat { get; set; } = null!;
}
