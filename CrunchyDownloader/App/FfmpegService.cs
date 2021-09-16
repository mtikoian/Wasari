﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.EventStream;
using CrunchyDownloader.Models;
using Microsoft.Extensions.Logging;

namespace CrunchyDownloader.App
{
    public class FfmpegService
    {
        public FfmpegService(ILogger<FfmpegService> logger)
        {
            Logger = logger;
        }

        private ILogger<FfmpegService> Logger { get; }

        private static async IAsyncEnumerable<string> GetAvailableHardwareAccelerationMethods()
        {
            var arguments = new[]
            {
                "-hide_banner -hwaccels"
            };

            var command = Cli.Wrap("ffmpeg")
                .WithValidation(CommandResultValidation.None)
                .WithArguments(arguments, false);

            await foreach (var commandEvent in command.ListenAsync())
            {
                if (commandEvent is StandardOutputCommandEvent standardOutputCommandEvent)
                {
                    yield return standardOutputCommandEvent.Text;
                }
            }
        }

        private static async IAsyncEnumerable<string> GetAvailableEncoders()
        {
            var arguments = new[]
            {
                "-hide_banner -encoders"
            };

            var command = Cli.Wrap("ffmpeg")
                .WithValidation(CommandResultValidation.None)
                .WithArguments(arguments, false);

            await foreach (var commandEvent in command.ListenAsync())
            {
                if (commandEvent is StandardOutputCommandEvent standardOutputCommandEvent)
                {
                    yield return standardOutputCommandEvent.Text;
                }
            }
        }

        public static async Task<bool> IsNvidiaAvailable() =>
            await GetAvailableHardwareAccelerationMethods()
                .AnyAsync(i => string.Equals(i, "cuda", StringComparison.InvariantCultureIgnoreCase))
            && await GetAvailableEncoders()
                .AnyAsync(i => i.Contains("hevc_nvenc", StringComparison.InvariantCultureIgnoreCase));

        public async Task MergeSubsToVideo(string videoFile, string[] subtitlesFiles, string newVideoFile,
            DownloadParameters downloadParameters)
        {
            subtitlesFiles = subtitlesFiles?.OrderBy(i => i).ToArray();

            var aggregate = subtitlesFiles?
                .Select(i => $"-f ass -i \"{i}\"")
                .Aggregate((x, y) => $"{x} {y}");

            var mappings = subtitlesFiles?
                .Select((s, i) => $"-map {i + 1}")
                .Aggregate((x, y) => $"{x} {y}");

            var metadata = subtitlesFiles?
                .Select((i, index) =>
                    $"-metadata:s:s:{index} language={i.Split(".").Reverse().Skip(1).First()}")
                .ToArray();

            var metadataMappings = metadata?
                .Aggregate((x, y) => $"{x} {y}");

            var subtitleArguments = subtitlesFiles != null && subtitlesFiles.Any()
                ? $"{aggregate} -map 0 {mappings} {metadataMappings}"
                : null;

            var arguments = new[]
                {
                    downloadParameters.UseHardwareAcceleration
                        ? $"-hwaccel {(downloadParameters.UseNvidiaAcceleration ? "cuda" : "auto")}"
                        : null,
                    $"-i \"{videoFile}\"",
                    $"{subtitleArguments}",
                    downloadParameters.UseX265
                        ? downloadParameters.UseNvidiaAcceleration ? "-c:v hevc_nvenc" : "-c:v libx265"
                        : "-c:v copy",
                    string.IsNullOrEmpty(downloadParameters.ConversionPreset)
                        ? null
                        : $"-preset {downloadParameters.ConversionPreset}",
                    "-c:a copy",
                    "-scodec copy",
                    $"\"{newVideoFile}\""
                }
                .Where(i => !string.IsNullOrEmpty(i))
                .ToArray();

            var command = Cli.Wrap("ffmpeg")
                .WithArguments(arguments, false);

            Logger.LogDebug(
                "Merging video file with subtitles. {@Command} {@OriginalVideoFile} {@Subtitles} {@NewVideoFile}",
                command.ToString(), videoFile, subtitlesFiles, newVideoFile);

            var stopwatch = Stopwatch.StartNew();
            await command.ExecuteAsync();
            stopwatch.Stop();

            Logger.LogDebug("Merging {@OriginalVideoFile} with {@Subtitles} to {@NewVideoFile} took {@Elapsed}",
                videoFile, subtitlesFiles, newVideoFile, stopwatch.Elapsed);
        }
    }
}