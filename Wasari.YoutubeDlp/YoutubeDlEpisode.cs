﻿using System.Text.Json.Serialization;

namespace Wasari.YoutubeDlp;

public record YoutubeDlEpisode(
    [property: JsonPropertyName("episode")]
    string Title,
    [property: JsonPropertyName("episode_number")]
    int Number,
    [property: JsonPropertyName("duration")]
    double Duration,
    [property: JsonPropertyName("language")]
    string Language,
    [property: JsonPropertyName("season_number")]
    int SeasonNumber,
    [property: JsonPropertyName("season_id")]
    string SeasonId,
    [property: JsonPropertyName("requested_downloads")]
    IReadOnlyList<YoutubeDlEpisodeDownload> RequestedDownloads,
    [property: JsonPropertyName("subtitles")]
    Dictionary<string, YoutubeDlSubtitle[]> Subtitles,
    [property: JsonPropertyName("extractor_key")]
    string ExtractorKey,
    [property: JsonIgnore]
    bool WasGrouped,
    [property: JsonIgnore]
    int? AbsoluteNumber,
    [property: JsonIgnore]
    int? AbsoluteSeasonNumber);