// <copyright file="SecureSessionOptions.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.Security;

using MUnique.OpenMU.Network.Security;

/// <summary>
/// Runtime options for challenge / heartbeat enforcement.
/// </summary>
public static class SecureSessionOptions
{
    /// <summary>
    /// Gets a value indicating whether challenge-response and heartbeat are enforced
    /// (invalid or missing responses disconnect the client).
    /// </summary>
    /// <remarks>
    /// <c>PROMU_SECURE_SESSION=off</c> disables enforcement.
    /// <c>PROMU_SECURE_SESSION=enforce</c> forces enforcement.
    /// Default: enforce when <see cref="PromuNetworkKeys.Environment"/> is not local.
    /// </remarks>
    public static bool Enforce
    {
        get
        {
            var raw = Environment.GetEnvironmentVariable("PROMU_SECURE_SESSION");
            if (string.Equals(raw, "off", StringComparison.OrdinalIgnoreCase)
                || string.Equals(raw, "0", StringComparison.OrdinalIgnoreCase)
                || string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(raw, "enforce", StringComparison.OrdinalIgnoreCase)
                || string.Equals(raw, "on", StringComparison.OrdinalIgnoreCase)
                || string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            PromuNetworkKeys.EnsureInitialized();
            return PromuNetworkKeys.Environment != NetworkKeyEnvironment.Local;
        }
    }

    /// <summary>
    /// Gets the heartbeat interval.
    /// </summary>
    public static TimeSpan HeartbeatInterval { get; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gets how long the client may take to answer a challenge or heartbeat.
    /// </summary>
    public static TimeSpan ResponseTimeout { get; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Shared HMAC secret material mixed into session tokens.
    /// Override with <c>PROMU_SESSION_HMAC_SECRET</c> in test/prod.
    /// </summary>
    public static string HmacSecret =>
        Environment.GetEnvironmentVariable("PROMU_SESSION_HMAC_SECRET")
        ?? "promu-local-session-hmac-not-for-prod";
}
