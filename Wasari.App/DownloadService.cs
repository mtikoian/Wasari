﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TomLonghurst.EnumerableAsyncProcessor.Builders;
using Wasari.App.Abstractions;
using Wasari.FFmpeg;
using Wasari.YoutubeDlp;

namespace Wasari.App;

public class DownloadService
{
    public DownloadService(ILogger<DownloadService> logger, FFmpegService fFmpegService, IOptions<DownloadOptions> options, YoutubeDlpService youtubeDlpService)
    {
        Logger = logger;
        FFmpegService = fFmpegService;
        Options = options;
        YoutubeDlpService = youtubeDlpService;
    }

    private ILogger<DownloadService> Logger { get; }

    private IOptions<DownloadOptions> Options { get; }

    private FFmpegService FFmpegService { get; }

    private YoutubeDlpService YoutubeDlpService { get; }

    public Task<DownloadedEpisode[]> DownloadEpisodes(string url, int levelOfParallelism) => DownloadEpisodes(YoutubeDlpService
        .GetEpisodes(YoutubeDlpService.GetFlatPlaylist(url))
        .OrderBy(i => i.SeasonNumber)
        .ThenBy(i => i.Number), levelOfParallelism);

    public async Task<DownloadedEpisode[]> DownloadEpisodes(IAsyncEnumerable<YoutubeDlEpisode> episodes, int levelOfParallelism)
    {
        var episodesArray = await episodes
            .Where(i => Options.Value.IncludeDubs || i.Language == "ja-JP")
            .Group()
            .ToArrayAsync();

        return await AsyncProcessorBuilder.WithItems(episodesArray)
            .SelectAsync(DownloadEpisode)
            .ProcessInParallel(levelOfParallelism);
    }

    private async Task<DownloadedEpisode> DownloadEpisode(YoutubeDlEpisode episode)
    {
        var subtitleInputs = Options.Value.IncludeSubs ? episode.Subtitles
            .SelectMany(i => i.Value
                .Select(o => new WasariEpisodeInput(o.Url, i.Key, InputType.Subtitle))) : ArraySegment<WasariEpisodeInput>.Empty;

        var inputs = episode.RequestedDownloads
            .Select(i => new WasariEpisodeInput(i.Url, i.Language, string.IsNullOrEmpty(i.Vcodec) ? InputType.Audio : InputType.Video))
            .Concat(subtitleInputs)
            .Cast<IWasariEpisodeInput>()
            .ToArray();

        var episodeName = $"S{episode.SeasonNumber:00}E{episode.Number:00}";
        var fileName = $"{episodeName} - {episode.Title}.mkv".AsSafePath();
        var filepath = string.IsNullOrEmpty(Options.Value.OutputDirectory) ? fileName : Path.Combine(Options.Value.OutputDirectory, fileName);
        var wasariEpisode = new WasariEpisode(episode.Title, episode.SeasonNumber, 1, inputs, TimeSpan.FromSeconds(episode.Duration));
        
        if (Options.Value.SkipExistingFiles && File.Exists(filepath))
        {
            Logger.LogWarning("Skipping episode since it already exists: {Path}", filepath);
            return new DownloadedEpisode(filepath, false, wasariEpisode);
        }

        var episodeProgress = new Progress<double>();
        var lastValue = double.MinValue;

        episodeProgress.ProgressChanged += (sender, d) =>
        {
            var delta = d - lastValue;

            if (delta > 0.01)
            {
                Logger.LogInformation("Encoding update {@Episode} {Percentage:p}", episodeName, d);
                lastValue = d;
            }
        };

       
        await FFmpegService.DownloadEpisode(wasariEpisode, filepath, episodeProgress);
        return new DownloadedEpisode(filepath, true, wasariEpisode);
    }
}