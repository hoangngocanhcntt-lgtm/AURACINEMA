using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuraCinemaWeb.Models
{
    public class OrderSeat
    {
        [Required]
        [MaxLength(50)]
        [ForeignKey("Order")]
        public string OrderID { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        [ForeignKey("Seat")]
        public string SeatID { get; set; } = string.Empty;

        public int Price { get; set; }

        // Navigation
        public Order Order { get; set; } = null!;
        public Seat Seat { get; set; } = null!;
    }
}
