using System.Runtime.InteropServices;
using osu.Shared;
using RosuPP;
using Sunrise.Server.Objects.Models;
using Sunrise.Server.Services;
using Beatmap = RosuPP.Beatmap;

namespace Sunrise.Server.Utils;

public class Calculators(ServicesProvider services)
{
    public double CalculatePerformancePoints(Score score)
    {
        var beatmapBytes = new BeatmapService(services).GetBeatmapFileById(score.BeatmapId).Result;

        if (beatmapBytes == null)
        {
            return 0;
        }

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

    public async Task<(double, double, double)> CalculatePerformancePoints(int beatmapId, int mode)
    {
        var beatmapBytes = await new BeatmapService(services).GetBeatmapFileById(beatmapId);

        if (beatmapBytes == null)
        {
            return (0, 0, 0);
        }

        var bytesPointer = new Sliceu8(GCHandle.Alloc(beatmapBytes, GCHandleType.Pinned), (uint)beatmapBytes.Length);
        var beatmap = Beatmap.FromBytes(bytesPointer);

        beatmap.Convert((Mode)mode);

        var ppList = new List<double>();

        var accuracyCalculate = new List<double>
        {
            100,
            98,
            95
        };

        foreach (var accuracy in accuracyCalculate)
        {
            var performance = Performance.New();
            performance.Accuracy((uint)accuracy);
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

        return (ppList[0], ppList[1], ppList[2]);
    }


    public async Task<double> CalculateUserWeightedAccuracy(Score score)
    {
        var userBests = await services.Database.GetUserBestScores(score.UserId, score.GameMode, score.BeatmapId);
        userBests.Add(score);

        var weightedAccuracy = userBests
            .Select((s, i) => Math.Pow(0.95, i) * s.Accuracy)
            .Sum();
        var bonusAccuracy = 100 / (20 * (1 - Math.Pow(0.95, userBests.Count)));

        return weightedAccuracy * bonusAccuracy / 100;
    }

    public async Task<double> CalculateUserWeightedPerformance(Score score)
    {
        var userBests = await services.Database.GetUserBestScores(score.UserId, score.GameMode, score.BeatmapId);
        userBests.Add(score);

        var bonusNumber = 416.6667;
        var weightedPp = userBests
            .Select((s, i) => Math.Pow(0.95, i) * s.PerformancePoints)
            .Sum();
        var bonusPp = bonusNumber * (1 - Math.Pow(0.9994, userBests.Count));

        return weightedPp + bonusPp;
    }


    public float CalculateAccuracy(Score score)
    {
        var totalHits = score.Count300 + score.Count100 + score.Count50 + score.CountMiss;

        if (score.GameMode == GameMode.Mania)
        {
            totalHits += score.CountGeki + score.CountKatu;
        }

        if (totalHits == 0)
        {
            return 0;
        }

        return score.GameMode switch
        {
            GameMode.Standard => (float)(score.Count300 * 300 + score.Count100 * 100 + score.Count50 * 50) / (totalHits * 300) * 100,
            GameMode.Taiko => (float)(score.Count300 * 300 + score.Count100 * 150) / (totalHits * 300) * 100,
            GameMode.CatchTheBeat => (float)(score.Count300 + score.Count100 + score.Count50) / totalHits * 100,
            GameMode.Mania => (float)((score.Count300 + score.CountGeki) * 300 + score.CountKatu * 200 + score.Count100 * 100 + score.Count50 * 50) / (totalHits * 300) * 100,
            _ => 0
        };
    }

    private Performance GetUserPerformance(Score score)
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