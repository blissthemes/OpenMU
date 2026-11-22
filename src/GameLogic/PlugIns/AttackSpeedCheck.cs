// <copyright file="AttackSpeedCheck.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using System;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel.Configuration;

/// <summary>
/// Evaluates the attack pace of a player with a token bucket.
/// </summary>
internal sealed class AttackSpeedCheck
{
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AttackSpeedCheck"/> class.
    /// </summary>
    /// <param name="timeProvider">The time provider.</param>
    public AttackSpeedCheck(TimeProvider timeProvider)
    {
        this._timeProvider = timeProvider;
    }

    /// <summary>
    /// Evaluates an attack and returns the severity to record, if any.
    /// </summary>
    /// <param name="player">The attacking player.</param>
    /// <param name="config">The configuration.</param>
    /// <param name="state">The player state.</param>
    /// <param name="skill">The used skill, or <c>null</c> for a regular hit.</param>
    /// <returns>The severity to record, or <see cref="SpeedHackViolationSeverity.None"/>.</returns>
    public SpeedHackViolationSeverity Evaluate(
        Player player,
        SpeedHackDetectConfiguration config,
        SpeedHackState state,
        Skill? skill)
    {
        var attackLimit = AttackSpeedModel.Resolve(config, player, skill);
        var now = this._timeProvider.GetTimestamp();

        var minIntervalMs = attackLimit.IntervalMs;
        bool shouldRecordViolation = false;
        double elapsedMs = 0;
        double remainingTokens = 0;

        lock (state.Lock)
        {
            if (state.LastAttackTokenUpdateTimestamp is null)
            {
                state.LastAttackTokenUpdateTimestamp = now;
                state.AttackTokens = config.MaxAttackTokens - 1.0;
                return SpeedHackViolationSeverity.None;
            }

            elapsedMs = Math.Max(0, this._timeProvider.GetElapsedTime(state.LastAttackTokenUpdateTimestamp.Value, now).TotalMilliseconds);
            state.LastAttackTokenUpdateTimestamp = now;

            var regen = elapsedMs / minIntervalMs;
            state.AttackTokens = Math.Min(config.MaxAttackTokens, state.AttackTokens + regen);

            if (state.AttackTokens >= 1.0)
            {
                state.AttackTokens -= 1.0;
                return SpeedHackViolationSeverity.None;
            }

            remainingTokens = state.AttackTokens;
            shouldRecordViolation = !config.ObserveOnly;

            if (shouldRecordViolation)
            {
                state.RecentAttackViolations.Enqueue(now);
                var window = TimeSpan.FromSeconds(Math.Max(1, config.AttackViolationKickWindowSeconds));
                while (state.RecentAttackViolations.Count > 0
                       && this._timeProvider.GetElapsedTime(state.RecentAttackViolations.Peek(), now) > window)
                {
                    state.RecentAttackViolations.Dequeue();
                }
            }
        }

        player.Logger.LogDebug(
            "Speedhack detected on attack for player {0}: {1:F0}ms since the last attack, expected at least {2:F0}ms ({3} {4:F0}, skill {5}), {6:F2} attack tokens left.{7}",
            player.Name,
            elapsedMs,
            minIntervalMs,
            attackLimit.SpeedSource,
            attackLimit.Speed,
            skill?.Number.ToString() ?? "melee",
            remainingTokens,
            config.ObserveOnly ? " Observe-only, the attack is not blocked." : string.Empty);

        if (!shouldRecordViolation)
        {
            return SpeedHackViolationSeverity.None;
        }

        // Sustained attack floods within a short window escalate to Reject (kick path).
        lock (state.Lock)
        {
            if (state.RecentAttackViolations.Count >= Math.Max(1, config.AttackViolationKickThreshold))
            {
                return SpeedHackViolationSeverity.Reject;
            }
        }

        return SpeedHackViolationSeverity.Soft;
    }
}
