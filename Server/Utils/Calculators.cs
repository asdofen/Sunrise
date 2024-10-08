using System.Runtime.InteropServices;
using osu.Shared;
using RosuPP;
using Sunrise.Server.Application;
using Sunrise.Server.Database;
using Sunrise.Server.Database.Models;
using Sunrise.Server.Helpers;
using Sunrise.Server.Managers;
using Sunrise.Server.Objects;
using Beatmap = RosuPP.Beatmap;
using Mods = osu.Shared.Mods;

namespace Sunrise.Server.Utils;

public static class Calculators
{
    public static double CalculatePerformancePoints(Session session, Score score)
    {
        var beatmapBytes = BeatmapManager.GetBeatmapFile(session, score.BeatmapId).Result;

        if (beatmapBytes == null) return 0;

        var bytesPointer = new Sliceu8(GCHandle.Alloc(beatmapBytes, GCHandleType.Pinned), (uint)beatmapBytes.Length);
        var beatmap = Beatmap.FromBytes(bytesPointer);

        beatmap.Convert((Mode)score.GameMode);

        var result = GetUserPerformance(score).Calculate(beatmap.Context);

        return result.mode switch
        {
            Mode.Osu => result.osu.ToNullable()!.Value.pp,
            Mode.Taiko => result.taiko.ToNullable()!.Value.pp,
            Mode.Catch => result.fruit.ToNullable()!.Value.pp,
            Mode.Mania => result.mania.ToNullable()!.Value.pp,
            _ => 0
        };
    }

    public static async Task<double> RecalcuteBeatmapDifficulty(Session session, int beatmapId, int mode,
        Mods mods = Mods.None)
    {
        var beatmapBytes = await BeatmapManager.GetBeatmapFile(session, beatmapId);

        if (beatmapBytes == null) return -1;

        var bytesPointer = new Sliceu8(GCHandle.Alloc(beatmapBytes, GCHandleType.Pinned), (uint)beatmapBytes.Length);
        var beatmap = Beatmap.FromBytes(bytesPointer);

        beatmap.Convert((Mode)mode);

        var difficulty = Difficulty.New();
        difficulty.IMods((uint)mods);
        var result = difficulty.Calculate(beatmap.Context);

        return result.mode switch
        {
            Mode.Osu => result.osu.ToNullable()!.Value.stars,
            Mode.Taiko => result.taiko.ToNullable()!.Value.stars,
            Mode.Catch => result.fruit.ToNullable()!.Value.stars,
            Mode.Mania => result.mania.ToNullable()!.Value.stars,
            _ => -1
        };
    }

    public static async Task<(double, double, double, double)> CalculatePerformancePoints(Session session,
        int beatmapId, int mode, Mods mods = Mods.None)
    {
        var beatmapBytes = await BeatmapManager.GetBeatmapFile(session, beatmapId);

        if (beatmapBytes == null) return (0, 0, 0, 0);

        var bytesPointer = new Sliceu8(GCHandle.Alloc(beatmapBytes, GCHandleType.Pinned), (uint)beatmapBytes.Length);
        var beatmap = Beatmap.FromBytes(bytesPointer);

        beatmap.Convert((Mode)mode);

        var ppList = new List<double>();

        var accuracyCalculate = new List<double>
        {
            100,
            99,
            98,
            95
        };

        foreach (var accuracy in accuracyCalculate)
        {
            var performance = Performance.New();
            performance.Accuracy((uint)accuracy);
            performance.IMods((uint)mods);
            var result = performance.Calculate(beatmap.Context);
            ppList.Add(result.mode switch
            {
                Mode.Osu => result.osu.ToNullable()!.Value.pp,
                Mode.Taiko => result.taiko.ToNullable()!.Value.pp,
                Mode.Catch => result.fruit.ToNullable()!.Value.pp,
                Mode.Mania => result.mania.ToNullable()!.Value.pp,
                _ => 0
            });
        }

        return (ppList[0], ppList[1], ppList[2], ppList[3]);
    }


    public static async Task<double> CalculateUserWeightedAccuracy(int userId, GameMode mode, Score? score = null)
    {
        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();

        // Get users top scores sorted by pp in descending order
        var userBests = await database.GetUserBestScores(userId, mode, score?.BeatmapId ?? 0);
        if (score != null)
            userBests.Add(score);

        if (userBests.Count == 0) return 0;

        var top100Scores = userBests.Take(100).ToList();

        // Sorting again because we previously added a new score
        top100Scores = top100Scores.GetSortedScoresByPP(false);

        var weightedAccuracy = top100Scores
            .Select((s, i) => Math.Pow(0.95, i) * s.Accuracy)
            .Sum();
        var bonusAccuracy = 100 / (20 * (1 - Math.Pow(0.95, userBests.Count)));

        return weightedAccuracy * bonusAccuracy / 100;
    }

    public static async Task<double> CalculateUserWeightedPerformance(int userId, GameMode mode, Score? score = null)
    {
        var database = ServicesProviderHolder.GetRequiredService<SunriseDb>();

        // Get users top scores sorted by pp in descending order
        var userBests = await database.GetUserBestScores(userId, mode, score?.BeatmapId ?? 0);
        if (score != null)
            userBests.Add(score);

        if (userBests.Count == 0) return 0;

        var top100Scores = userBests.Take(100).ToList();

        // Sorting again because we previously added a new score
        top100Scores = top100Scores.GetSortedScoresByPP(false);

        const double bonusNumber = 416.6667;
        var weightedPp = top100Scores
            .Select((s, i) => Math.Pow(0.95, i) * s.PerformancePoints)
            .Sum();
        var bonusPp = bonusNumber * (1 - Math.Pow(0.9994, userBests.Count));

        return weightedPp + bonusPp;
    }

    public static float CalculateAccuracy(Score score)
    {
        var totalHits = score.Count300 + score.Count100 + score.Count50 + score.CountMiss;

        if (score.GameMode == GameMode.Mania) totalHits += score.CountGeki + score.CountKatu;

        if (totalHits == 0) return 0;

        return score.GameMode switch
        {
            GameMode.Standard => (float)(score.Count300 * 300 + score.Count100 * 100 + score.Count50 * 50) /
                (totalHits * 300) * 100,
            GameMode.Taiko => (float)(score.Count300 * 300 + score.Count100 * 150) / (totalHits * 300) * 100,
            GameMode.CatchTheBeat => (float)(score.Count300 + score.Count100 + score.Count50) / totalHits * 100,
            GameMode.Mania => (float)((score.Count300 + score.CountGeki) * 300 + score.CountKatu * 200 +
                                      score.Count100 * 100 + score.Count50 * 50) / (totalHits * 300) * 100,
            _ => 0
        };
    }

    private static Performance GetUserPerformance(Score score)
    {
        var performance = Performance.New();
        performance.Accuracy((uint)score.Accuracy);
        performance.Combo((uint)score.MaxCombo);
        performance.N300((uint)score.Count300);
        performance.N100((uint)score.Count100);
        performance.N50((uint)score.Count50);
        performance.Misses((uint)score.CountMiss);
        performance.IMods((uint)score.Mods);
        return performance;
    }
}