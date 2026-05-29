namespace AuraCinema.Domain.Entities;
public class Room {
    public int RoomID { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public int Capacity { get; set; } = 50;
    public string Status { get; set; } = "Hoat dong";
    public ICollection<Seat> Seats { get; set; } = new List<Seat>();
    public ICollection<Showtime> Showtimes { get; set; } = new List<Showtime>();
}
