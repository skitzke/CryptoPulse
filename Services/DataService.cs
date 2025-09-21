using SQLite;
using CryptoPulse.Models;

namespace CryptoPulse.Services;

public class DataService
{
    private SQLiteAsyncConnection _conn = default!;
    private const string DbName = "cryptopulse.db3";

    private string _dbPath = string.Empty;

    // Makes sure the SQLite connection is set up and ready
    public async Task EnsureInitializedAsync()
    {
        if (_conn != null) return;

        _dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            DbName);

        _conn = new SQLiteAsyncConnection(_dbPath);
        await _conn.CreateTableAsync<PricePoint>();

        Console.WriteLine($"[DB] Initialized at {_dbPath}");
    }

    // Wipes the database clean (removes all rows)
    public async Task ClearAsync()
    {
        if (_conn == null) throw new InvalidOperationException("DB not initialized");
        await _conn.DeleteAllAsync<PricePoint>();
        Console.WriteLine("[DB] Cleared all rows");
    }

    // Seed data for just one coin + a specific time window (fine control)
    public async Task<int> SeedRangeFromApiAsync(
        ApiService api,
        string coinId,
        string vsCurrency,
        DateTime from,
        DateTime to,
        CancellationToken ct)
    {
        if (_conn == null) throw new InvalidOperationException("DB not initialized.");

        // Fetch from the API
        var prices = await api.GetMarketChartRangeAsync(coinId, vsCurrency, from, to, ct);
        if (prices.Count == 0) return 0;

        // Convert into DB entities
        var entities = prices.Select(p => new PricePoint
        {
            CoinId = coinId,
            Timestamp = p.ts,
            Price = p.price
        }).ToList();

        // Insert into SQLite
        await _conn.InsertAllAsync(entities);
        Console.WriteLine($"[DB] Inserted {entities.Count} rows for {coinId} ({from:yyyy-MM-dd} → {to:yyyy-MM-dd})");
        return entities.Count;
    }

    // Seed multiple coins at once (fetches in parallel, inserts sequentially)
    public async Task<int> SeedMultipleFromApiAsync(
        ApiService api,
        List<string> coinIds,
        string vsCurrency,
        int targetRows,
        Func<int, Task>? progress = null,
        bool clearFirst = true,
        CancellationToken ct = default)
    {
        if (_conn == null) throw new InvalidOperationException("DB not initialized.");
        if (clearFirst)
        {
            Console.WriteLine("[DB] Clearing rows before seeding…");
            await ClearAsync();
        }

        int totalInserted = 0;

        // Fetch each coin’s prices in parallel
        var fetchTasks = coinIds.Select(async coin =>
        {
            var to = DateTime.UtcNow;
            var from = to.AddDays(-365);

            var prices = await api.GetMarketChartRangeAsync(coin, vsCurrency, from, to, ct);
            return (coin, prices);
        });

        var results = await Task.WhenAll(fetchTasks);

        // Insert into DB one coin at a time
        foreach (var (coin, prices) in results)
        {
            if (ct.IsCancellationRequested) break;
            if (prices.Count == 0) continue;

            var entities = prices.Select(p => new PricePoint
            {
                CoinId = coin,
                Timestamp = p.ts,
                Price = p.price
            }).ToList();

            await _conn.InsertAllAsync(entities);
            totalInserted += entities.Count;

            // Update progress callback if provided
            if (progress != null) await progress(totalInserted);
            if (totalInserted >= targetRows) break;
        }

        Console.WriteLine($"[DB] ✅ Finished seeding {coinIds.Count} coins. Inserted {totalInserted} rows.");
        return totalInserted;
    }

    // Count how many rows exist in the DB
    public async Task<int> CountAsync()
    {
        if (_conn == null) throw new InvalidOperationException("DB not initialized");
        return await _conn.Table<PricePoint>().CountAsync();
    }

    // Return a page of rows (used for pagination in UI)
    public async Task<List<PricePoint>> GetPageAsync(int pageSize, int pageIndex)
    {
        if (_conn == null) throw new InvalidOperationException("DB not initialized");
        if (pageIndex < 0) pageIndex = 0;

        return await _conn.Table<PricePoint>()
            .OrderByDescending(p => p.Timestamp)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    // Get a snapshot of recent prices for analysis
    public async Task<Dictionary<string, List<PricePoint>>> GetSnapshotAsync(TimeSpan window)
    {
        if (_conn == null) throw new InvalidOperationException("DB not initialized");

        var cutoff = DateTime.UtcNow - window;
        var rows = await _conn.Table<PricePoint>()
            .Where(p => p.Timestamp >= cutoff)
            .ToListAsync();

        return rows
            .GroupBy(r => r.CoinId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(r => r.Timestamp).ToList()
            );
    }

    // Append a single new data point
    public async Task AppendPointAsync(string coinId, DateTime ts, decimal price)
    {
        if (_conn == null) throw new InvalidOperationException("DB not initialized");

        await _conn.InsertAsync(new PricePoint
        {
            CoinId = coinId,
            Timestamp = ts,
            Price = price
        });

        Console.WriteLine($"[DB] Appended {coinId} @ {ts:u} = {price}");
    }

    // Get the latest series of one coin for charting
    public async Task<(string CoinId, List<PricePoint> Points)> GetLatestSeriesAsync(string coinId, int take = 500)
    {
        if (_conn == null) throw new InvalidOperationException("DB not initialized");

        var rows = await _conn.Table<PricePoint>()
            .Where(p => p.CoinId == coinId)
            .OrderByDescending(p => p.Timestamp)
            .Take(take)
            .ToListAsync();

        rows.Reverse(); // Make it oldest → newest
        return (coinId, rows);
    }

    // Bulk insert points (used for batching)
    public async Task InsertBatchAsync(List<PricePoint> entities)
    {
        if (_conn == null)
            throw new InvalidOperationException("DB not initialized");

        if (entities.Count == 0) return;

        await _conn.InsertAllAsync(entities);
    }
}
