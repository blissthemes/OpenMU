// <copyright file="PacketRateLimiter.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.Security;

/// <summary>
/// Token-bucket style per-connection incoming packet rate limiter.
/// </summary>
public sealed class PacketRateLimiter
{
    private readonly object _lock = new();
    private readonly double _maxPacketsPerSecond;
    private readonly double _burst;
    private double _tokens;
    private long _lastTimestamp;

    /// <summary>
    /// Initializes a new instance of the <see cref="PacketRateLimiter"/> class.
    /// </summary>
    /// <param name="maxPacketsPerSecond">Sustained packets per second.</param>
    /// <param name="burst">Maximum burst size.</param>
    public PacketRateLimiter(double maxPacketsPerSecond = 120, double burst = 240)
    {
        this._maxPacketsPerSecond = maxPacketsPerSecond;
        this._burst = burst;
        this._tokens = burst;
        this._lastTimestamp = Environment.TickCount64;
    }

    /// <summary>
    /// Creates a limiter from environment overrides.
    /// </summary>
    /// <returns>The configured limiter.</returns>
    public static PacketRateLimiter FromEnvironment()
    {
        var max = ParseDouble("PROMU_PACKET_RATE_PER_SEC", 120);
        var burst = ParseDouble("PROMU_PACKET_RATE_BURST", 240);
        return new PacketRateLimiter(max, burst);
    }

    /// <summary>
    /// Tries to consume one packet slot.
    /// </summary>
    /// <returns><see langword="true"/> when the packet is allowed.</returns>
    public bool TryAcquire()
    {
        lock (this._lock)
        {
            var now = Environment.TickCount64;
            var elapsedSeconds = Math.Max(0, (now - this._lastTimestamp) / 1000.0);
            this._lastTimestamp = now;
            this._tokens = Math.Min(this._burst, this._tokens + (elapsedSeconds * this._maxPacketsPerSecond));
            if (this._tokens < 1.0)
            {
                return false;
            }

            this._tokens -= 1.0;
            return true;
        }
    }

    private static double ParseDouble(string name, double fallback)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        return double.TryParse(raw, out var value) && value > 0 ? value : fallback;
    }
}
