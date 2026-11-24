// <copyright file="AgiStatChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// ProMU QoL: A chat command plugin which handles the short command <c>/agi</c> to add agility stat points.
/// It's an alias of <c>/add agi</c>, and is enabled by default.
/// </summary>
[Guid("2D400748-A5DF-455F-AD43-2A3638892D18")]
[PlugIn]
[Display(Name = "Add agility stat (/agi)", Description = "Adds agility stat points. Usage: /agi <amount>. Short alias of /add agi <amount>.")]
[ChatCommandHelp(Command, typeof(AddSingleStatChatCommandArgs), MinimumStatus)]
public class AgiStatChatCommandPlugIn : AddStatChatCommandPlugIn
{
    private const string Command = "/agi";

    private const CharacterStatus MinimumStatus = CharacterStatus.Normal;

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    public override CharacterStatus MinCharacterStatusRequirement => MinimumStatus;

    /// <inheritdoc />
    public override async ValueTask HandleCommandAsync(Player player, string command)
    {
        // Rewrites e.g. "/agi 10" to "/add agi 10".
        command = "/add agi" + command[Command.Length..];
        await base.HandleCommandAsync(player, command).ConfigureAwait(false);
    }
}