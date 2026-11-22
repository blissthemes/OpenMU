// <copyright file="MacroBehaviorAnalyzerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.Security;

using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Detects inhumanely consistent skill-combo timings (macro / AHK style).
/// </summary>
[PlugIn]
[Display(Name = "ProMU Macro Behavior Analyzer", Description = "Flags near-zero jitter between combo skill casts.")]
[Guid("B1C2D3E4-F5A6-4789-8ABC-DEF012345678")]
public sealed class MacroBehaviorAnalyzerPlugIn : IFeaturePlugIn, IAttackSpeedCheckPlugIn
{
    private const double StdDevThresholdMs = 3.0;
    private const int SeriesThreshold = 25;
    private const int SampleCapacity = 32;

    private readonly ConditionalWeakTable<Player, MacroState> _states = new();
    private readonly Func<long> _timestampProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="MacroBehaviorAnalyzerPlugIn"/> class.
    /// </summary>
    public MacroBehaviorAnalyzerPlugIn()
        : this(static () => Environment.TickCount64)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MacroBehaviorAnalyzerPlugIn"/> class.
    /// </summary>
    /// <param name="timestampProvider">Millisecond-resolution timestamp source (test seam).</param>
    internal MacroBehaviorAnalyzerPlugIn(Func<long> timestampProvider)
    {
        this._timestampProvider = timestampProvider;
    }

    /// <inheritdoc />
    public async ValueTask AttackCheatCheckAsync(Player player, Skill? skill, SpeedHackCheckEventArgs eventArgs)
    {
        if (skill is null || player.Attributes is null)
        {
            return;
        }

        var state = this._states.GetValue(player, static _ => new MacroState());
        var now = this._timestampProvider();
        long? previous;
        double? stdDev;
        int series;
        lock (state.Lock)
        {
            previous = state.LastSkillTimestamp;
            state.LastSkillTimestamp = now;
            if (previous is null)
            {
                return;
            }

            var delta = now - previous.Value;
            state.Intervals.Enqueue(delta);
            while (state.Intervals.Count > SampleCapacity)
            {
                state.Intervals.Dequeue();
            }

            if (state.Intervals.Count < SeriesThreshold)
            {
                return;
            }

            stdDev = ComputeStdDev(state.Intervals);
            series = state.Intervals.Count;
            if (stdDev < StdDevThresholdMs)
            {
                state.FlaggedSeries++;
            }
            else
            {
                state.FlaggedSeries = 0;
            }

            if (state.FlaggedSeries < SeriesThreshold)
            {
                return;
            }
        }

        if (stdDev is null)
        {
            return;
        }

        player.Logger.LogWarning(
            "Macro-like skill timing for {Player}: σ={StdDev:F2}ms over {Series} intervals",
            player.Name,
            stdDev.Value,
            series);

        SecurityViolationBus.TryPublish(new SecurityViolationEvent(
            player.Account?.LoginName ?? string.Empty,
            player.SelectedCharacter?.Name ?? player.Name,
            string.Empty,
            string.Empty,
            "Macro",
            "Soft",
            $"stdDevMs={stdDev:F2};series={series};skill={skill.Number}",
            "Flag"));

        // Soft flag only — do not drop the attack from this plugin (SpeedHackDetect still owns pacing).
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask AttackCheatCheckAsync(Player player, SpeedHackCheckEventArgs eventArgs) =>
        this.AttackCheatCheckAsync(player, null, eventArgs);

    private static double ComputeStdDev(IEnumerable<long> values)
    {
        var list = values.Select(v => (double)v).ToList();
        var mean = list.Average();
        var variance = list.Sum(v => (v - mean) * (v - mean)) / list.Count;
        return Math.Sqrt(variance);
    }

    private sealed class MacroState
    {
        public object Lock { get; } = new();

        public long? LastSkillTimestamp { get; set; }

        public Queue<long> Intervals { get; } = new();

        public int FlaggedSeries { get; set; }
    }
}
