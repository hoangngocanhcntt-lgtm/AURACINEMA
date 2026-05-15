namespace AuraCinema.Domain.Entities;
public class OrderService {
    public int OrderID { get; set; }
    public int ServiceID { get; set; }
    public int Quantity { get; set; }
    public int Price { get; set; }
    public Order Order { get; set; } = null!;
    public Service Service { get; set; } = null!;
}
