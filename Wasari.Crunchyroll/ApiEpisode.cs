﻿using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Wasari.Crunchyroll.Converters;

namespace Wasari.Crunchyroll
{
    public record ApiEpisode
    {
        [JsonPropertyName("__href__")]
        public string Href { get; init; }
        
        [JsonPropertyName("id")]
        public string Id { get; init; }

        [JsonPropertyName("title")]
        public string Title { get; init; }
        
        [JsonPropertyName("episode")]
        [JsonConverter(typeof(NullIfEmptyConverter))]
        public string Episode { get; init; }
        
        [JsonPropertyName("season_title")]
        public string SeasonTitle { get; init; }
        
        [JsonPropertyName("series_title")]
        public string SeriesTitle { get; init; }
        
        [JsonPropertyName("duration_ms")]
        public double DurationMs { get; init; }
        
        [JsonPropertyName("episode_number")]
        public int? EpisodeNumber { get; init; }
        
        [JsonPropertyName("sequence_number")]
        public decimal SequenceNumber { get; init; }
        
        [JsonPropertyName("season_number")]
        public int SeasonNumber { get; init; }
        
        [JsonPropertyName("season_id")]
        public string SeasonId { get; init; }
        
        [JsonPropertyName("images")]
        [JsonConverter(typeof(ThumbnailsConverter))]
        public string[] ThumbnailIds { get; init; }
        
        [JsonPropertyName("__links__")]
        [JsonConverter(typeof(LinksConverter))]
        public string[] Links { get; init; }

        public string StreamLink => Links.LastOrDefault(i => i.EndsWith("/streams"));

        [JsonPropertyName("is_clip")]
        public bool IsClip { get; init; }
        
        [JsonPropertyName("is_dubbed")]
        public bool IsDubbed { get; init; }
        
        [JsonPropertyName("is_subbed")]
        public bool IsSubbed { get; init; }
        
        [JsonPropertyName("is_premium_only")]
        public bool IsPremium { get; init; }
        
        [JsonPropertyName("subtitle_locales")]
        public string[] Subtitles { get; init; }

        [JsonPropertyName("audio_locale")]
        public string AudioLocale { get; set; }

        public string Locale => string.IsNullOrEmpty(AudioLocale) ? ApiEpisodeStreams?.AudioLocale : AudioLocale;
        
        public ApiEpisodeStreams ApiEpisodeStreams { get; private set; }

        public async Task LoadStreams(CrunchyrollApiService crunchyrollApiService)
        {
            if (!string.IsNullOrEmpty(StreamLink) && ApiEpisodeStreams == null)
            {
                ApiEpisodeStreams = await crunchyrollApiService.GetStreams(StreamLink);
            }
        }
    }
}