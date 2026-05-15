using System.ComponentModel.DataAnnotations;

namespace AuraCinemaWeb.Models
{
    public class Movie
    {
        [Key]
        [MaxLength(50)]
        public string MovieID { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? Genre { get; set; }

        [MaxLength(100)]
        public string? Director { get; set; }

        [MaxLength(500)]
        public string? Actors { get; set; }

        public int Duration { get; set; } // phút

        public DateOnly? ReleaseDate { get; set; }

        [MaxLength(255)]
        public string? Poster { get; set; }

        [MaxLength(255)]
        public string? Trailer { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "ComingSoon"; // NowShowing, ComingSoon, Ended

        // Navigation
        public ICollection<Showtime> Showtimes { get; set; } = new List<Showtime>();
    }
}
