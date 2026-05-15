using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuraCinemaWeb.Models
{
    public class Seat
    {
        [Key]
        [MaxLength(50)]
        public string SeatID { get; set; } = string.Empty;

        [Required]
        [MaxLength(2)]
        public string RowLabel { get; set; } = string.Empty; // A, B, C...

        public int SeatNumber { get; set; }

        [Required]
        [MaxLength(20)]
        public string SeatType { get; set; } = "Standard"; // Standard, VIP, Couple

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Active"; // Active, Inactive, Broken

        [Required]
        [MaxLength(50)]
        [ForeignKey("Room")]
        public string RoomID { get; set; } = string.Empty;

        // Navigation
        public Room Room { get; set; } = null!;
        public ICollection<OrderSeat> OrderSeats { get; set; } = new List<OrderSeat>();
    }
}
