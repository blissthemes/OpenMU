// <copyright file="ConnectChallengeHandler.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.ConnectServer.Security;

using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network;

/// <summary>
/// Lightweight ConnectServer challenge: after Hello, the server may send C1 FE 01;
/// clients that answer incorrectly are disconnected when enforcement is on.
/// </summary>
internal static class ConnectChallengeHandler
{
    /// <summary>
    /// Packet type for ProMU connect challenges.
    /// </summary>
    public const byte Code = 0xFE;

    /// <summary>
    /// Challenge sub-code (server → client).
    /// </summary>
    public const byte ChallengeSubCode = 0x01;

    /// <summary>
    /// Challenge response sub-code (client → server).
    /// </summary>
    public const byte ResponseSubCode = 0x02;

    private static readonly ConditionalWeakTable<IConnection, ChallengeState> States = new();

    /// <summary>
    /// Gets a value indicating whether connect challenges are enforced.
    /// </summary>
    public static bool Enforce
    {
        get
        {
            var raw = Environment.GetEnvironmentVariable("PROMU_CONNECT_CHALLENGE");
            if (string.Equals(raw, "off", StringComparison.OrdinalIgnoreCase)
                || string.Equals(raw, "0", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(raw, "on", StringComparison.OrdinalIgnoreCase)
                || string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(raw, "enforce", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Default: off for local lab (stock clients / tooling still work against CS).
            return false;
        }
    }

    /// <summary>
    /// Sends a connect challenge to the client when enforcement is enabled.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="logger">The logger.</param>
    /// <returns>The task.</returns>
    public static async ValueTask SendChallengeAsync(IConnection connection, ILogger logger)
    {
        if (!Enforce)
        {
            return;
        }

        var nonce = RandomNumberGenerator.GetBytes(16);
        var state = States.GetOrCreateValue(connection);
        state.Nonce = nonce;
        state.IssuedAtUtc = DateTime.UtcNow;
        state.Completed = false;

        await connection.SendAsync(() =>
        {
            const int length = 20;
            var span = connection.Output.GetSpan(length)[..length];
            span[0] = 0xC1;
            span[1] = length;
            span[2] = Code;
            span[3] = ChallengeSubCode;
            nonce.CopyTo(span[4..]);
            return length;
        }).ConfigureAwait(false);

        logger.LogDebug("Sent ConnectServer ProMU challenge");
    }

    /// <summary>
    /// Tries to handle a challenge response. Returns false when the packet is not a response.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="packet">The packet.</param>
    /// <param name="logger">The logger.</param>
    /// <returns><see langword="true"/> when the packet was a (valid or invalid) challenge response.</returns>
    public static async ValueTask<bool> TryHandleResponseAsync(IConnection connection, Memory<byte> packet, ILogger logger)
    {
        if (!Enforce || packet.Length < 36 || packet.Span[2] != Code || packet.Span[3] != ResponseSubCode)
        {
            return false;
        }

        if (!States.TryGetValue(connection, out var state) || state.Nonce is null)
        {
            await connection.DisconnectAsync().ConfigureAwait(false);
            return true;
        }

        var secret = Encoding.UTF8.GetBytes(
            Environment.GetEnvironmentVariable("PROMU_SESSION_HMAC_SECRET")
            ?? "promu-local-session-hmac-not-for-prod");
        var expected = HMACSHA256.HashData(secret, state.Nonce);
        var ok = CryptographicOperations.FixedTimeEquals(expected, packet.Span.Slice(4, 32));
        state.Completed = ok;
        if (!ok)
        {
            logger.LogWarning("ConnectServer ProMU challenge failed; disconnecting");
            await connection.DisconnectAsync().ConfigureAwait(false);
        }

        return true;
    }

    private sealed class ChallengeState
    {
        public byte[]? Nonce { get; set; }

        public DateTime IssuedAtUtc { get; set; }

        public bool Completed { get; set; }
    }
}
