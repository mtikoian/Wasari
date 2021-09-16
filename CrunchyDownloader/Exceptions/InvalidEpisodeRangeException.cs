﻿using System;

namespace CrunchyDownloader.Exceptions
{
    public sealed class InvalidEpisodeRangeException : Exception
    {
        public InvalidEpisodeRangeException() : base("Invalid episode range sent. Please use format {startingEpisode}-{finalEpisode}")
        {
        }
    }
}