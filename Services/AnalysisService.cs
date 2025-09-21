using CryptoPulse.Models;

namespace CryptoPulse.Services;

public sealed class AnalysisService
{
    private readonly DataService _db;

    public AnalysisService(DataService db)
    {
        _db = db;
    }

    // Work out the top movers (biggest percentage gainers/losers) in the given time window.
    // I calculate the % change between the first price inside the window and the most recent price.
    // This gives me "what coin moved the most in X hours/days".
    public List<MoverRow> ComputeTopMovers(
        IReadOnlyDictionary<string, List<PricePoint>> dataUtc,
        TimeSpan window)
    {
        var now = DateTime.UtcNow;
        var from = now - window;

        var rows = new List<MoverRow>();

        foreach (var (id, list) in dataUtc)
        {
            if (list.Count < 2) continue; // not enough data points to compare

            var last = list[^1]; // latest point
            var first = list.FirstOrDefault(p => p.Timestamp >= from) ?? list[0]; 
            // ^ if no point in the window, just use the oldest one we have

            var prev = first.Price;
            var nowP = last.Price;

            // % change calculation (basic formula)
            var change = prev == 0 ? 0m : (nowP - prev) / prev * 100m;

            rows.Add(new MoverRow(id, prev, nowP, change));
        }

        // Order by absolute movement so biggest gainers and losers show up
        return rows
            .OrderByDescending(r => Math.Abs(r.ChangePct))
            .Take(20) // top 20 movers
            .ToList();
    }

    // Async version: grab snapshot from DB first, then reuse the logic above.
    // This is what I actually call from the ViewModel.
    public async Task<List<MoverRow>> ComputeTopMoversAsync(TimeSpan window)
    {
        var snapshot = await _db.GetSnapshotAsync(window);
        return ComputeTopMovers(snapshot, window);
    }
}
