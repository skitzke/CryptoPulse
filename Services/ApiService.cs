using System.Text.Json;

namespace CryptoPulse.Services;

public class ApiService
{
    private readonly HttpClient _http;

    public ApiService(HttpClient http)
    {
        _http = http;
    }

    // Fetch market history between two dates.
    // I chunk it because CoinGecko has limits on how much data you can pull at once.
    // Also I don’t force an interval param → CoinGecko decides best granularity.
    public async Task<List<(DateTime ts, decimal price)>> GetMarketChartRangeAsync(
        string coinId,
        string vsCurrency,
        DateTime from,
        DateTime to,
        CancellationToken ct = default)
    {
        var prices = new List<(DateTime ts, decimal price)>();

        DateTime chunkStart = from;
        while (chunkStart < to)
        {
            // I pick chunk size dynamically depending on how many days I’m fetching
            TimeSpan chunkSize;
            var totalDays = (to - chunkStart).TotalDays;

            if (totalDays <= 1)
                chunkSize = TimeSpan.FromDays(1);       // intraday (5-min data)
            else if (totalDays <= 90)
                chunkSize = TimeSpan.FromDays(90);      // hourly data
            else
                chunkSize = TimeSpan.FromDays(180);     // daily data

            var chunkEnd = chunkStart.Add(chunkSize);
            if (chunkEnd > to) chunkEnd = to;

            long fromUnix = new DateTimeOffset(chunkStart).ToUnixTimeSeconds();
            long toUnix = new DateTimeOffset(chunkEnd).ToUnixTimeSeconds();

            string url = $"coins/{coinId}/market_chart/range" +
                         $"?vs_currency={vsCurrency}" +
                         $"&from={fromUnix}&to={toUnix}";

            LogRequest(url);

            // Do the HTTP call
            using var resp = await _http.GetAsync(url, ct);

            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                throw new HttpRequestException(
                    $"401 Unauthorized → Check COINGECKO_API_KEY in .env. URL={_http.BaseAddress}{url}");

            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException(
                    $"GetMarketChartRangeAsync failed ({(int)resp.StatusCode} {resp.ReasonPhrase}): {err}");
            }

            // Parse JSON
            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("prices", out var priceArray))
            {
                Console.WriteLine($"[ApiService] WARNING: No 'prices' array in response for {coinId}");
                break;
            }

            int rowCount = 0;
            foreach (var arr in priceArray.EnumerateArray())
            {
                long ms = (long)arr[0].GetDouble(); // timestamp in ms
                decimal price = (decimal)arr[1].GetDouble();
                prices.Add((DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime, price));
                rowCount++;
            }

            Console.WriteLine($"[ApiService] Parsed {rowCount} rows for {coinId} ({chunkStart:u} → {chunkEnd:u})");

            chunkStart = chunkEnd;
        }

        Console.WriteLine($"[ApiService] Total parsed rows for {coinId}: {prices.Count}");

        // Deduplicate just in case API sends duplicates
        return prices
            .GroupBy(p => p.ts)
            .Select(g => g.First())
            .OrderBy(p => p.ts)
            .ToList();
    }

    // Simple price endpoint: fetch current price of one coin
    public async Task<decimal> GetSimplePriceAsync(
        string coinId,
        string vsCurrency,
        CancellationToken ct = default)
    {
        string url = $"simple/price?ids={coinId}&vs_currencies={vsCurrency}";
        LogRequest(url);

        using var resp = await _http.GetAsync(url, ct);

        if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            throw new HttpRequestException(
                $"401 Unauthorized → Check COINGECKO_API_KEY in .env. URL={_http.BaseAddress}{url}");

        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"GetSimplePriceAsync failed ({(int)resp.StatusCode} {resp.ReasonPhrase}): {err}");
        }

        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty(coinId, out var coinObj) &&
            coinObj.TryGetProperty(vsCurrency, out var priceElement))
        {
            return (decimal)priceElement.GetDouble();
        }

        return 0m;
    }

    // Just logs the request so I can see if the PRO API key was attached
    private void LogRequest(string url)
    {
        bool hasKey = _http.DefaultRequestHeaders.Any(
            h => h.Key.Equals("x-cg-pro-api-key", StringComparison.OrdinalIgnoreCase));

        Console.WriteLine($"[ApiService] GET {_http.BaseAddress}{url} | API Key attached? {hasKey}");
    }
}
