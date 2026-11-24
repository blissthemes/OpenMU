// <copyright file="EneStatChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// ProMU QoL: A chat command plugin which handles the short command <c>/ene</c> to add energy stat points.
/// It's an alias of <c>/add ene</c>, and is enabled by default.
/// </summary>
[Guid("C2E638F7-5B08-444F-A2D4-975DE696EF86")]
[PlugIn]
[Display(Name = "Add energy stat (/ene)", Description = "Adds energy stat points. Usage: /ene <amount>. Short alias of /add ene <amount>.")]
[ChatCommandHelp(Command, typeof(AddSingleStatChatCommandArgs), MinimumStatus)]
public class EneStatChatCommandPlugIn : AddStatChatCommandPlugIn
{
    private const string Command = "/ene";

    private const CharacterStatus MinimumStatus = CharacterStatus.Normal;

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    public override CharacterStatus MinCharacterStatusRequirement => MinimumStatus;

    /// <inheritdoc />
    public override async ValueTask HandleCommandAsync(Player player, string command)
    {
        // Rewrites e.g. "/ene 10" to "/add ene 10".
        command = "/add ene" + command[Command.Length..];
        await base.HandleCommandAsync(player, command).ConfigureAwait(false);
    }
}