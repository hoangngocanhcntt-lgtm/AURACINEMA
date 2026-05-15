namespace AuraCinema.Web.ViewModels.Booking;

public class CheckoutViewModel
{
    public int OrderID { get; set; }
    public string MovieTitle { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public string ShowtimeLabel { get; set; } = string.Empty;
    public string SeatList { get; set; } = string.Empty; // e.g. "A1, A2, VIP1"
    public string MoviePoster { get; set; } = string.Empty;

    public int BaseTotal { get; set; }
    public int Discount { get; set; }
    public int FinalTotal { get; set; }
    
    public DateTime HoldExpiryTime { get; set; }
    public string? PromoCode { get; set; }
    public string? PromoError { get; set; }
}
