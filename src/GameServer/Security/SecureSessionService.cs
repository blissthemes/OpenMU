// <copyright file="SecureSessionService.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.Security;

using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameServer.RemoteView;
using MUnique.OpenMU.Network;

/// <summary>
/// Issues and verifies ProMU challenge / heartbeat packets.
/// </summary>
public static class SecureSessionService
{
    private static readonly ConditionalWeakTable<Player, SecureSessionState> States = new();

    /// <summary>
    /// Gets or creates the secure session state for a player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The session state.</returns>
    public static SecureSessionState GetState(Player player) => States.GetOrCreateValue(player);

    /// <summary>
    /// Sends the initial challenge after the login window is shown.
    /// </summary>
    /// <param name="player">The remote player.</param>
    /// <param name="logger">The logger.</param>
    /// <returns>The task.</returns>
    public static async ValueTask SendChallengeAsync(RemotePlayer player, ILogger logger)
    {
        var state = GetState(player);
        var challenge = RandomNumberGenerator.GetBytes(16);
        state.PendingChallenge = challenge;
        state.ChallengeIssuedAtUtc = DateTime.UtcNow;
        state.ChallengeCompleted = false;
        state.SessionKey = null;

        if (player.Connection is not { } connection)
        {
            return;
        }

        await connection.SendAsync(() =>
        {
            var span = connection.Output.GetSpan(PromuSecurityPackets.ChallengeLength)[..PromuSecurityPackets.ChallengeLength];
            span[0] = 0xC1;
            span[1] = (byte)PromuSecurityPackets.ChallengeLength;
            span[2] = PromuSecurityPackets.Code;
            span[3] = PromuSecurityPackets.ChallengeSubCode;
            challenge.CopyTo(span[4..]);
            return PromuSecurityPackets.ChallengeLength;
        }).ConfigureAwait(false);

        logger.LogDebug("Sent ProMU secure challenge to {Player}", player);
    }

    /// <summary>
    /// Verifies a challenge response packet.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="packet">The packet.</param>
    /// <param name="logger">The logger.</param>
    /// <returns><see langword="true"/> when accepted.</returns>
    public static async ValueTask<bool> HandleChallengeResponseAsync(Player player, Memory<byte> packet, ILogger logger)
    {
        var state = GetState(player);
        if (packet.Length < PromuSecurityPackets.ChallengeResponseLength
            || state.PendingChallenge is null)
        {
            return false;
        }

        if (state.ChallengeIssuedAtUtc is { } issued
            && DateTime.UtcNow - issued > SecureSessionOptions.ResponseTimeout)
        {
            logger.LogWarning("Secure challenge timed out for {Player}", player);
            return false;
        }

        var provided = packet.Span.Slice(4, 32);
        var expected = ComputeChallengeMac(state.PendingChallenge);
        if (!CryptographicOperations.FixedTimeEquals(provided, expected))
        {
            logger.LogWarning("Secure challenge HMAC mismatch for {Player}", player);
            if (SecureSessionOptions.Enforce && player is RemotePlayer)
            {
                await player.DisconnectAsync().ConfigureAwait(false);
            }

            return false;
        }

        state.SessionKey = DeriveSessionKey(state.PendingChallenge);
        state.ChallengeCompleted = true;
        state.PendingChallenge = null;
        state.LastHeartbeatOkUtc = DateTime.UtcNow;
        logger.LogDebug("Secure challenge completed for {Player}", player);
        return true;
    }

