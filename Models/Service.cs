using System.ComponentModel.DataAnnotations;

namespace AuraCinemaWeb.Models
{
    public class Service
    {
        [Key]
        [MaxLength(50)]
        public string ServiceID { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string ServiceName { get; set; } = string.Empty;

        public int Price { get; set; }

        [MaxLength(255)]
        public string? Image { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Active"; // Active, Inactive

        // Navigation
        public ICollection<OrderService> OrderServices { get; set; } = new List<OrderService>();
    }
}
