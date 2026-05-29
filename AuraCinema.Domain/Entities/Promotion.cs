namespace AuraCinema.Domain.Entities;
public class Promotion {
    public int PromoID { get; set; }
    public string PromoCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int DiscountValue { get; set; }
    public int MinAmount { get; set; } = 0;
    public string Condition { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = "Hoat dong";
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
