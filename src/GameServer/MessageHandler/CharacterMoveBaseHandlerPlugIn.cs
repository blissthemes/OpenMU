// <copyright file="CharacterMoveBaseHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler;

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns.Security;
using MUnique.OpenMU.Network.Packets.ClientToServer;
using MUnique.OpenMU.Network.Security;
using MUnique.OpenMU.Pathfinding;

/// <summary>
/// Abstract packet handler for move packets.
/// </summary>
internal abstract class CharacterMoveBaseHandlerPlugIn : IPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public abstract byte Key { get; }

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < InstantMoveRequest.Length)
        {
            return;
        }

        // Client InstantMove is a known teleport/wallhack vector. Skill-driven moves are
        // applied server-side. Allow the client packet only for local debugger sessions
        // or when PROMU_ALLOW_INSTANT_MOVE=1.
        if (!IsInstantMoveAllowed())
        {
            InstantMoveRequest blocked = packet;
            var map = player.CurrentMap;
            if (map?.Terrain is { } terrain
                && (blocked.TargetX >= terrain.WalkMap.GetLength(0)
                    || blocked.TargetY >= terrain.WalkMap.GetLength(1)
                    || !terrain.WalkMap[blocked.TargetX, blocked.TargetY]))
            {
                SecurityViolationBus.TryPublish(new SecurityViolationEvent(
                    player.Account?.LoginName ?? string.Empty,
                    player.SelectedCharacter?.Name ?? player.Name,
                    string.Empty,
                    string.Empty,
                    "Movement",
                    "Reject",
                    $"blocked InstantMove to ({blocked.TargetX},{blocked.TargetY})",
                    "Rubberband"));
            }

            player.Logger.LogDebug("Ignored client InstantMove for {Player}", player);
            await player.MoveAsync(player.Position).ConfigureAwait(false);
            return;
        }

        InstantMoveRequest moveRequest = packet;
        var target = new Point(moveRequest.TargetX, moveRequest.TargetY);
        if (player.CurrentMap?.Terrain is { } walkTerrain
            && (target.X >= walkTerrain.WalkMap.GetLength(0)
                || target.Y >= walkTerrain.WalkMap.GetLength(1)
                || !walkTerrain.WalkMap[target.X, target.Y]))
        {
            player.Logger.LogWarning("InstantMove to non-walkable cell rejected for {Player}", player);
            await player.MoveAsync(player.Position).ConfigureAwait(false);
            return;
        }

        await player.MoveAsync(target).ConfigureAwait(false);
    }

    private static bool IsInstantMoveAllowed()
    {
        var raw = Environment.GetEnvironmentVariable("PROMU_ALLOW_INSTANT_MOVE");
        if (string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        PromuNetworkKeys.EnsureInitialized();
        return PromuNetworkKeys.Environment == NetworkKeyEnvironment.Local && Debugger.IsAttached;
    }
}