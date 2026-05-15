using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuraCinemaWeb.Models
{
    public class Order
    {
        [Key]
        [MaxLength(50)]
        public string OrderID { get; set; } = string.Empty;

        public int TotalAmount { get; set; }

        public int FinalAmount { get; set; }

        [MaxLength(100)]
        public string? PayTransID { get; set; }

        [MaxLength(255)]
        public string? QrCode { get; set; }

        public DateTime? HoldExpiryTime { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Pending";

        [Required]
        [MaxLength(50)]
        [ForeignKey("User")]
        public string UserID { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        [ForeignKey("Showtime")]
        public string ShowtimeID { get; set; } = string.Empty;

        [MaxLength(50)]
        [ForeignKey("Promotion")]
        public string? PromoID { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public User User { get; set; } = null!;
        public Showtime Showtime { get; set; } = null!;
        public Promotion? Promotion { get; set; }
        public ICollection<OrderSeat> OrderSeats { get; set; } = new List<OrderSeat>();
        public ICollection<OrderService> OrderServices { get; set; } = new List<OrderService>();
    }
}
