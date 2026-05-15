namespace AuraCinema.Domain.Entities;
public class Seat {
    public int SeatID { get; set; }
    public string SeatCode { get; set; } = string.Empty;
    public int RoomID { get; set; }
    public string RowLabel { get; set; } = string.Empty;
    public int SeatNumber { get; set; }
    public string SeatType { get; set; } = "Thuong";
    public Room Room { get; set; } = null!;
    public ICollection<OrderSeat> OrderSeats { get; set; } = new List<OrderSeat>();
}
