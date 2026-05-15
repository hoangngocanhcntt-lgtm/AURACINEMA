using System.ComponentModel.DataAnnotations;

namespace AuraCinemaWeb.Models
{
    public class User
    {
        [Key]
        [MaxLength(50)]
        public string UserID { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string Password { get; set; } = string.Empty;

        [MaxLength(10)]
        public string? Phone { get; set; }

        [Required]
        [MaxLength(20)]
        public string Role { get; set; } = "Customer"; // Customer, Staff, Admin

        [MaxLength(6)]
        public string? OtpCode { get; set; }

        public DateTime? OtpExpiry { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Active"; // Active, Inactive, Locked

        // Navigation
        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}
