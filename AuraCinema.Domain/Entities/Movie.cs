namespace AuraCinema.Domain.Entities;
public class Movie {
    public int MovieID { get; set; }
    public string MovieCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public string Director { get; set; } = string.Empty;
    public string Actors { get; set; } = string.Empty;
    public int Duration { get; set; }
    public DateOnly ReleaseDate { get; set; }
    public string Poster { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Trailer { get; set; } = string.Empty;
    public string Status { get; set; } = "Dang chieu";
    public ICollection<Showtime> Showtimes { get; set; } = new List<Showtime>();
}
