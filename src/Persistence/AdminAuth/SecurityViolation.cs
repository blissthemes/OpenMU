// <copyright file="SecurityViolation.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AdminAuth;

/// <summary>
/// Durable record of a client security / anti-cheat violation.
/// </summary>
public sealed class SecurityViolation
{
    /// <summary>Gets or sets the identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the UTC time of the violation.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Gets or sets the account login name, if known.</summary>
    public string AccountLogin { get; set; } = string.Empty;

    /// <summary>Gets or sets the character name, if known.</summary>
    public string CharacterName { get; set; } = string.Empty;

    /// <summary>Gets or sets the client IP address, if known.</summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>Gets or sets the hardware id hash (hex), if known.</summary>
    public string HwidHash { get; set; } = string.Empty;

    /// <summary>Gets or sets the violation category (SpeedHack, Macro, Movement, Flood, Hwid, …).</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Gets or sets the severity label.</summary>
    public string Severity { get; set; } = string.Empty;

    /// <summary>Gets or sets a short human-readable detail.</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>Gets or sets the applied sanction (None, Warn, Disconnect, Ban).</summary>
    public string Sanction { get; set; } = "None";
}
