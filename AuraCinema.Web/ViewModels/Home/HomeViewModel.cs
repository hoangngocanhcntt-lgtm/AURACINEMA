namespace AuraCinema.Web.ViewModels.Home;

public class HomeViewModel
{
    public List<MovieCardViewModel> FeaturedMovies { get; set; } = new();
    public List<MovieCardViewModel> NowShowing { get; set; } = new();
    public List<MovieCardViewModel> ComingSoon { get; set; } = new();
    public List<AuraCinema.Web.ViewModels.MyTickets.TicketItemViewModel> RecentTickets { get; set; } = new();
    
    public int TotalRooms { get; set; }
    public int TotalSeats { get; set; }
}

public class MovieCardViewModel
{
    public int MovieID { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public string Poster { get; set; } = string.Empty;
    public int Duration { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateOnly ReleaseDate { get; set; }
    public string Director { get; set; } = string.Empty;
    public bool HasEarlyTickets { get; set; }
}
