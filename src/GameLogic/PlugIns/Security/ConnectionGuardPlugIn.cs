// <copyright file="ConnectionGuardPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.Security;

using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Limits concurrent game windows per HWID and per IP.
/// </summary>
[PlugIn]
[Display(Name = "ProMU Connection Guard", Description = "Limits simultaneous windows per HWID / IP.")]
[Guid("C2D3E4F5-A6B7-4890-9BCD-EF0123456789")]
public sealed class ConnectionGuardPlugIn : IFeaturePlugIn
{
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<ushort, byte>> ByHwid = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<ushort, byte>> ByIp = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<ushort, Registration> Registrations = new();

    /// <summary>
    /// Gets the maximum simultaneous online windows per HWID.
    /// Explicit <c>PROMU_MAX_WINDOWS_PER_HWID</c> wins; else world defaults: x1=2, x10=3, x100=4.
    /// </summary>
    public static int MaxWindowsPerHwid { get; } = ResolveMaxWindows("PROMU_MAX_WINDOWS_PER_HWID");

    /// <summary>
    /// Gets the maximum simultaneous online windows per IP (same resolution as HWID).
    /// </summary>
    public static int MaxWindowsPerIp { get; } = ResolveMaxWindows("PROMU_MAX_WINDOWS_PER_IP");

    /// <summary>
    /// Registers an online player after HWID is known. Offline farming does not call this.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="hwidHashHex">The HWID hash as hex.</param>
    /// <param name="ipAddress">The IP address.</param>
    /// <returns><see langword="true"/> when the connection is allowed.</returns>
    public static async ValueTask<bool> TryRegisterOnlineAsync(Player player, string hwidHashHex, string? ipAddress)
    {
        if (player.Id == 0)
        {
            return true;
        }

        var hwidKey = string.IsNullOrWhiteSpace(hwidHashHex) ? $"account:{player.Account?.LoginName}" : hwidHashHex;
        var ipKey = string.IsNullOrWhiteSpace(ipAddress) ? "unknown" : ipAddress;

        var hwidSet = ByHwid.GetOrAdd(hwidKey, static _ => new ConcurrentDictionary<ushort, byte>());
        var ipSet = ByIp.GetOrAdd(ipKey, static _ => new ConcurrentDictionary<ushort, byte>());

        hwidSet[player.Id] = 1;
        ipSet[player.Id] = 1;
        Registrations[player.Id] = new Registration(hwidKey, ipKey);

        if (hwidSet.Count > MaxWindowsPerHwid || ipSet.Count > MaxWindowsPerIp)
        {
            player.Logger.LogWarning(
                "ConnectionGuard rejected {Player}: hwidCount={HwidCount}/{HwidMax} ipCount={IpCount}/{IpMax}",
                player.Name,
                hwidSet.Count,
                MaxWindowsPerHwid,
                ipSet.Count,
                MaxWindowsPerIp);

            SecurityViolationBus.TryPublish(new SecurityViolationEvent(
                player.Account?.LoginName ?? string.Empty,
                player.SelectedCharacter?.Name ?? player.Name,
                ipKey,
                hwidKey,
                "HwidLimit",
                "Reject",
                $"hwid={hwidSet.Count};ip={ipSet.Count}",
                "Disconnect"));

            Unregister(player.Id);
            await player.DisconnectAsync().ConfigureAwait(false);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Removes a player from the online window counters.
    /// </summary>
    /// <param name="playerId">The player id.</param>
    public static void Unregister(ushort playerId)
    {
        if (!Registrations.TryRemove(playerId, out var registration))
        {
            return;
        }

        if (ByHwid.TryGetValue(registration.HwidKey, out var hwidSet))
        {
            hwidSet.TryRemove(playerId, out _);
        }

        if (ByIp.TryGetValue(registration.IpKey, out var ipSet))
        {
            ipSet.TryRemove(playerId, out _);
        }
    }

    private static int ResolveMaxWindows(string envName)
    {
        var raw = Environment.GetEnvironmentVariable(envName);
        if (int.TryParse(raw, out var value) && value > 0)
        {
            return value;
        }

        var world = Environment.GetEnvironmentVariable("PROMU_WORLD_CODE")?.Trim().ToUpperInvariant();
        return world switch
        {
            "X1" => 2,
            "X10" => 3,
            "X100" => 4,
            _ => 3,
        };
    }

    private readonly record struct Registration(string HwidKey, string IpKey);
}
