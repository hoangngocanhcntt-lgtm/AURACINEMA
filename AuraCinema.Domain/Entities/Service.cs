namespace AuraCinema.Domain.Entities;
public class Service {
    public int ServiceID { get; set; }
    public string ServiceCode { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public int Price { get; set; }
    public string Image { get; set; } = string.Empty;
    public string Status { get; set; } = "Hoat dong";
    public ICollection<OrderService> OrderServices { get; set; } = new List<OrderService>();
}
