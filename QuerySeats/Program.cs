using Microsoft.Data.SqlClient;

var connStr = "Server=(localdb)\\mssqllocaldb;Database=AuraCinemaDb_Dev;Trusted_Connection=True;TrustServerCertificate=True";
using var conn = new SqlConnection(connStr);
conn.Open();

Console.WriteLine("=== PHONG 1 (RoomID=4) SEATS ===");
using (var cmd = new SqlCommand(@"
    SELECT s.RowLabel, s.SeatNumber, s.SeatType, s.SeatID 
    FROM Seats s WHERE s.RoomID = 4
    ORDER BY s.RowLabel, s.SeatNumber", conn))
using (var reader = cmd.ExecuteReader())
{
    while (reader.Read())
        Console.WriteLine($"  {reader.GetString(0)}{reader.GetInt32(1)} Type='{reader.GetString(2)}' ID={reader.GetInt32(3)}");
}

// Check sold/held seats for showtime 9128
Console.WriteLine("\n=== SOLD/HELD SEATS FOR SHOWTIME 9128 ===");
using (var cmd = new SqlCommand(@"
    SELECT os.SeatID, s.RowLabel, s.SeatNumber, s.SeatType, o.Status, o.HoldExpiryTime
    FROM OrderSeats os
    JOIN Orders o ON os.OrderID = o.OrderID
    JOIN Seats s ON os.SeatID = s.SeatID
    WHERE o.ShowtimeID = 9128
    ORDER BY s.RowLabel, s.SeatNumber", conn))
using (var reader = cmd.ExecuteReader())
{
    while (reader.Read())
        Console.WriteLine($"  Seat {reader.GetString(1)}{reader.GetInt32(2)} ({reader.GetString(3)}) ID={reader.GetInt32(0)} OrderStatus='{reader.GetString(4)}' Expiry={reader.GetDateTime(5):HH:mm:ss}");
}

// Also run the same logic as GetShowtimeSeatLayoutAsync
Console.WriteLine("\n=== SOLD OR HELD SEAT IDS (same logic as BookingService) ===");
using (var cmd = new SqlCommand(@"
    SELECT DISTINCT os.SeatID
    FROM OrderSeats os
    JOIN Orders o ON os.OrderID = o.OrderID
    WHERE o.ShowtimeID = 9128
    AND (o.Status = N'Đã thanh toán' OR (o.Status = N'Chờ thanh toán' AND o.HoldExpiryTime > GETDATE()))", conn))
using (var reader = cmd.ExecuteReader())
{
    var ids = new List<int>();
    while (reader.Read()) ids.Add(reader.GetInt32(0));
    Console.WriteLine($"  Sold/Held: [{string.Join(", ", ids)}]");
}
