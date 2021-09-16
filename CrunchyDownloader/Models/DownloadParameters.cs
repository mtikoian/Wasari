﻿namespace CrunchyDownloader.Models
{
    public class DownloadParameters
    {
        public string OutputDirectory { get; init; }

        public bool CreateSubdirectory { get; init; } = true;

        public string SubtitleLanguage { get; init; }

        public string CookieFilePath { get; init; }

        public bool UseX265 { get; init; } = true;

        public string ConversionPreset { get; init; } = "slow"; 

        public bool UseNvidiaAcceleration { get; init; } = true;

        public bool UseHardwareAcceleration { get; init; } = true;

        public bool DeleteTemporaryFiles { get; init; } = true;

        public string UserAgent { get; init; }
        
        public bool Subtitles { get; set; }
    }
}