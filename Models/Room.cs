using System.ComponentModel.DataAnnotations;

namespace AuraCinemaWeb.Models
{
    public class Room
    {
        [Key]
        [MaxLength(50)]
        public string RoomID { get; set; } = string.Empty;

        [Required]
        [MaxLength(10)]
        public string RoomName { get; set; } = string.Empty;

        public int Capacity { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Active"; // Active, Maintenance, Inactive

        // Navigation
        public ICollection<Seat> Seats { get; set; } = new List<Seat>();
        public ICollection<Showtime> Showtimes { get; set; } = new List<Showtime>();
    }
}
