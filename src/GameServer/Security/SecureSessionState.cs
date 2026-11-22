// <copyright file="SecureSessionState.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.Security;

/// <summary>
/// Per-connection secure session state.
/// </summary>
public sealed class SecureSessionState
{
    /// <summary>
    /// Gets or sets the random session key established after a successful challenge.
    /// </summary>
    public byte[]? SessionKey { get; set; }

    /// <summary>
    /// Gets or sets the pending challenge nonce.
    /// </summary>
    public byte[]? PendingChallenge { get; set; }

    /// <summary>
    /// Gets or sets when the pending challenge was issued (UTC).
    /// </summary>
    public DateTime? ChallengeIssuedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the challenge was answered correctly.
    /// </summary>
    public bool ChallengeCompleted { get; set; }

    /// <summary>
    /// Gets or sets the pending heartbeat counter.
    /// </summary>
    public ulong? PendingHeartbeatCounter { get; set; }

    /// <summary>
    /// Gets or sets the pending heartbeat nonce.
    /// </summary>
    public byte[]? PendingHeartbeatNonce { get; set; }

    /// <summary>
    /// Gets or sets when the pending heartbeat was issued (UTC).
    /// </summary>
    public DateTime? HeartbeatIssuedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets when the last valid heartbeat response arrived (UTC).
    /// </summary>
    public DateTime LastHeartbeatOkUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the reported hardware id hash (32 bytes), if any.
    /// </summary>
    public byte[]? HwidHash { get; set; }

    /// <summary>
    /// Gets or sets the last client integrity CRC reported with a heartbeat.
    /// </summary>
    public uint LastIntegrityCrc { get; set; }

    /// <summary>
    /// Gets the monotonic heartbeat counter.
    /// </summary>
    public ulong HeartbeatCounter { get; set; }
}
