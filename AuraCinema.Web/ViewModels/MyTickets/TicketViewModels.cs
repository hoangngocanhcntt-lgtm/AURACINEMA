namespace AuraCinema.Web.ViewModels.MyTickets;

public class TicketItemViewModel
{
    public int OrderID { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string MovieTitle { get; set; } = string.Empty;
    public string Poster { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public DateTime Showtime { get; set; }
    public string SeatList { get; set; } = string.Empty;
    public int FinalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class TicketDetailViewModel : TicketItemViewModel
{
    public string QrCodeBase64 { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? PayOSTransID { get; set; }
    public string? PromotionTitle { get; set; }
    public int BaseAmount { get; set; }
    public int Discount { get; set; }
    public List<string> ServiceList { get; set; } = new();
}
