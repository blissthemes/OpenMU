// <copyright file="PromuVipPrivilegesTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.PlugIns.Promu;
using NUnit.Framework;

/// <summary>
/// Unit tests for <see cref="PromuVipPrivileges"/>.
/// </summary>
[TestFixture]
public class PromuVipPrivilegesTests
{
    /// <summary>
    /// Daily BC/DS bonus entry is granted once per VIP account per day per type.
    /// </summary>
    [Test]
    public async ValueTask DailyBonusEntryIsIdempotentPerDayAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        GrantVip(player);
        if (player.Account is not null)
        {
            player.Account.LoginName = $"vip-{Guid.NewGuid():N}";
        }

        Assert.That(PromuVipPrivileges.TryConsumeDailyBonusEntry(player, MiniGameType.BloodCastle), Is.True);
        Assert.That(PromuVipPrivileges.TryConsumeDailyBonusEntry(player, MiniGameType.BloodCastle), Is.False);
        Assert.That(PromuVipPrivileges.TryConsumeDailyBonusEntry(player, MiniGameType.DevilSquare), Is.True);
        Assert.That(PromuVipPrivileges.TryConsumeDailyBonusEntry(player, MiniGameType.ChaosCastle), Is.False);
    }

    /// <summary>
    /// Non-VIP cannot consume the daily bonus entry.
    /// </summary>
    [Test]
    public async ValueTask NonVipCannotConsumeDailyBonusAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        if (player.Account is not null)
        {
            player.Account.LoginName = $"novip-{Guid.NewGuid():N}";
        }

        Assert.That(PromuVipPrivileges.IsVip(player), Is.False);
        Assert.That(PromuVipPrivileges.TryConsumeDailyBonusEntry(player, MiniGameType.BloodCastle), Is.False);
    }

    /// <summary>
    /// Stadium requires VIP and level 200.
    /// </summary>
    [Test]
    public async ValueTask StadiumRequiresVipAndLevelAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        Assert.That(PromuVipPrivileges.CanEnterStadium(player), Is.False);

        GrantVip(player);
        player.Attributes![Stats.Level] = 199;
        Assert.That(PromuVipPrivileges.CanEnterStadium(player), Is.False);

        player.Attributes[Stats.Level] = 200;
        Assert.That(PromuVipPrivileges.CanEnterStadium(player), Is.True);
    }

    /// <summary>
    /// Offline pickup requires VIP.
    /// </summary>
    [Test]
    public async ValueTask OfflinePickupRequiresVipAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        Assert.That(PromuVipPrivileges.CanOfflinePickup(player), Is.False);
        GrantVip(player);
        Assert.That(PromuVipPrivileges.CanOfflinePickup(player), Is.True);
    }

    private static void GrantVip(GameLogic.Player player)
        => player.Attributes!.AddElement(new SimpleElement(1, AggregateType.AddRaw), Stats.IsVip);
}
