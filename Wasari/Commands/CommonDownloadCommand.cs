﻿using System.IO;
using CliFx.Attributes;

namespace Wasari.Commands
{
    internal abstract class CommonDownloadCommand
    {
        [CommandOption("create-subdir", 'c')]
        public bool CreateSubdirectory { get; init; } = true;
        
        [CommandOption("season-folder")]
        public bool CreateSeasonFolder { get; init; } = true;

        [CommandOption("output-directory", 'o')]
        public string OutputDirectory { get; init; } = Directory.GetCurrentDirectory();

        [CommandOption("sub")]
        public bool Subtitles { get; init; } = true;

        [CommandOption("sub-language", 'l')]
        public string SubtitleLanguage { get; init; }
        
        [CommandOption("dub")]
        public bool Dubs { get; init; } = false;

        [CommandOption("dub-languages")]
        public string DubsLanguages { get; init; }

        [CommandOption("encoding-pool", 'b')]
        public int EncodingPoolSize { get; init; } = 1;

        [CommandOption("download-pool", 'd')]
        public int DownloadPoolSize { get; init; } = 2;
    }
}