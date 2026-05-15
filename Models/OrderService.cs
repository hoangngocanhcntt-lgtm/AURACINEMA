using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuraCinemaWeb.Models
{
    public class OrderService
    {
        [Required]
        [MaxLength(50)]
        [ForeignKey("Order")]
        public string OrderID { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        [ForeignKey("Service")]
        public string ServiceID { get; set; } = string.Empty;

        public int Quantity { get; set; }

        public int Price { get; set; }

        // Navigation
        public Order Order { get; set; } = null!;
        public Service Service { get; set; } = null!;
    }
}
