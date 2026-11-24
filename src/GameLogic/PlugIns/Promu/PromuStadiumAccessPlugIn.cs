// <copyright file="PromuStadiumAccessPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.Promu;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Enforces VIP + level 200 for Stadium (Arena map 6) warps. Server-side; client warp list is not trusted.
/// </summary>
[PlugIn]
[Guid("A91C0E2B-7F44-4D8A-9B31-2E6F5C8D1A70")]
[Display(Name = "ProMU Stadium VIP access", Description = "Requires active VIP and character level 200 to enter Stadium (Arena).")]
public sealed class PromuStadiumAccessPlugIn : IWarpGateEnteringPlugIn
{
    /// <inheritdoc />
    public async ValueTask WarpGateEnteringAsync(Player player, ExitGate targetGate, CancelEventArgs eventArgs)
    {
        if (targetGate.Map?.Number != PromuVipPrivileges.StadiumMapNumber)
        {
            return;
        }

        if (PromuVipPrivileges.CanEnterStadium(player))
        {
            return;
        }

        eventArgs.Cancel = true;
        var message = PromuVipPrivileges.IsVip(player)
            ? $"Stadium requires character level {PromuVipPrivileges.StadiumMinimumLevel}."
            : "Stadium is available for VIP characters only.";
        await player.ShowBlueMessageAsync(message).ConfigureAwait(false);
    }
}
