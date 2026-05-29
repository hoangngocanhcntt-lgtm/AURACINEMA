using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuraCinema.Domain.Entities;

public class RefundRequest
{
    [Key]
    public int RefundID { get; set; }

    [Required]
    public int OrderID { get; set; }

    [Required]
    [MaxLength(100)]
    public string BankName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string AccountNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string AccountName { get; set; } = string.Empty;

    public DateTime? ResolvedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [ForeignKey("OrderID")]
    public Order Order { get; set; } = null!;
}
