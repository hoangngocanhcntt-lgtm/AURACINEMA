namespace AuraCinema.Web.Areas.Admin.ViewModels;

public class DashboardViewModel
{
    // Filters
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string RangeType { get; set; } = "7days";

    // KPI Scorecards
    public int TotalRevenue { get; set; }
    public int TotalTicketsSold { get; set; }
    public int TotalConcessionRevenue { get; set; }
    public int TotalTicketRevenue { get; set; }
    public double RoomOccupancyRate { get; set; } // %

    // Chart Data
    public LineChartData RevenueOverTime { get; set; } = new();
    public PieChartData RevenueByMovie { get; set; } = new();
    public PieChartData PaymentMethods { get; set; } = new();
    public HorizontalBarChartData TopMoviesByRevenue { get; set; } = new();
    public ColumnChartData CustomersByTime { get; set; } = new();
    public FunnelChartData PromotionUsage { get; set; } = new();
}

public class LineChartData
{
    public List<string> Labels { get; set; } = new();
    public List<int> Data { get; set; } = new();
}

public class PieChartData
{
    public List<string> Labels { get; set; } = new();
    public List<int> Data { get; set; } = new();
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
