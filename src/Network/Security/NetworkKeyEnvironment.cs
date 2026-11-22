// <copyright file="NetworkKeyEnvironment.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Network.Security;

/// <summary>
/// Deployment environment that selects which network encryption keys are active.
/// </summary>
public enum NetworkKeyEnvironment
{
    /// <summary>
    /// Stock OpenMU / public GitHub keys. Used for lab development.
    /// </summary>
    Local = 0,

    /// <summary>
    /// Unique test-environment keys (not the public defaults).
    /// </summary>
    Test = 1,

    /// <summary>
    /// Unique production keys (not the public defaults).
    /// </summary>
    Prod = 2,
}
