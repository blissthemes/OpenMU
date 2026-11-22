// <copyright file="SecureSessionPacketHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.Security;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns.Security;
using MUnique.OpenMU.GameServer.MessageHandler;
using MUnique.OpenMU.GameServer.RemoteView;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Handles ProMU security packets (C1 0xFE).
/// </summary>
[PlugIn]
[Display(Name = "ProMU Secure Session", Description = "Challenge-response and secure heartbeat handlers.")]
[Guid("6E2C1A90-4B7D-4F11-9C3A-8D5E0F1A2B3C")]
public sealed class SecureSessionPacketHandlerPlugIn : IPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public byte Key => PromuSecurityPackets.Code;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < 4)
        {
            return;
        }

        var logger = player.Logger;
        var subCode = packet.Span[3];
        switch (subCode)
        {
            case PromuSecurityPackets.ChallengeResponseSubCode:
                await SecureSessionService.HandleChallengeResponseAsync(player, packet, logger).ConfigureAwait(false);
                break;
            case PromuSecurityPackets.HeartbeatResponseSubCode:
                await SecureSessionService.HandleHeartbeatResponseAsync(player, packet, logger).ConfigureAwait(false);
                break;
            case PromuSecurityPackets.HwidReportSubCode:
                if (SecureSessionService.HandleHwidReport(player, packet))
                {
                    var state = SecureSessionService.GetState(player);
                    var hwidHex = state.HwidHash is null ? string.Empty : Convert.ToHexString(state.HwidHash);
                    var ip = player is RemotePlayer remote ? remote.IpAddress : null;
                    await ConnectionGuardPlugIn.TryRegisterOnlineAsync(player, hwidHex, ip).ConfigureAwait(false);
                }

                break;
            default:
                logger.LogDebug("Unknown ProMU security sub-code {SubCode} from {Player}", subCode, player);
                break;
        }
    }
}
