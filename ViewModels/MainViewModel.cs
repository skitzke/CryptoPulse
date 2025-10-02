using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.Defaults;
using SkiaSharp;
using CryptoPulse.Services;
using CryptoPulse.Models;
using Microsoft.Maui.ApplicationModel;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoPulse.ViewModels;

public class MainViewModel : INotifyPropertyChanged, IDisposable
{
    // Services for API, DB and Analysis
    private readonly ApiService _api;
    private readonly DataService _db;
    private readonly AnalysisService _analysis;

    // Used for cancellation of background tasks
    private CancellationTokenSource _cts = new();
    private Task? _liveLoop;

    // Progress bar binding for seeding
    private double _seedProgress;
    public double SeedProgress
    {
        get => _seedProgress;
        private set { _seedProgress = value; OnPropertyChanged(); }
    }

    // Tracks how many rows are currently in the DB
    private int _rowCount;
    public int RowCount
    {
        get => _rowCount;
        private set { _rowCount = value; OnPropertyChanged(); }
    }

    // Shows current status message in the UI
    private string _status = "Ready.";
    public string Status
    {
        get => _status;
        private set { _status = value; OnPropertyChanged(); }
    }

    // CollectionView is bound to this to show database rows
    public ObservableCollection<PricePoint> SQLiteEntries { get; } = new();

