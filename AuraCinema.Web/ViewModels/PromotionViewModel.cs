namespace AuraCinema.Web.ViewModels;

public class PromotionViewModel
{
    public int PromoID { get; set; }
    public string Title { get; set; } = string.Empty;
    public int DiscountValue { get; set; }
    public string Condition { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public int DaysRemaining => (EndDate - DateTime.Now).Days;
}
