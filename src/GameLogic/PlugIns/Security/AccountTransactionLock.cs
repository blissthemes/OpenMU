// <copyright file="AccountTransactionLock.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.Security;

using System.Collections.Concurrent;
using System.Threading;
using MUnique.OpenMU.Persistence;

/// <summary>
/// Process-wide per-account lock for item / economy mutations (trade, vault, chaos, drop/pickup, website rewards).
/// </summary>
public static class AccountTransactionLock
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> Locks = new();

    /// <summary>
    /// Acquires the lock for an account id.
    /// </summary>
    /// <param name="accountId">The account id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A disposable release handle.</returns>
    public static async ValueTask<IAsyncDisposable> AcquireAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var gate = Locks.GetOrAdd(accountId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Releaser(gate);
    }

    /// <summary>
    /// Runs an action under the account lock when an account is available.
    /// </summary>
    /// <param name="account">The account, or null.</param>
    /// <param name="action">The action.</param>
    /// <returns>The task.</returns>
    public static async ValueTask RunAsync(DataModel.Entities.Account? account, Func<ValueTask> action)
    {
        if (account is null)
        {
            await action().ConfigureAwait(false);
            return;
        }

        await using var handle = await AcquireAsync(account.GetId()).ConfigureAwait(false);
        await action().ConfigureAwait(false);
    }

    /// <summary>
    /// Runs an action under the account lock for a known account id.
    /// </summary>
    /// <param name="accountId">The account id, or null.</param>
    /// <param name="action">The action.</param>
    /// <returns>The task.</returns>
    public static async ValueTask RunAsync(Guid? accountId, Func<ValueTask> action)
    {
        if (accountId is null || accountId == Guid.Empty)
        {
            await action().ConfigureAwait(false);
            return;
        }

        await using var handle = await AcquireAsync(accountId.Value).ConfigureAwait(false);
        await action().ConfigureAwait(false);
    }

    private sealed class Releaser(SemaphoreSlim gate) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            gate.Release();
            return ValueTask.CompletedTask;
        }
    }
}
