using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wasari.Abstractions;
using Wasari.Abstractions.Extensions;
using Wasari.App;
using Wasari.Crunchyroll;
using Wasari.Crunchyroll.API;
using Wasari.Exceptions;
using Wasari.Ffmpeg;
using Wasari.Models;
using WasariEnvironment;

namespace Wasari.Commands
{
    [Command("crunchy")]
    internal class CrunchyrollDownloadSeriesCommand : CommonDownloadCommand, ICommand
    {
        public CrunchyrollDownloadSeriesCommand(
            CrunchyRollAuthenticationService crunchyRollAuthenticationService,
            ILogger<CrunchyrollDownloadSeriesCommand> logger,
            EnvironmentService environmentService,
            BrowserFactory browserFactory,
            CrunchyrollApiServiceFactory crunchyrollApiServiceFactory, IServiceProvider serviceProvider, DownloadSeriesService downloadSeriesService)
        {
            CrunchyRollAuthenticationService = crunchyRollAuthenticationService;
            Logger = logger;
            EnvironmentService = environmentService;
            BrowserFactory = browserFactory;
            CrunchyrollApiServiceFactory = crunchyrollApiServiceFactory;
            ServiceProvider = serviceProvider;
            DownloadSeriesService = downloadSeriesService;
        }

        private CrunchyRollAuthenticationService CrunchyRollAuthenticationService { get; }

        [CommandParameter(0, Description = "Series URL.")]
        public string SeriesUrl { get; init; }

        [CommandOption("username", 'u', Description = "Crunchyroll username.")]
        public string Username { get; init; }

        [CommandOption("password", 'p', Description = "Crunchyroll password.")]
        public string Password { get; init; }

        [CommandOption("episodes", 'e', Description = "Episodes range (eg. 1-5 would be episode 1 to 5)")]
        public string EpisodeRange { get; init; }

        [CommandOption("seasons", 's', Description = "Seasons range (eg. 1-3 would be seasons 1 to 5)")]
        public string SeasonsRange { get; init; }

        [CommandOption("preset", Description = "Conversion preset passed down to FFmpeg while encoding")]
        public string ConversionPreset { get; init; }

        [CommandOption("clean", Description = "Clean temporary files (Subtitles and Raw video file)")]
        public bool CleanTemporaryFiles { get; init; } = true;

        [CommandOption("hevc", Description = "Encode final video file in H265/HEVC")]
        public bool ConvertToHevc { get; init; } = true;

        [CommandOption("gpuaccel", Description = "Use GPU acceleration for FFmpeg encoding")]
        public bool GpuAcceleration { get; init; } = true;

        [CommandOption("temp-dir", 't', Description = "The directory for temporary files")]
        public string TemporaryDirectory { get; init; } = Path.Combine(Path.GetTempPath(), "Wasari");

        [CommandOption("skip-existing-episodes", Description = "Skip download from existing episodes")]
        public bool SkipExistingEpisodes { get; init; } = true;

        [CommandOption("anime-4k", Description = "Uses Anime4K to upscale final video file to 4K")]
        public bool UseAnime4K { get; init; } = false;

        [CommandOption("headless", Description = "Chromium headless mode")]
        public bool Headless { get; init; } = true;

        [CommandOption("format", 'f', Description = "Format passed to yt-dlp")]
        public string Format { get; init; } = "best";
        
        [CommandOption("mask", 'm', Description = "Final episode file mask. Available parameters are: 0: Season and episode prefix (S00E00). 1: Safe episode title")]
        public string FileMask { get; init; } = "{0} - {1}";

        private ILogger<CrunchyrollDownloadSeriesCommand> Logger { get; }

        private EnvironmentService EnvironmentService { get; }
        
        private IServiceProvider ServiceProvider { get; }

        private BrowserFactory BrowserFactory { get; }
        
        private DownloadSeriesService DownloadSeriesService { get; }

        private CrunchyrollApiServiceFactory CrunchyrollApiServiceFactory { get; }

