namespace AuraCinema.Web.ViewModels.Booking;

public class SelectSeatsViewModel
{
    public int ShowtimeID { get; set; }
    public string MovieTitle { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public string ShowtimeLabel { get; set; } = string.Empty; // e.g. "14:30 - 20/05/2026"
    public string MoviePoster { get; set; } = string.Empty;

    public List<SeatRowViewModel> Rows { get; set; } = new();
    
    // Ghế đã bị người khác mua hoặc giữ
    public List<int> SoldOrHeldSeatIds { get; set; } = new();
    
    // Cấu hình giá để tính nhẩm trên client
    public int BasePrice { get; set; }
    public int VipSurcharge { get; set; }
    public int CoupleSurcharge { get; set; }
    public int DaySurcharge { get; set; }
}

public class SeatRowViewModel
{
    public string RowLabel { get; set; } = string.Empty;
    public List<SeatItemViewModel> Seats { get; set; } = new();
}

public class SeatItemViewModel
{
    public int SeatID { get; set; }
    public int SeatNumber { get; set; }
    public string SeatType { get; set; } = string.Empty; // "Thuong", "VIP", "Doi"
}

public class CreateBookingRequest
{
    public int ShowtimeId { get; set; }
    public List<int> SeatIds { get; set; } = new();
    public List<int> ServiceIds { get; set; } = new();
}
