// <copyright file="PromuAntiCheatSecurityTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Collections.Concurrent;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.PlugIns.Security;

/// <summary>
/// Deterministic ProMU anti-cheat simulations (NUnit; OpenMU does not use xUnit).
/// Covers: legal attack pacing with ping buffer, speedhack x1.5, wall-teleport walk math,
/// and parallel trade/web mutation serialization via <see cref="AccountTransactionLock"/>.
/// </summary>
[TestFixture]
public class PromuAntiCheatSecurityTests
{
    /// <summary>
    /// Legal melee pacing at ~200 ms RTT-equivalent buffer must not raise Soft/Reject.
    /// </summary>
    [Test]
    public async Task AttackSpeed_NormalPaceWithPingBuffer_DoesNotViolate()
    {
        var time = new ManualTimeProvider();
        var plugin = new SpeedHackDetectPlugIn(time)
        {
            Configuration = CreateEnforceConfig(pingBufferPercent: 0.15),
        };
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var args = new SpeedHackCheckEventArgs();

        // Prime token bucket.
        await plugin.AttackCheatCheckAsync(player, null, args).ConfigureAwait(false);
        Assert.That(args.Severity, Is.EqualTo(SpeedHackViolationSeverity.None));

        var intervalMs = AttackSpeedModel.Resolve(plugin.Configuration!, player, null).IntervalMs;
        // Advance slightly slower than the buffered minimum (honest client + jitter).
        for (var i = 0; i < 40; i++)
        {
            time.Advance(TimeSpan.FromMilliseconds(intervalMs + 5));
            args = new SpeedHackCheckEventArgs();
            await plugin.AttackCheatCheckAsync(player, null, args).ConfigureAwait(false);
            Assert.That(args.Severity, Is.EqualTo(SpeedHackViolationSeverity.None), $"iteration {i}, interval={intervalMs:F1}ms");
        }
    }

    /// <summary>
    /// Sustained 1.5x attack rate must escalate Soft → Reject under enforce config.
    /// </summary>
    [Test]
    public async Task AttackSpeed_SpeedHackTimesOnePointFive_Rejects()
    {
        var time = new ManualTimeProvider();
        var plugin = new SpeedHackDetectPlugIn(time)
        {
            Configuration = CreateEnforceConfig(
                pingBufferPercent: 0.15,
                kickThreshold: 5,
                kickWindowSeconds: 5,
                maxAttackTokens: 1),
        };
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var args = new SpeedHackCheckEventArgs();
        await plugin.AttackCheatCheckAsync(player, null, args).ConfigureAwait(false);

        var intervalMs = AttackSpeedModel.Resolve(plugin.Configuration!, player, null).IntervalMs;
        var hackInterval = intervalMs / 1.5;

        var sawSoft = false;
        var sawReject = false;
        for (var i = 0; i < 40; i++)
        {
            // Only advance by the hacked interval — extra padding regenerates tokens and masks the flood.
            time.Advance(TimeSpan.FromMilliseconds(hackInterval));
            args = new SpeedHackCheckEventArgs();
            await plugin.AttackCheatCheckAsync(player, null, args).ConfigureAwait(false);
            if (args.Severity is SpeedHackViolationSeverity.Soft)
            {
                sawSoft = true;
            }

            if (args.Severity is SpeedHackViolationSeverity.Reject)
            {
                sawReject = true;
                break;
            }
        }

        Assert.Multiple(() =>
        {
            Assert.That(sawSoft, Is.True, "expected Soft violations before kick");
            Assert.That(sawReject, Is.True, "expected Reject after sustained x1.5 flood");
        });
    }

    /// <summary>
    /// Instant +5-tile wall poke (teleport-through-wall) exceeds walk budget → violation.
    /// </summary>
    [Test]
    public void WalkSpeed_WallTeleportFiveTiles_IsViolation()
    {
        // One “step” costing 5 tiles at normal delay, almost no wall-clock time.
        var costMs = WalkSpeedValidator.GetExpectedTimeMs(5.0, WalkSpeedValidator.NormalStepDelayMs);
        var verdict = WalkSpeedValidator.Evaluate(costMs, windowMs: 50, toleranceMs: 100);
        Assert.That(verdict.IsViolation, Is.True, $"cost={verdict.ExpectedTimeMs:F0} budget={verdict.BudgetMs:F0}");
    }

    /// <summary>
    /// Server-side teleport / skill move reset path: empty window must not violate.
    /// </summary>
    [Test]
    public void WalkSpeed_AfterServerTeleportReset_NoViolation()
    {
        var verdict = WalkSpeedValidator.Evaluate(windowCostMs: 0, windowMs: 15_000, toleranceMs: 500);
        Assert.That(verdict.IsViolation, Is.False);
    }