        public async ValueTask ExecuteAsync(IConsole console)
        {
            BrowserFactory.Headless = Headless;
            EnvironmentService.ThrowIfFeatureNotAvailable(EnvironmentFeatureType.Ffmpeg, EnvironmentFeatureType.YtDlp);

            var stopwatch = Stopwatch.StartNew();
            var isValidSeriesUrl = IsValidSeriesUrl();
            if (!isValidSeriesUrl)
                throw new CommandException("The URL provided doesnt seem to be a crunchyroll SERIES page URL.");
            
            var isBeta = SeriesUrl.Contains("beta.");

            if (isBeta)
            {
                Logger.LogInformation("BETA Series detected");
            }

            if ((!string.IsNullOrEmpty(Username) || !string.IsNullOrEmpty(Password)) && isBeta)
            {
                if (string.IsNullOrEmpty(Username))
                    throw new CrunchyrollAuthenticationException("Missing username", Username, Password);

                if (string.IsNullOrEmpty(Password))
                    throw new CrunchyrollAuthenticationException("Missing password", Username, Password);

                await CrunchyrollApiServiceFactory.CreateAuthenticatedService(Username, Password);
            }
            else
            {
                await CrunchyrollApiServiceFactory.CreateUnauthenticatedService();
            }
            
            using var cookieFile = isBeta ? null : await CreateCookiesFile();
            var downloadParameters = await CreateDownloadParameters(cookieFile);
            await DownloadSeriesService.DownloadEpisodes(new Uri(SeriesUrl), downloadParameters);
            await BrowserFactory.DisposeAsync();
            
            if (cookieFile != null)
            {
                Logger.LogDebug("Cleaning cookie file {@CookieFile}", cookieFile);
                cookieFile?.Dispose();
            }
            
            stopwatch.Stop();
            Logger.LogInformation("Completed. Time Elapsed {@TimeElapsed}", stopwatch.Elapsed);
        }

        private async Task<TemporaryCookieFile> CreateCookiesFile()
        {
            if (string.IsNullOrEmpty(Username) && string.IsNullOrEmpty(Password)) return null;

            if (string.IsNullOrEmpty(Username))
                throw new CrunchyrollAuthenticationException("Missing username", Username, Password);

            if (string.IsNullOrEmpty(Password))
                throw new CrunchyrollAuthenticationException("Missing password", Username, Password);

            var cookies = await CrunchyRollAuthenticationService.GetCookies(Username, Password);
            var cookieFileName = Path.GetTempFileName();
            await File.WriteAllTextAsync(cookieFileName, cookies);

            Logger.LogInformation("Cookie file written to {@FilePath}", cookieFileName);

            return new TemporaryCookieFile { Path = cookieFileName };
        }

        private bool IsValidSeriesUrl()
        {
            if (Uri.TryCreate(SeriesUrl, UriKind.Absolute, out var parsedUri))
            {
                var crunchyHost =
                    parsedUri.Host.EndsWith("crunchyroll.com", StringComparison.InvariantCultureIgnoreCase);
                
                if (parsedUri.Host == "beta.crunchyroll.com")
                    return (SeriesUrl?.Contains("/series/") ?? false) || (SeriesUrl?.Contains("/watch/") ?? false);

                return crunchyHost && (!SeriesUrl?.Contains("/episode-") ?? false);
            }

            return true;
        }

        private async Task<DownloadParameters> CreateDownloadParameters(TemporaryCookieFile file)
        {
            var isNvidiaAvailable = GpuAcceleration
                                    && await ServiceProvider.GetService<FfmpegService>()!.IsNvidiaAvailable()
                                    && EnvironmentService.IsFeatureAvailable(EnvironmentFeatureType.NvidiaGpu);

            if (isNvidiaAvailable) Logger.LogInformation("NVIDIA hardware acceleration is available");

            var anime4K = UseAnime4K;
            if (anime4K)
            {
                if (!isNvidiaAvailable)
                {
                    Logger.LogWarning("Anime 4K was requested but no NVIDIA card is available");
                    anime4K = false;
                }

                if (!EnvironmentService.IsFeatureAvailable(EnvironmentFeatureType.FfmpegLibPlacebo))
                {
                    Logger.LogWarning("Anime 4K was requested but no FFmpeg with libplacebo is available");
                    anime4K = false;
                }
            }
            
            return new DownloadParameters
            {
                CookieFilePath = file?.Path,
                SubtitleLanguage = SubtitleLanguage?.Split(","),
                Subtitles = !string.IsNullOrEmpty(SubtitleLanguage) || Subtitles,
                BaseOutputDirectory = OutputDirectory,
                CreateSeasonFolder = CreateSeasonFolder,
                UseNvidiaAcceleration = isNvidiaAvailable,
                ConversionPreset = ConversionPreset,
                DeleteTemporaryFiles = CleanTemporaryFiles,
                UseHevc = ConvertToHevc,
                TemporaryDirectory = TemporaryDirectory,
                ParallelDownloads = DownloadPoolSize,
                ParallelMerging = EncodingPoolSize,
                UseAnime4K = anime4K,
                Format = Format,
                FileMask = FileMask,
                CreateSeriesFolder = CreateSubdirectory,
                SkipExistingEpisodes = SkipExistingEpisodes,
                DownloadPoolSize = DownloadPoolSize,
                EncodingPoolSize = EncodingPoolSize,
                EpisodeRange = EpisodeRange,
                SeasonRange = SeasonsRange,
                Dubs = !string.IsNullOrEmpty(DubsLanguages) || Dubs,
                DubsLanguage = DubsLanguages?.Split(",")
            };
        }

       
    }
}