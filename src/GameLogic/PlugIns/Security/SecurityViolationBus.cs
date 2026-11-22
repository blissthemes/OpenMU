// <copyright file="SecurityViolationBus.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.Security;

/// <summary>
/// Fire-and-forget bus so GameLogic plugins can persist security events without taking an EF dependency.
/// </summary>
public static class SecurityViolationBus
{
    /// <summary>
    /// Gets or sets the optional async publisher registered by Startup / GameServer.
    /// </summary>
    public static Func<SecurityViolationEvent, ValueTask>? PublishAsync { get; set; }

    /// <summary>
    /// Publishes a violation when a publisher is registered.
    /// </summary>
    /// <param name="violationEvent">The event.</param>
    public static void TryPublish(SecurityViolationEvent violationEvent)
    {
        var publish = PublishAsync;
        if (publish is null)
        {
            return;
        }

        try
        {
            // Prefer synchronous completion (tests + in-memory publishers); only offload when truly async.
            var task = publish(violationEvent);
            if (!task.IsCompletedSuccessfully)
            {
                _ = PersistInBackgroundAsync(task);
            }
        }
        catch
        {
            // Persistence failures must not take down gameplay.
        }
    }

    private static async Task PersistInBackgroundAsync(ValueTask task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
            // Persistence failures must not take down gameplay.
        }
    }
}

/// <summary>
/// A security violation event payload.
/// </summary>
/// <param name="AccountLogin">Account login.</param>
/// <param name="CharacterName">Character name.</param>
/// <param name="IpAddress">Client IP.</param>
/// <param name="HwidHash">HWID hash hex.</param>
/// <param name="Category">Category.</param>
/// <param name="Severity">Severity.</param>
/// <param name="Detail">Detail.</param>
/// <param name="Sanction">Sanction applied.</param>
public sealed record SecurityViolationEvent(
    string AccountLogin,
    string CharacterName,
    string IpAddress,
    string HwidHash,
    string Category,
    string Severity,
    string Detail,
    string Sanction);
