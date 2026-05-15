using AuraCinema.Domain.Entities;
using AuraCinema.Domain.Models.Booking;

namespace AuraCinema.Web.ViewModels.Booking;

public class AddServicesViewModel
{
    public int ShowtimeID { get; set; }
    public string MovieTitle { get; set; } = string.Empty;
    public string ShowtimeLabel { get; set; } = string.Empty;
    public string SeatList { get; set; } = string.Empty;
    public List<int> SelectedSeatIds { get; set; } = new();
    public List<Service> AvailableServices { get; set; } = new();
}

public class ConfirmBookingRequest
{
    public int ShowtimeId { get; set; }
    public List<int> SeatIds { get; set; } = new();
    public List<ServiceSelection> Services { get; set; } = new();
}
