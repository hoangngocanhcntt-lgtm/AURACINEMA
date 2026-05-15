namespace AuraCinema.Domain.Entities;
public class Showtime {
    public int ShowtimeID { get; set; }
    public string ShowtimeCode { get; set; } = string.Empty;
    public int MovieID { get; set; }
    public int RoomID { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public Movie Movie { get; set; } = null!;
    public Room Room { get; set; } = null!;
    public string Status { get; set; } = "Sap chieu";
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
