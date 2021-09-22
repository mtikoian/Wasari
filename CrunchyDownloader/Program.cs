﻿using System;
using System.IO;
using System.Threading.Tasks;
using CliFx;
using CrunchyDownloader.App;
using CrunchyDownloader.Commands;
using Microsoft.Extensions.DependencyInjection;
using PuppeteerSharp;
using Serilog;
using Serilog.Events;

namespace CrunchyDownloader
{
    internal static class Program
    {
        private static async Task<int> Main()
        {
            Console.CursorVisible = false;
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Sink<KonsoleSink>(LogEventLevel.Information)
                .WriteTo.File(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "CrunchyDownloader", "logs", "log.txt"), rollingInterval: RollingInterval.Day, restrictedToMinimumLevel: LogEventLevel.Verbose)
                .CreateLogger();

            var browserFetcher = new BrowserFetcher();
            await browserFetcher.DownloadAsync();
            await using var browser = await Puppeteer.LaunchAsync(
                new LaunchOptions
                {
                    Headless = false,
#if RELEASE
                    Args = new[] {"--no-sandbox"}
#endif
                });
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(browser);
            serviceCollection.AddTransient<CrunchyRollAuthenticationService>();
            serviceCollection.AddTransient<YoutubeDlService>();
            serviceCollection.AddTransient<FfmpegService>();
            serviceCollection.AddTransient<CrunchyRollService>();
            serviceCollection.AddTransient<DownloadSeriesCommand>();
            serviceCollection.AddLogging(c => c.AddSerilog());
            serviceCollection.AddSingleton<DownloadProgressManager>();
            var serviceProvider = serviceCollection.BuildServiceProvider();

            return await new CliApplicationBuilder()
                .AddCommand<DownloadSeriesCommand>()
                .UseTypeActivator(serviceProvider.GetService)
                .Build()
                .RunAsync();
        }
    }
}