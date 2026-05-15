namespace AuraCinema.Domain.Entities;
public class User {
    public int UserID { get; set; }
    public string UserCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Role { get; set; } = "Khach hang";
    public string? OtpCode { get; set; }
    public DateTime? OtpExpiry { get; set; }
    public string Status { get; set; } = "Hoat dong";
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
