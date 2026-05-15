namespace AuraCinema.Web.Areas.Admin.ViewModels;

public class DashboardViewModel
{
    // KPI Scorecards
    public int TotalRevenue { get; set; }
    public int TotalTicketsSold { get; set; }
    public int TotalConcessionOrders { get; set; }
    public double RoomOccupancyRate { get; set; } // %

    // Chart Data
    public LineChartData RevenueOverTime { get; set; } = new();
    public DonutChartData RevenueStructure { get; set; } = new();
    public PieChartData PaymentMethods { get; set; } = new();
    public HorizontalBarChartData TopMovies { get; set; } = new();
    public ColumnChartData CustomersByTime { get; set; } = new();
    public FunnelChartData PromotionUsage { get; set; } = new();
}

public class LineChartData
{
    public List<string> Labels { get; set; } = new();
    public List<int> Data { get; set; } = new();
}

public class DonutChartData
{
    public int TicketRevenue { get; set; }
    public int ConcessionRevenue { get; set; }
}

public class PieChartData
{
    public int PayOS { get; set; }
    public int Cash { get; set; }
    public int Momo { get; set; }
}

public class HorizontalBarChartData
{
    public List<string> Labels { get; set; } = new();
    public List<int> Data { get; set; } = new();
}

public class ColumnChartData
{
    public List<string> Labels { get; set; } = new();
    public List<int> Data { get; set; } = new();
}

public class FunnelChartData
{
    public int TotalOrders { get; set; }
    public int OrdersWithPromoCode { get; set; }
}