    /// <summary>
    /// Sends a heartbeat request when the session is established.
    /// </summary>
    /// <param name="player">The remote player.</param>
    /// <returns>The task.</returns>
    public static async ValueTask SendHeartbeatRequestAsync(RemotePlayer player)
    {
        var state = GetState(player);
        if (!state.ChallengeCompleted || state.SessionKey is null || player.Connection is null)
        {
            return;
        }

        state.HeartbeatCounter++;
        var nonce = RandomNumberGenerator.GetBytes(8);
        state.PendingHeartbeatCounter = state.HeartbeatCounter;
        state.PendingHeartbeatNonce = nonce;
        state.HeartbeatIssuedAtUtc = DateTime.UtcNow;

        var counter = state.HeartbeatCounter;
        var connection = player.Connection;
        await connection.SendAsync(() =>
        {
            var span = connection.Output.GetSpan(PromuSecurityPackets.HeartbeatRequestLength)[..PromuSecurityPackets.HeartbeatRequestLength];
            span[0] = 0xC1;
            span[1] = (byte)PromuSecurityPackets.HeartbeatRequestLength;
            span[2] = PromuSecurityPackets.Code;
            span[3] = PromuSecurityPackets.HeartbeatRequestSubCode;
            BitConverter.TryWriteBytes(span[4..12], counter);
            nonce.CopyTo(span[12..20]);
            return PromuSecurityPackets.HeartbeatRequestLength;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Verifies a heartbeat response.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="packet">The packet.</param>
    /// <param name="logger">The logger.</param>
    /// <returns><see langword="true"/> when accepted.</returns>
    public static async ValueTask<bool> HandleHeartbeatResponseAsync(Player player, Memory<byte> packet, ILogger logger)
    {
        var state = GetState(player);
        if (packet.Length < PromuSecurityPackets.HeartbeatResponseLength
            || state.SessionKey is null
            || state.PendingHeartbeatNonce is null
            || state.PendingHeartbeatCounter is null)
        {
            return false;
        }

        if (state.HeartbeatIssuedAtUtc is { } issued
            && DateTime.UtcNow - issued > SecureSessionOptions.ResponseTimeout)
        {
            logger.LogWarning("Secure heartbeat timed out for {Player}", player);
            if (SecureSessionOptions.Enforce)
            {
                await player.DisconnectAsync().ConfigureAwait(false);
            }

            return false;
        }

        var mac = packet.Span.Slice(4, 32);
        var expected = ComputeHeartbeatMac(state.SessionKey, state.PendingHeartbeatCounter.Value, state.PendingHeartbeatNonce);
        if (!CryptographicOperations.FixedTimeEquals(mac, expected))
        {
            logger.LogWarning("Secure heartbeat HMAC mismatch for {Player}", player);
            if (SecureSessionOptions.Enforce)
            {
                await player.DisconnectAsync().ConfigureAwait(false);
            }

            return false;
        }

        state.LastIntegrityCrc = BitConverter.ToUInt32(packet.Span.Slice(36, 4));
        state.PendingHeartbeatCounter = null;
        state.PendingHeartbeatNonce = null;
        state.LastHeartbeatOkUtc = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Stores an HWID report.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="packet">The packet.</param>
    /// <returns><see langword="true"/> when accepted.</returns>
    public static bool HandleHwidReport(Player player, Memory<byte> packet)
    {
        if (packet.Length < PromuSecurityPackets.HwidReportLength)
        {
            return false;
        }

        var state = GetState(player);
        state.HwidHash = packet.Span.Slice(4, 32).ToArray();
        return true;
    }

    /// <summary>
    /// Disconnects players that never completed the challenge or missed heartbeats.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="logger">The logger.</param>
    /// <returns>The task.</returns>
    public static async ValueTask EnforceTimeoutsAsync(Player player, ILogger logger)
    {
        if (!SecureSessionOptions.Enforce)
        {
            return;
        }

        var state = GetState(player);
        var now = DateTime.UtcNow;
        if (!state.ChallengeCompleted
            && state.ChallengeIssuedAtUtc is { } challengeAt
            && now - challengeAt > SecureSessionOptions.ResponseTimeout)
        {
            logger.LogWarning("Disconnecting {Player}: secure challenge not completed", player);
            await player.DisconnectAsync().ConfigureAwait(false);
            return;
        }

        if (state.ChallengeCompleted
            && state.PendingHeartbeatNonce is not null
            && state.HeartbeatIssuedAtUtc is { } hbAt
            && now - hbAt > SecureSessionOptions.ResponseTimeout)
        {
            logger.LogWarning("Disconnecting {Player}: secure heartbeat not answered", player);
            await player.DisconnectAsync().ConfigureAwait(false);
        }
    }

    private static byte[] ComputeChallengeMac(byte[] challenge)
    {
        var key = Encoding.UTF8.GetBytes(SecureSessionOptions.HmacSecret);
        return HMACSHA256.HashData(key, challenge);
    }

    private static byte[] DeriveSessionKey(byte[] challenge)
    {
        var material = Encoding.UTF8.GetBytes(SecureSessionOptions.HmacSecret)
            .Concat(challenge)
            .ToArray();
        return SHA256.HashData(material);
    }

    private static byte[] ComputeHeartbeatMac(byte[] sessionKey, ulong counter, byte[] nonce)
    {
        Span<byte> payload = stackalloc byte[16];
        BitConverter.TryWriteBytes(payload[..8], counter);
        nonce.CopyTo(payload[8..]);
        return HMACSHA256.HashData(sessionKey, payload);
    }
}
