// <copyright file="PromuSecurityPackets.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.Security;

/// <summary>
/// Custom ProMU security packet codes (C1 0xFE …). Unused by stock Season 6.
/// </summary>
public static class PromuSecurityPackets
{
    /// <summary>
    /// Packet type byte shared by all ProMU security packets.
    /// </summary>
    public const byte Code = 0xFE;

    /// <summary>
    /// Server → client: session challenge (16-byte nonce).
    /// </summary>
    public const byte ChallengeSubCode = 0x01;

    /// <summary>
    /// Client → server: challenge response (32-byte HMAC).
    /// </summary>
    public const byte ChallengeResponseSubCode = 0x02;

    /// <summary>
    /// Server → client: secure heartbeat request (8-byte counter + 8-byte nonce).
    /// </summary>
    public const byte HeartbeatRequestSubCode = 0x03;

    /// <summary>
    /// Client → server: secure heartbeat response (32-byte HMAC + 4-byte integrity crc).
    /// </summary>
    public const byte HeartbeatResponseSubCode = 0x04;

    /// <summary>
    /// Client → server: hardware id report (32-byte hash).
    /// </summary>
    public const byte HwidReportSubCode = 0x05;

    /// <summary>
    /// Length of a challenge packet (header + sub + 16 nonce).
    /// </summary>
    public const int ChallengeLength = 3 + 1 + 16;

    /// <summary>
    /// Length of a challenge-response packet.
    /// </summary>
    public const int ChallengeResponseLength = 3 + 1 + 32;

    /// <summary>
    /// Length of a heartbeat request packet.
    /// </summary>
    public const int HeartbeatRequestLength = 3 + 1 + 8 + 8;

    /// <summary>
    /// Length of a heartbeat response packet.
    /// </summary>
    public const int HeartbeatResponseLength = 3 + 1 + 32 + 4;

    /// <summary>
    /// Length of an HWID report packet.
    /// </summary>
    public const int HwidReportLength = 3 + 1 + 32;
}
