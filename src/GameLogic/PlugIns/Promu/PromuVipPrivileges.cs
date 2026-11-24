// <copyright file="PromuVipPrivileges.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.Promu;

using System.Collections.Concurrent;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// Shared ProMU VIP privilege checks (server-side only).
/// </summary>
public static class PromuVipPrivileges
{
    /// <summary>Stadium map number (client warp name "Stadium" → Arena).</summary>
    public const byte StadiumMapNumber = 6;

    /// <summary>Minimum character level to enter Stadium as VIP.</summary>
    public const int StadiumMinimumLevel = 200;

    private static readonly ConcurrentDictionary<string, byte> DailyBonusEntries = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Determines whether the player currently has an active VIP flag.
    /// </summary>
    public static bool IsVip(Player player)
        => (player.Attributes?[Stats.IsVip] ?? 0) >= 1;

    /// <summary>
    /// Offline leveling item pickup is VIP-only.
    /// </summary>
    public static bool CanOfflinePickup(Player player) => IsVip(player);

    /// <summary>
    /// Stadium (Arena map 6) requires VIP and level ≥ 200.
    /// </summary>
    public static bool CanEnterStadium(Player player)
    {
        if (!IsVip(player))
        {
            return false;
        }

        var level = player.Attributes?[Stats.Level] ?? 0;
        return level >= StadiumMinimumLevel;
    }

    /// <summary>
    /// Tries to consume the VIP daily bonus entry for Blood Castle / Devil Square (+1 without ticket).
    /// </summary>
    /// <returns><c>true</c> if the bonus entry was granted; otherwise <c>false</c>.</returns>
    public static bool TryConsumeDailyBonusEntry(Player player, MiniGameType miniGameType)
    {
        if (miniGameType is not (MiniGameType.BloodCastle or MiniGameType.DevilSquare))
        {
            return false;
        }

        if (!IsVip(player) || player.Account?.LoginName is not { } login)
        {
            return false;
        }

        var key = $"{login}|{DateTime.UtcNow:yyyyMMdd}|{miniGameType}";
        return DailyBonusEntries.TryAdd(key, 1);
    }
}
