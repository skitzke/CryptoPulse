using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;
using LiveChartsCore.SkiaSharpView.Maui;
using Microsoft.Extensions.DependencyInjection;
using CryptoPulse.Services;
using CryptoPulse.ViewModels;
using DotNetEnv;

namespace CryptoPulse
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            // Try to load the .env file so we can pull in API keys locally
            try
            {
                string[] possibleEnvPaths =
                {
                    Path.Combine(AppContext.BaseDirectory, ".env"),          
                    Path.Combine(Directory.GetCurrentDirectory(), ".env"),   
                };

                // Pick the first .env file we find
                string? foundPath = possibleEnvPaths.FirstOrDefault(File.Exists);
                if (foundPath != null)
                {
                    Env.Load(foundPath);
                    var apiKey = Environment.GetEnvironmentVariable("COINGECKO_API_KEY");

                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        Console.WriteLine($"[BOOT] Loaded .env from {foundPath}");
                        Console.WriteLine($"[BOOT] COINGECKO_API_KEY length={apiKey.Length}");
                        Console.WriteLine($"[BOOT] Preview: {apiKey.Substring(0, 6)}******");
                    }
                    else
                    {
                        Console.WriteLine($"[BOOT] Loaded .env from {foundPath} | COINGECKO_API_KEY not set");
                    }
                }
                else
                {
                    Console.WriteLine("[BOOT] No .env file found in expected locations.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BOOT] Failed to load .env: {ex.Message}");
            }

            // Standard Maui app builder setup
            var builder = MauiApp.CreateBuilder();

            builder
                .UseMauiApp<App>()
                .UseSkiaSharp()  // needed for chart rendering
                .UseLiveCharts() // hook LiveCharts into MAUI
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
            // Log output in debug builds
            builder.Logging.AddDebug();
#endif

            // Register services for dependency injection
            builder.Services.AddSingleton<DataService>();
            builder.Services.AddSingleton<AnalysisService>();

            // Register ApiService with HTTP client (inject API key if available)
            builder.Services.AddHttpClient<ApiService>((sp, client) =>
            {
                var apiKey = Environment.GetEnvironmentVariable("COINGECKO_API_KEY");

                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    client.BaseAddress = new Uri("https://pro-api.coingecko.com/api/v3/");

                    // Add key header for pro access
                    client.DefaultRequestHeaders.Remove("x-cg-pro-api-key");
                    client.DefaultRequestHeaders.TryAddWithoutValidation("x-cg-pro-api-key", apiKey);

                    Console.WriteLine("[BOOT] Using CoinGecko PRO API with key.");
                    Console.WriteLine($"[BOOT] Header preview: {apiKey.Substring(0, 6)}******");

                    // Debug headers just to verify
                    foreach (var h in client.DefaultRequestHeaders)
                        Console.WriteLine($"[BOOT] Header: {h.Key} = {string.Join(",", h.Value)}");
                }
                else
                {
                    // Fallback to free API
                    client.BaseAddress = new Uri("https://api.coingecko.com/api/v3/");
                    Console.WriteLine("[BOOT] Using CoinGecko FREE API (no key).");
                }
            });

            // ViewModels
            builder.Services.AddTransient<MainViewModel>();

            // Pages
            builder.Services.AddSingleton<MainPage>();

            // Build the app and initialize our DI container
            var app = builder.Build();
            AppHost.Initialize(app.Services);

            Console.WriteLine("[BOOT] DI container initialized successfully.");
            return app;
        }
    }
}
