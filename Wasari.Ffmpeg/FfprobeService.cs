﻿using System;
using System.IO;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Buffered;

namespace Wasari.Ffmpeg;

public class FfprobeService
{
    public async Task<TimeSpan?> GetVideoDuration(string path)
    {
        if (!File.Exists(path))
            return null;

        var command = Cli.Wrap("ffprobe")
            .WithArguments(new[] { "-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 -sexagesimal", $"\"{path}\"" }, false);
            
        var commandResult = await command
            .ExecuteBufferedAsync();
            
        if (TimeSpan.TryParse(commandResult.StandardOutput, out var duration))
        {
            return duration;
        }

        return null;
    }
}