// <copyright file="ISecurityViolationRepository.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.AdminAuth;

using System.Threading;

/// <summary>
/// Persists <see cref="SecurityViolation"/> rows in the admin schema.
/// </summary>
public interface ISecurityViolationRepository
{
    /// <summary>
    /// Adds a violation row.
    /// </summary>
    /// <param name="violation">The violation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The task.</returns>
    ValueTask AddAsync(SecurityViolation violation, CancellationToken cancellationToken = default);
}
