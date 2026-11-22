// <copyright file="SecurityViolationRepository.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.AdminAuth;

using System.Threading;
using MUnique.OpenMU.Persistence.AdminAuth;

/// <summary>
/// EF Core implementation of <see cref="ISecurityViolationRepository"/>.
/// </summary>
public sealed class SecurityViolationRepository : ISecurityViolationRepository
{
    /// <inheritdoc />
    public async ValueTask AddAsync(SecurityViolation violation, CancellationToken cancellationToken = default)
    {
        await using var context = new AdminPanelContext();
        context.SecurityViolations.Add(violation);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
