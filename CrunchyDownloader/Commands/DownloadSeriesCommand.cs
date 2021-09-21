using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using CrunchyDownloader.App;
using CrunchyDownloader.Exceptions;
using CrunchyDownloader.Extensions;
using CrunchyDownloader.Models;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;

namespace CrunchyDownloader.Commands
{
    [Command("series")]
    public class DownloadSeriesCommand : CrunchyAuthenticatedCommand, ICommand
    {
        public DownloadSeriesCommand(YoutubeDlService youtubeDlService, CrunchyRollService crunchyRollService,
            CrunchyRollAuthenticationService crunchyRollAuthenticationService, Browser browser,
            ILogger<DownloadSeriesCommand> logger) : base(
            crunchyRollAuthenticationService)
        {
            YoutubeDlService = youtubeDlService;
            CrunchyRollService = crunchyRollService;
            Browser = browser;
            Logger = logger;
        }

        [CommandParameter(0, Description = "Series URL.")]
        public string SeriesUrl { get; init; }

        [CommandOption("create-subdir", 'c')]
        public bool CreateSubdirectory { get; init; } = true;

        [CommandOption("output-directory", 'o')]
        public string OutputDirectory { get; init; } = Directory.GetCurrentDirectory();

        [CommandOption("subtitles", 's')]
        public bool Subtitles { get; init; } = true;

        [CommandOption("sub-language", 'l')]
        public string SubtitleLanguage { get; init; }

        [CommandOption("batch", 'b')]
        public int EpisodeBatchSize { get; init; } = 5;

        [CommandOption("episodes", 'e')]
        public string EpisodeRange { get; init; }

        [CommandOption("preset")]
        public string ConversionPreset { get; init; }

        [CommandOption("clean")]
        public bool CleanTemporaryFiles { get; init; } = true;

        [CommandOption("hevc")]
        public bool ConvertToHevc { get; init; } = false;

        [CommandOption("haccel", 'a')]
        public bool HardwareAcceleration { get; init; } = true;

        [CommandOption("gpuaccel")]
        public bool GpuAcceleration { get; init; } = true;

        [CommandOption("temp-dir", 't')]
        public string TemporaryDirectory { get; init; } = Path.GetTempPath();

        private YoutubeDlService YoutubeDlService { get; }

        private CrunchyRollService CrunchyRollService { get; }

        private ILogger<DownloadSeriesCommand> Logger { get; }

        private Browser Browser { get; }

        public async ValueTask ExecuteAsync(IConsole console)
        {
            using var cookieFile = await CreateCookiesFile();

            var episodes = await CrunchyRollService
                .GetEpisodes(SeriesUrl)
                .Where(i => i.SeasonInfo?.Title == null ||
                            !i.SeasonInfo.Title.Contains("Dub", StringComparison.InvariantCultureIgnoreCase))
                .OrderBy(i => i.SeasonInfo.Season)
                .ThenBy(i => i.Number)
                .ToArrayAsync();

            var episodeRange = ParseEpisodeRange(EpisodeRange, episodes.Length);
            
            Logger.LogInformation("Episodes range is {@Range}", episodeRange);
            
            episodes = episodes.Where(i => i.Number >= episodeRange[0] && i.Number <= episodeRange[1]).ToArray();

            var userAgent = await Browser.GetUserAgentAsync();
            await Browser.DisposeAsync();

            var isNvidiaAvailable = GpuAcceleration && await FfmpegService.IsNvidiaAvailable();

            if (isNvidiaAvailable) Logger.LogInformation("NVIDIA hardware acceleration is available");

            var downloadParameters = new DownloadParameters
            {
                CookieFilePath = cookieFile.Path,
                CreateSubdirectory = CreateSubdirectory,
                SubtitleLanguage = SubtitleLanguage,
                Subtitles = !string.IsNullOrEmpty(SubtitleLanguage) || Subtitles,
                OutputDirectory = OutputDirectory,
                UserAgent = userAgent,
                UseNvidiaAcceleration = isNvidiaAvailable,
                UseHardwareAcceleration = HardwareAcceleration,
                ConversionPreset = ConversionPreset,
                DeleteTemporaryFiles = CleanTemporaryFiles,
                UseX265 = ConvertToHevc
            };

            foreach (var episodesBatch in episodes.Batch(EpisodeBatchSize))
            {
                var stopwatch = Stopwatch.StartNew();
                var tasks = episodesBatch
                    .Select(episodeInfo => YoutubeDlService.DownloadEpisode(episodeInfo, downloadParameters))
                    .ToArray();
                await Task.WhenAll(tasks);
                stopwatch.Stop();

                Logger.LogDebug("Batch took {@TotalRunTime} to run", stopwatch.Elapsed);
            }

            Logger.LogDebug("Cleaning cookie file {@CookieFile}", cookieFile);
            cookieFile.Dispose();

            Logger.LogInformation("Completed");
        }

        private static int[] ParseEpisodeRange(string episodeRange, int numberOfEpisodes)
        {
            if (string.IsNullOrEmpty(episodeRange))
                return new[] { 1, numberOfEpisodes };

            if (episodeRange.Any(i => !char.IsDigit(i) && i != '-'))
                throw new InvalidEpisodeRangeException();

            if (episodeRange.Contains('-'))
            {
                var episodesNumbers = episodeRange.Split('-');

                if (episodesNumbers.Length != 2 || episodesNumbers.All(string.IsNullOrEmpty))
                    throw new InvalidEpisodeRangeException();

                if (episodesNumbers.All(i => !string.IsNullOrEmpty(i)))
                    return episodesNumbers.Select(int.Parse).ToArray();

                if (string.IsNullOrEmpty(episodesNumbers[0]))
                    return new[] { 1, int.Parse(episodesNumbers[1]) };

                if (string.IsNullOrEmpty(episodesNumbers[1]))
                    return new[] { int.Parse(episodesNumbers[0]), numberOfEpisodes };
            }

            return new[] { numberOfEpisodes, numberOfEpisodes };
        }
    }
}