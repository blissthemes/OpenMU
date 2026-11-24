// <copyright file="PromuStadiumNonPvpPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.Promu;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Blocks player-versus-player attacks on Stadium (Arena map 6) — NON-PVP city.
/// </summary>
[PlugIn]
[Guid("B82D1F3C-8E55-4E9B-AC42-3F706D9E2B81")]
[Display(Name = "ProMU Stadium NON-PVP", Description = "Prevents PvP attacks between players on Stadium (Arena).")]
public sealed class PromuStadiumNonPvpPlugIn : IPlayerAttackRestrictionPlugIn
{
    /// <inheritdoc />
    public void CheckAttack(Player attacker, Player defender, CancelEventArgs eventArgs)
    {
        var mapNumber = attacker.CurrentMap?.MapId ?? defender.CurrentMap?.MapId;
        if (mapNumber == PromuVipPrivileges.StadiumMapNumber)
        {
            eventArgs.Cancel = true;
        }
    }
}
