﻿using System;
using System.IO;
using System.Threading.Tasks;
using CliFx;
using CrunchyDownloader.App;
using CrunchyDownloader.Commands;
using CrunchyDownloader.Models;
using Crunchyroll.API;
using Crunchyroll.API.Models;
using Microsoft.Extensions.DependencyInjection;
using PuppeteerExtraSharp;
using PuppeteerExtraSharp.Plugins.ExtraStealth;
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
            var loggerConfiguration = new LoggerConfiguration();

            var konsoleAvailable = KonsoleSink.AvailableHeight > 10;
            loggerConfiguration = konsoleAvailable
                ? loggerConfiguration.WriteTo.Sink<KonsoleSink>()
                : loggerConfiguration.WriteTo.Console();

            Log.Logger = loggerConfiguration
                .WriteTo.File(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "CrunchyDownloader", "logs", "log.txt"), rollingInterval: RollingInterval.Day,
                    restrictedToMinimumLevel: LogEventLevel.Verbose)
                .CreateLogger();
            
            if(!konsoleAvailable)
                Log.Logger.Warning("There isn't enough space available for Konsole, falling back to regular Console sink");

            Log.Logger.Information("Setting up chrome");
            var browserFetcher = new BrowserFetcher();
            await browserFetcher.DownloadAsync();
            var extra = new PuppeteerExtra();
            extra.Use(new StealthPlugin());   
            
            await using var browser = await extra.LaunchAsync(
                new LaunchOptions
                {
                    Headless = true,
#if RELEASE
                    Args = new[] {"--no-sandbox"}
#endif
                });
            Log.Logger.Information("Chrome set up");

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(browser);
            serviceCollection.AddTransient<CrunchyRollAuthenticationService>();
            serviceCollection.AddTransient<YoutubeDlService>();
            serviceCollection.AddTransient<FfmpegService>();
            serviceCollection.AddTransient<CrunchyRollService>();
            serviceCollection.AddTransient<DownloadSeriesCommand>();
            serviceCollection.AddSingleton<YoutubeDlQueueService>();
            serviceCollection.AddSingleton<FfmpegQueueService>();
            serviceCollection.AddLogging(c => c.AddSerilog());
            serviceCollection.Configure<ProgressBarOptions>(o => { o.Enabled = true; });
            serviceCollection.AddCrunchyrollApiService();
            var serviceProvider = serviceCollection.BuildServiceProvider();

            return await new CliApplicationBuilder()
                .AddCommand<DownloadSeriesCommand>()
                .UseTypeActivator(serviceProvider.GetService)
                .Build()
                .RunAsync();
        }
    }
}