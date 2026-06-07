// Quick query to check SeatType values and seat layout for a showtime
using Microsoft.Data.SqlClient;

var connStr = "Server=(localdb)\\mssqllocaldb;Database=AuraCinemaDb;Trusted_Connection=True;TrustServerCertificate=True";
using var conn = new SqlConnection(connStr);
conn.Open();

// 1. Check distinct SeatType values
Console.WriteLine("=== DISTINCT SeatType VALUES ===");
using (var cmd = new SqlCommand("SELECT DISTINCT SeatType, COUNT(*) as cnt FROM Seats GROUP BY SeatType", conn))
using (var reader = cmd.ExecuteReader())
{
    while (reader.Read())
        Console.WriteLine($"  SeatType='{reader.GetString(0)}' Count={reader.GetInt32(1)}");
}

// 2. Check Phong 1 seat layout  
Console.WriteLine("\n=== SEATS IN PHONG 1 (Room 1) ===");
using (var cmd = new SqlCommand(@"
    SELECT s.RowLabel, s.SeatNumber, s.SeatType, s.SeatID 
    FROM Seats s 
    JOIN Rooms r ON s.RoomID = r.RoomID 
    WHERE r.RoomName LIKE '%Phong 1%' OR r.RoomName LIKE '%Phòng 1%' OR r.RoomID = 1
    ORDER BY s.RowLabel, s.SeatNumber", conn))
using (var reader = cmd.ExecuteReader())
{
    while (reader.Read())
        Console.WriteLine($"  {reader.GetString(0)}{reader.GetInt32(1)} Type='{reader.GetString(2)}' ID={reader.GetInt32(3)}");
}

// 3. Check sold/held seats for the relevant showtime (THE SHEEP DETECTIVES, 09:00)
Console.WriteLine("\n=== SHOWTIMES FOR 'SHEEP' ===");
using (var cmd = new SqlCommand(@"
    SELECT st.ShowtimeID, m.Title, st.StartTime, st.Status, r.RoomName
    FROM Showtimes st 
    JOIN Movies m ON st.MovieID = m.MovieID 
    JOIN Rooms r ON st.RoomID = r.RoomID
    WHERE m.Title LIKE '%SHEEP%' OR m.Title LIKE '%sheep%'
    ORDER BY st.StartTime", conn))
using (var reader = cmd.ExecuteReader())
{
    while (reader.Read())
        Console.WriteLine($"  ShowtimeID={reader.GetInt32(0)} '{reader.GetString(1)}' {reader.GetDateTime(2):yyyy-MM-dd HH:mm} Status='{reader.GetString(3)}' Room='{reader.GetString(4)}'");
}
