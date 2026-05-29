namespace AuraCinema.Web.ViewModels.Movie;

public class MovieDetailViewModel
{
    public int MovieID { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public string Director { get; set; } = string.Empty;
    public string Actors { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Duration { get; set; }
    public DateOnly ReleaseDate { get; set; }
    public string Poster { get; set; } = string.Empty;
    public string Trailer { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public List<ShowtimeGroupViewModel> ShowtimeGroups { get; set; } = new();
}

public class ShowtimeGroupViewModel
{
    public DateOnly Date { get; set; }
    public string DayLabel { get; set; } = string.Empty; // "Hôm nay", "Ngày mai", "12/05"
    public List<ShowtimeItemViewModel> Showtimes { get; set; } = new();
}

public class ShowtimeItemViewModel
{
    public int ShowtimeID { get; set; }
    public string StartTime { get; set; } = string.Empty; // "14:30"
    public string EndTime { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public int AvailableSeats { get; set; }
    public int TotalSeats { get; set; }
}

public class MovieListViewModel
{
    public List<Home.MovieCardViewModel> Movies { get; set; } = new();
    public string? SearchTerm { get; set; }
    public string? StatusFilter { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; }
}
