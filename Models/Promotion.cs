using System.ComponentModel.DataAnnotations;

namespace AuraCinemaWeb.Models
{
    public class Promotion
    {
        [Key]
        [MaxLength(50)]
        public string PromoID { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Title { get; set; } = string.Empty;

        public int DiscountValue { get; set; }

        [MaxLength(255)]
        public string? Condition { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Active"; // Active, Inactive, Expired

        // Navigation
        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}