    // Paging state
    private int _currentPage = 1;
    public int CurrentPage
    {
        get => _currentPage;
        set
        {
            _currentPage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PageInfo));
            OnPropertyChanged(nameof(IsLastPage));
        }
    }

    public int PageSize { get; } = 500;
    private int _totalRows;
    public int TotalPages => _totalRows > 0 ? (int)Math.Ceiling((double)_totalRows / PageSize) : 1;
    public bool IsLastPage => CurrentPage >= TotalPages;
    public string PageInfo => $"Page {CurrentPage}/{TotalPages} — showing {SQLiteEntries.Count:N0} rows out of {_totalRows:N0}";

    // Commands for the UI buttons
    public Helpers.AsyncCommand SeedCommand { get; private set; } = default!;
    public Helpers.AsyncCommand StartLiveCommand { get; private set; } = default!;
    public Helpers.AsyncCommand StopLiveCommand { get; private set; } = default!;
    public Helpers.AsyncCommand PrevPageCommand { get; private set; } = default!;
    public Helpers.AsyncCommand NextPageCommand { get; private set; } = default!;

    // Chart data
    public ObservableCollection<ISeries> Series { get; }
    public List<Axis> XAxes { get; }
    public List<Axis> YAxes { get; }

    public LiveChartsCore.Measure.LegendPosition LegendPosition { get; } = LiveChartsCore.Measure.LegendPosition.Right;

    // List of top movers (biggest changes in last 24h)
    public ObservableCollection<MoverRow> TopMovers { get; } = new();

    // Core coin set
    private readonly List<string> _coins = new()
    {
        "bitcoin", "ethereum", "solana", "cardano", "dogecoin",
        "litecoin", "polkadot", "tron", "avalanche-2", "chainlink",
        "monero", "stellar", "uniswap", "cosmos", "near",
        "algorand", "aptos", "hedera-hashgraph", "fantom", "arweave",
        "vechain", "filecoin", "tezos", "eos", "the-graph",
        "elrond-erd-2", "theta-token", "flow", "aave", "maker"
    };

    // Extra coins just to boost DB size to 100k rows
    private readonly List<string> _extraCoins = new()
    {
        "internet-computer", "quant-network", "gala", "injective-protocol", "klay-token",
        "neo", "dash", "chiliz", "curve-dao-token", "optimism",
        "zilliqa", "mina-protocol", "pancakeswap-token", "iota", "kava",
        "waves", "sui", "blur", "helium", "oasis-network",
        "ravencoin", "kusama", "decred", "loopring", "convex-finance",
        "gnosis", "yearn-finance", "ankr", "0x", "enjincoin",
        "bancor", "wax", "fetch-ai", "balancer", "harmony",
        "ocean-protocol", "singularitynet", "render-token", "immutable-x", "the-sandbox",
        "decentraland", "theta-fuel", "gmx", "rocket-pool", "stacks", "tdccp", "pump-fun",
        "merlin-chain", "aethir", "kamino", "dexe", "worldcoin-wld", "metaplex", "wormhole",
        "bitdao", "celo", "civic", "coti", "district0x", "everipedia", "funfair", "hydra", "litentry", "lisk",
        "nervos-network", "numeraire", "pax-gold", "perpetual-protocol", "playdapp", "rari-governance-token"
    };

    public MainViewModel(ApiService api, DataService db, AnalysisService analysis)
    {
        _api = api;
        _db = db;
        _analysis = analysis;

        // Chart color palette
        SKColor[] palette = new[]
        {
            SKColors.DeepSkyBlue,
            SKColors.OrangeRed,
            SKColors.YellowGreen,
            SKColors.MediumPurple,
            SKColors.LightCoral,
            SKColors.Gold,
            SKColors.Cyan,
            SKColors.Violet,
            SKColors.LimeGreen,
            SKColors.IndianRed
        };

        var allCoins = _coins.Concat(_extraCoins).ToList();

        // Initialize chart series for every coin
        Series = new ObservableCollection<ISeries>(
            allCoins.Select((coin, i) =>
            {
                var color = palette[i % palette.Length];
                return new LineSeries<DateTimePoint>
                {
                    Name = $"{coin} (EUR)",
                    Tag = coin,
                    Values = new ObservableCollection<DateTimePoint>(),
                    GeometrySize = 0,
                    Stroke = new SolidColorPaint(color, 2),
                    Fill = null,
                    LineSmoothness = 0,
                    IsHoverable = false,
                    IsVisible = false
                };
            })
        );

        // Chart axes style
        var labelsPaint = new SolidColorPaint(SKColors.White);
        var separatorsPaint = new SolidColorPaint(new SKColor(255, 255, 255, 64)) { StrokeThickness = 1 };

        XAxes = new List<Axis>
        {
            new Axis
            {
                Labeler = v =>
                {
                    try
                    {
                        if (double.IsNaN(v) || double.IsInfinity(v)) return string.Empty;
                        var dt = DateTime.FromOADate(v);

                        // Adjust label format depending on the scale
                        if ((DateTime.UtcNow - dt).TotalDays > 365)
                            return dt.ToString("MMM yyyy");
                        if ((DateTime.UtcNow - dt).TotalDays > 30)
                            return dt.ToString("dd MMM");
                        return dt.ToString("HH:mm");
                    }
                    catch { return string.Empty; }
                },
                LabelsPaint = labelsPaint,
                SeparatorsPaint = separatorsPaint
            }
        };

        YAxes = new List<Axis>
        {
            new Axis
            {
                Labeler = v => $"{v:F2} €",
                LabelsPaint = labelsPaint,
                SeparatorsPaint = separatorsPaint
            }
        };

        // --- Seeding command (fetches 100k rows in chunks) ---
        SeedCommand = new Helpers.AsyncCommand(async _ =>
        {
            SeedProgress = 0;
            await SetStatusAsync("Fetching rows from CoinGecko...");
            await _db.EnsureInitializedAsync();

            try
            {
                var all = _coins.Concat(_extraCoins).ToList();
                int insertedTotal = 0;

                // Instead of one giant request, we fetch 1 month at a time, 5 years back
                var start = DateTime.UtcNow.AddYears(-5);
                var end = DateTime.UtcNow;

                var chunks = Enumerable.Range(0, (int)((end - start).TotalDays / 30) + 1)
                    .Select(i => start.AddMonths(i))
                    .TakeWhile(d => d < end)
                    .Select(d => (from: d, to: d.AddMonths(1) < end ? d.AddMonths(1) : end))
                    .ToList();

                foreach (var coin in all)
                {
                    foreach (var (from, to) in chunks)
                    {
                        if (_cts.Token.IsCancellationRequested) break;

                        // Insert monthly data into DB
                        int inserted = await _db.SeedRangeFromApiAsync(
                            _api, coin, "eur", from, to, _cts.Token);

                        insertedTotal += inserted;

                        // Update progress bar + status
                        SeedProgress = Math.Min(1, insertedTotal / 100_000.0);
                        await SetStatusAsync($"Seeding {coin}: {insertedTotal:N0} rows so far...");

                        if (insertedTotal >= 100_000) break;
                    }

                    if (insertedTotal >= 100_000) break;
                }

                // Refresh UI after seeding
                RowCount = await _db.CountAsync();
                await SetStatusAsync($"✅ DB now contains {RowCount:N0} rows total.");
                SeedProgress = 1;

                await RefreshChartAsync();
                await RefreshTopMoversAsync();

                CurrentPage = 1;
                await LoadPageAsync();
            }
            catch (OperationCanceledException)
            {
                await SetStatusAsync("⚠️ Seeding cancelled.");
            }
            catch (Exception ex)
            {
                await SetStatusAsync($"❌ Seeding error: {ex.Message}");
            }
        });

                // --- Live commands ---
        StartLiveCommand = new Helpers.AsyncCommand(async _ =>
        {
            if (_cts.IsCancellationRequested)
            {
                _cts.Dispose();
                _cts = new CancellationTokenSource();
            }

            if (_liveLoop is { IsCompleted: false }) return;

            await _db.EnsureInitializedAsync();
            await SetStatusAsync("Starting live updates (30s)...");
            _liveLoop = LiveLoopAsync(_cts.Token);
        });

        StopLiveCommand = new Helpers.AsyncCommand(async _ =>
        {
            if (!_cts.IsCancellationRequested)
            {
                await SetStatusAsync("Stopping live updates...");
                _cts.Cancel();
            }
        });


        // Paging commands
        PrevPageCommand = new Helpers.AsyncCommand(async _ =>
        {
            if (CurrentPage > 1)
            {
                CurrentPage--;
                await LoadPageAsync();
            }
        });

        NextPageCommand = new Helpers.AsyncCommand(async _ =>
        {
            if (!IsLastPage)
            {
                CurrentPage++;
                await LoadPageAsync();
            }
        });
    }

    // Loads a page of SQLite entries
    private async Task LoadPageAsync()
    {
        await _db.EnsureInitializedAsync();
        _totalRows = await _db.CountAsync();

        var pageIndex = CurrentPage - 1;
        var page = await _db.GetPageAsync(PageSize, pageIndex) ?? new List<PricePoint>();

        await RunOnUiThreadAsync(() =>
        {
            SQLiteEntries.Clear();
            foreach (var row in page) SQLiteEntries.Add(row);
        });

        OnPropertyChanged(nameof(PageInfo));
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(IsLastPage));
    }


        // Background live loop (fetches prices every 30s)
    private async Task LiveLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                await SetStatusAsync("Live: fetching latest...");
                var anyOk = false;

                foreach (var id in _coins.Take(5)) // limit to a few coins for performance
                {
                    try
                    {
                        var price = await _api.GetSimplePriceAsync(id, "eur", ct);
                        if (price > 0)
                        {
                            await _db.AppendPointAsync(id, DateTime.UtcNow, price);
                            anyOk = true;
                            await SetStatusAsync($"Live: {id} = {price} EUR @ {DateTime.Now:HH:mm:ss}");
                        }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        await SetStatusAsync($"Live: error fetching {id}: {ex.Message}");
                    }
                }

                RowCount = await _db.CountAsync();
                await RefreshChartAsync();
                await RefreshTopMoversAsync();

                if (!anyOk)
                    await SetStatusAsync("Live: last fetch failed.");
            }
        }
        catch (OperationCanceledException)
        {
            await SetStatusAsync("Live stopped.");
        }
    }


    // Updates chart series with latest data
    private async Task RefreshChartAsync()
    {
        var all = _coins.Concat(_extraCoins).ToList();

        foreach (var coin in all)
        {
            var (coinId, points) = await _db.GetLatestSeriesAsync(coin, take: 500);
            var newValues = points
                .OrderBy(p => p.Timestamp)
                .Select(p => new DateTimePoint(p.Timestamp, (double)p.Price))
                .ToList();

            await RunOnUiThreadAsync(() =>
            {
                var foundSeries = Series?.FirstOrDefault(s => (string?)s.Tag == coinId);

                if (foundSeries is LineSeries<DateTimePoint> series)
                {
                    if (newValues.Count == 0)
                    {
                        series.IsVisible = false;
                    }
                    else
                    {
                        series.IsVisible = true;
                        series.Values = new ObservableCollection<DateTimePoint>(newValues);
                    }
                }
            });
        }
    }

    // Refreshes Top Movers table
    private async Task RefreshTopMoversAsync()
    {
        var movers = await _analysis.ComputeTopMoversAsync(TimeSpan.FromHours(24));
        var top10 = movers.Take(10).ToList();

        await RunOnUiThreadAsync(() =>
        {
            TopMovers.Clear();
            foreach (var m in top10) TopMovers.Add(m);
        });
    }

    private async Task SetStatusAsync(string text)
        => await RunOnUiThreadAsync(() => Status = text);

    // Makes sure UI updates always happen on the main thread
    private static Task RunOnUiThreadAsync(Action action)
    {
        if (MainThread.IsMainThread)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource<bool>();
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try { action(); tcs.SetResult(true); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
