using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuraCinemaWeb.Models
{
    public class Showtime
    {
        [Key]
        [MaxLength(50)]
        public string ShowtimeID { get; set; } = string.Empty;

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        [Required]
        [MaxLength(50)]
        [ForeignKey("Movie")]
        public string MovieID { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        [ForeignKey("Room")]
        public string RoomID { get; set; } = string.Empty;

        // Navigation
        public Movie Movie { get; set; } = null!;
        public Room Room { get; set; } = null!;
        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}