    /// <summary>
    /// Trade mutation and website reward path must serialize on the same account id.
    /// </summary>
    [Test]
    public async Task TransactionLock_ParallelTradeAndWebReward_Serialize()
    {
        var accountId = Guid.NewGuid();
        var log = new ConcurrentQueue<string>();
        var tradeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var webTried = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var trade = Task.Run(async () =>
        {
            await AccountTransactionLock.RunAsync(accountId, async () =>
            {
                log.Enqueue("trade-enter");
                tradeStarted.TrySetResult();
                await webTried.Task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                await Task.Delay(80).ConfigureAwait(false);
                log.Enqueue("trade-exit");
            }).ConfigureAwait(false);
        });

        await tradeStarted.Task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

        var web = Task.Run(async () =>
        {
            webTried.TrySetResult();
            await AccountTransactionLock.RunAsync(accountId, () =>
            {
                log.Enqueue("web-enter");
                log.Enqueue("web-exit");
                return ValueTask.CompletedTask;
            }).ConfigureAwait(false);
        });

        await Task.WhenAll(trade, web).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        Assert.That(log.ToArray(), Is.EqualTo(new[] { "trade-enter", "trade-exit", "web-enter", "web-exit" }));
    }

    /// <summary>
    /// Near-zero jitter skill series must soft-flag Macro via SecurityViolationBus.
    /// </summary>
    [Test]
    public async Task MacroAnalyzer_FixedIntervalSeries_PublishesMacroFlag()
    {
        SecurityViolationEvent? published = null;
        SecurityViolationBus.PublishAsync = e =>
        {
            published = e;
            return ValueTask.CompletedTask;
        };

        try
        {
            // Deterministic clock: Windows TickCount64 resolution (~15ms) makes wall-clock spin flaky.
            long clock = 0;
            var plugin = new MacroBehaviorAnalyzerPlugIn(() => clock);
            var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
            Assert.That(player.Attributes, Is.Not.Null, "macro analyzer requires Attributes");
            var skill = new Skill { Number = 19, Name = "Twisting Slash" };
            var args = new SpeedHackCheckEventArgs();

            for (var i = 0; i < 60; i++)
            {
                clock += 16;
                await plugin.AttackCheatCheckAsync(player, skill, args).ConfigureAwait(false);
            }

            Assert.That(published, Is.Not.Null);
            Assert.That(published!.Category, Is.EqualTo("Macro"));
        }
        finally
        {
            SecurityViolationBus.PublishAsync = null;
        }
    }

    /// <summary>
    /// Fourth concurrent window on the same HWID must be rejected.
    /// </summary>
    [Test]
    public async Task ConnectionGuard_FourthWindowSameHwid_Rejected()
    {
        var hwid = Convert.ToHexString(Guid.NewGuid().ToByteArray());
        var players = new List<Player>();
        for (var i = 0; i < 4; i++)
        {
            players.Add(await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false));
        }

        for (ushort i = 0; i < players.Count; i++)
        {
            players[i].Id = (ushort)(1000 + i);
        }

        try
        {
            Assert.That(await ConnectionGuardPlugIn.TryRegisterOnlineAsync(players[0], hwid, "127.0.0.1").ConfigureAwait(false), Is.True);
            Assert.That(await ConnectionGuardPlugIn.TryRegisterOnlineAsync(players[1], hwid, "127.0.0.1").ConfigureAwait(false), Is.True);
            Assert.That(await ConnectionGuardPlugIn.TryRegisterOnlineAsync(players[2], hwid, "127.0.0.1").ConfigureAwait(false), Is.True);
            Assert.That(await ConnectionGuardPlugIn.TryRegisterOnlineAsync(players[3], hwid, "127.0.0.1").ConfigureAwait(false), Is.False);
        }
        finally
        {
            foreach (var p in players)
            {
                ConnectionGuardPlugIn.Unregister(p.Id);
            }
        }
    }

    /// <summary>
    /// Ping buffer 15% shortens the enforced minimum interval (more tolerant to lag).
    /// </summary>
    [Test]
    public void AttackSpeedModel_PingBuffer_ShortensInterval()
    {
        var config = new SpeedHackDetectConfiguration { PingBufferPercent = 0.15 };
        var buffered = AttackSpeedModel.ApplyPingBuffer(config, 200);
        Assert.That(buffered, Is.EqualTo(170).Within(0.01));
    }

    private static SpeedHackDetectConfiguration CreateEnforceConfig(
        double pingBufferPercent,
        int kickThreshold = 10,
        int kickWindowSeconds = 5,
        double maxAttackTokens = 2) =>
        new()
        {
            ObserveOnly = false,
            DisconnectOnViolation = true,
            EnforceWalkSpeed = true,
            PingBufferPercent = pingBufferPercent,
            AttackViolationKickThreshold = kickThreshold,
            AttackViolationKickWindowSeconds = kickWindowSeconds,
            AlertDebounceSeconds = 0,
            MaxAttackTokens = maxAttackTokens,
            AttackSpeedBaseDelayMs = 450,
            AttackSpeedScalingFactor = 0,
            AttackSpeedMinIntervalMs = 200,
        };

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => 10_000_000;

        public override long GetTimestamp() => this._timestamp;

        public override DateTimeOffset GetUtcNow() =>
            DateTimeOffset.UnixEpoch + TimeSpan.FromTicks(this._timestamp);

        public void Advance(TimeSpan delta) =>
            this._timestamp += (long)(delta.TotalSeconds * this.TimestampFrequency);
    }
}
