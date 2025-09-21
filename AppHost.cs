using System;

namespace CryptoPulse
{
    /// <summary>
    /// Global hook to MAUI's IServiceProvider.
    /// Call AppHost.Initialize(app.Services) in MauiProgram after builder.Build().
    /// </summary>
    public static class AppHost
    {
        public static IServiceProvider? Services { get; private set; }

        public static void Initialize(IServiceProvider services) => Services = services;
    }
}
