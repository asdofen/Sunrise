﻿using osu.Shared;
using Sunrise.Server.Application;
using Sunrise.Server.Database.Models;
using Sunrise.Server.Managers;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.Serializable;
using Sunrise.Server.Types.Enums;

namespace Sunrise.Server.Helpers;

public static class SubmitScoreHelper
{
    private const string MetricsError = "Score {0} by {1} ({2}) rejected with reason: {3}";

    public static string GetNewFirstPlaceString(Session session, Score score, BeatmapSet beatmapSet, Beatmap beatmap)
    {
        return
            $"[https://osu.{Configuration.Domain}/user/{score.UserId} {session.User.Username}] achieved #1 on [{beatmap.Url.Replace("ppy.sh", Configuration.Domain)} {beatmapSet.Artist} - {beatmapSet.Title} [{beatmap.Version}]] with {score.Accuracy:0.00}% accuracy for {score.PerformancePoints:0.00}pp!";
    }

    public static void ReportRejectionToMetrics(Session session, Score score, string reason)
    {
        var message = string.Format(MetricsError, score.ScoreHash, session.User.Username, session.User.Id, reason);
        SunriseMetrics.RequestReturnedErrorCounterInc(RequestType.OsuSubmitScore, null, message);
    }

    public static bool IsScoreValid(Session session, Score score, string osuVersion, string clientHash,
        string beatmapHash, string onlineBeatmapHash, string? storyboardHash)
    {
        var userOsuVersion = session.Attributes.OsuVersion?.Split(".")[0] ?? "";
        var computedOnlineHash = score.ComputeOnlineHash(session.User.Username, clientHash, storyboardHash);

        return new[]
        {
            string.Equals($"b{osuVersion}", userOsuVersion, StringComparison.Ordinal),
            string.Equals(clientHash, session.Attributes.UserHash, StringComparison.Ordinal),
            string.Equals(score.ScoreHash, computedOnlineHash, StringComparison.Ordinal),
            string.Equals(beatmapHash, onlineBeatmapHash,
                StringComparison
                    .Ordinal) // Since we got beatmap from client hash, this is not really needed. But just for obscure cases.
        }.All(x => x);
    }

    public static async Task<string> GetScoreSubmitResponse(Beatmap beatmap, UserStats user, UserStats prevUser,
        Score newScore,
        Score? prevScore)
    {
        var userUrl = $"https://{Configuration.Domain}/user/{user.Id}";

        // TODO: Change playcount and passcount to be from out db
        var beatmapInfo =
            $"beatmapId:{beatmap.Id}|beatmapSetId:{beatmap.BeatmapsetId}|beatmapPlaycount:{beatmap.Playcount}|beatmapPasscount:{beatmap.Passcount}|approvedDate:{beatmap.LastUpdated:yyyy-MM-dd}";
        var beatmapRanking = $"chartId:beatmap|chartUrl:{beatmap.Url}|chartName:Beatmap Ranking";
        var scoreInfo = string.Join("|", GetChart(prevScore, newScore));
        var playerInfo = $"chartId:overall|chartUrl:{userUrl}|chartName:Overall Ranking|" +
                         string.Join("|", GetChart(prevUser, user));

        var newAchievements = await MedalManager.GetNewMedals(newScore, beatmap, user);

        return
            $"{beatmapInfo}\n{beatmapRanking}|{scoreInfo}|onlineScoreId:{newScore.Id}\n{playerInfo}|achievements-new:{newAchievements}";
    }

    public static bool IsHasInvalidMods(Mods mods)
    {
        // TODO: Support Relax and SCV2 at some point.
        return mods.HasFlag(Mods.Relax) || mods.HasFlag(Mods.Autoplay) || mods.HasFlag(Mods.Target) ||
               mods.HasFlag(Mods.ScoreV2);
    }

    public static int GetTimeElapsed(Score score, int scoreTime, int scoreFailTime)
    {
        var isPassed = score.IsPassed || score.Mods.HasFlag(Mods.NoFail);
        return isPassed ? scoreTime : scoreFailTime;
    }

    public static bool IsScoreFailed(Score score)
    {
        return !score.IsPassed && !score.Mods.HasFlag(Mods.NoFail);
    }

    private static List<string> GetChart<T>(T before, T after)
    {
        string[] chartEntries =
        [
            "Rank",
            "RankedScore",
            "TotalScore",
            "MaxCombo",
            "Accuracy",
            "Pp"
        ];

        var result = new List<string>();

        foreach (var entry in chartEntries)
        {
            var lowerFirst = char.ToLower(entry[0]) + entry[1..];

            var obj = entry switch
            {
                "RankedScore" => typeof(T) == typeof(Score) ? "TotalScore" : "RankedScore",
                "Rank" => typeof(T) == typeof(Score) ? "LeaderboardRank" : "Rank",
                "Pp" => "PerformancePoints",
                _ => entry
            };

            result.Add(GetChartEntry(lowerFirst, before?.GetType().GetProperty(obj)?.GetValue(before),
                after?.GetType().GetProperty(obj)?.GetValue(after)));
        }

        return result;
    }

    private static string GetChartEntry(string name, object? before, object? after)
    {
        return $"{name}Before:{before?.ToString() ?? string.Empty}|{name}After:{after?.ToString() ?? string.Empty}";
    }
}