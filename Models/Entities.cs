using SQLite;

namespace CryptoPulse.Models;

// Represents one row in the SQLite database.
// Each point is basically: "this coin had this price at this timestamp".
public class PricePoint
{
    [PrimaryKey, AutoIncrement] 
    public int Id { get; set; }   // Auto-increment ID, just for database storage.

    [Indexed]
    public string CoinId { get; set; } = ""; // e.g. "bitcoin", "ethereum", etc. Indexed so lookups are faster.

    [Indexed]
    public DateTime Timestamp { get; set; }  // When this price was recorded (UTC).

    public decimal Price { get; set; }       // The price value itself.
}

// Represents a row in the "Top Movers" list (used in the chart sidebar).
// This is not stored in the DB, it's just built in memory for UI display.
public record MoverRow(
    string CoinId,       // which coin moved
    decimal StartPrice,  // starting price (beginning of window)
    decimal EndPrice,    // ending price (latest in window)
    decimal ChangePct    // % change (gain/loss)
);
