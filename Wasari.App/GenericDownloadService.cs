using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TomLonghurst.EnumerableAsyncProcessor.Builders;
using Wasari.App.Abstractions;
using Wasari.App.Extensions;
using Wasari.FFmpeg;
using Wasari.YoutubeDlp;

namespace Wasari.App;

public class GenericDownloadService : IDownloadService
{
    public GenericDownloadService(ILogger<GenericDownloadService> logger, FFmpegService fFmpegService, IOptions<DownloadOptions> options, YoutubeDlpService youtubeDlpService)
    {
        Logger = logger;
        FFmpegService = fFmpegService;
        Options = options;
        YoutubeDlpService = youtubeDlpService;
    }

    protected ILogger<GenericDownloadService> Logger { get; }

    private IOptions<DownloadOptions> Options { get; }

    private FFmpegService FFmpegService { get; }

    private YoutubeDlpService YoutubeDlpService { get; }

    public virtual Task<DownloadedEpisode[]> DownloadEpisodes(string url, int levelOfParallelism) => DownloadEpisodes(YoutubeDlpService.GetPlaylist(url)
        .OrderBy(i => i.SeasonNumber)
        .ThenBy(i => i.Number), levelOfParallelism);

    protected async Task<DownloadedEpisode[]> DownloadEpisodes(IAsyncEnumerable<WasariEpisode> episodes, int levelOfParallelism)
    {
        var episodesArray = await episodes
            .FilterEpisodes(Options.Value.EpisodesRange, Options.Value.SeasonsRange)
            .ToArrayAsync();
        
        Logger.LogInformation("{@DownloadCount} episodes gathered to download", episodesArray.Length);

        return await AsyncProcessorBuilder.WithItems(episodesArray)
            .SelectAsync(DownloadEpisode)
            .ProcessInParallel(levelOfParallelism);
    }

    private async Task<DownloadedEpisode> DownloadEpisode(WasariEpisode episode)
    {
        var outputDirectory = Options.Value.OutputDirectory ?? Environment.CurrentDirectory;

        if (Options.Value.CreateSeriesFolder && !string.IsNullOrEmpty(episode.SeriesName))
        {
            outputDirectory = Path.Combine(outputDirectory, episode.SeriesName.AsSafePath());

            if (!Directory.Exists(outputDirectory))
                Directory.CreateDirectory(outputDirectory);
        }
        
        if (Options.Value.CreateSeasonFolder && episode.SeasonNumber.HasValue)
        {
            outputDirectory = Path.Combine(outputDirectory, $"Season {episode.SeasonNumber}");

            if (!Directory.Exists(outputDirectory))
                Directory.CreateDirectory(outputDirectory);
        }

        var episodeNameBuilder = BuildEpisodeName(episode);
        var fileName = episodeNameBuilder.ToString().AsSafePath();
        var filepath = Path.Combine(outputDirectory, fileName);

        if (Options.Value.SkipExistingFiles && File.Exists(filepath))
        {
            Logger.LogWarning("Skipping episode since it already exists: {Path}", filepath);
            return new DownloadedEpisode(filepath, false, episode);
        }

        var episodeProgress = new Progress<FFmpegProgressUpdate>();
        var lastValue = double.MinValue;

        episodeProgress.ProgressChanged += (_, d) =>
        {
            var delta = d.Progress - lastValue;

            if (delta > 0.01 || d.Progress >= 1d)
            {
                Logger.LogInformation("Encoding update {Path} {Percentage:p} {Speed}x", filepath, d.Progress, d.Speed);
                lastValue = d.Progress;
            }
        };


        await FFmpegService.DownloadEpisode(episode, filepath, episodeProgress);
        return new DownloadedEpisode(filepath, true, episode);
    }

    private static StringBuilder BuildEpisodeName(IWasariEpisode episode)
    {
        var episodeNameBuilder = new StringBuilder();

        if (episode.SeasonNumber.HasValue) episodeNameBuilder.Append($"S{episode.SeasonNumber:00}");

        if (episode.Number.HasValue) episodeNameBuilder.Append($"E{episode.Number:00}");

        if (episode.Number.HasValue || episode.SeasonNumber.HasValue)
            episodeNameBuilder.Append(" - ");
        episodeNameBuilder.Append(episode.Title);
        episodeNameBuilder.Append(".mkv");
        return episodeNameBuilder;
    }
}