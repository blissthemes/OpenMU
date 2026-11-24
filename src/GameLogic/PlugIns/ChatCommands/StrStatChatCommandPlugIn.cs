// <copyright file="StrStatChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// ProMU QoL: A chat command plugin which handles the short command <c>/str</c> to add strength stat points.
/// It's an alias of <c>/add str</c>, and is enabled by default.
/// </summary>
[Guid("DB00B6F6-9C62-455C-ACF1-E90C6BF0E066")]
[PlugIn]
[Display(Name = "Add strength stat (/str)", Description = "Adds strength stat points. Usage: /str <amount>. Short alias of /add str <amount>.")]
[ChatCommandHelp(Command, typeof(AddSingleStatChatCommandArgs), MinimumStatus)]
public class StrStatChatCommandPlugIn : AddStatChatCommandPlugIn
{
    private const string Command = "/str";

    private const CharacterStatus MinimumStatus = CharacterStatus.Normal;

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    public override CharacterStatus MinCharacterStatusRequirement => MinimumStatus;

    /// <inheritdoc />
    public override async ValueTask HandleCommandAsync(Player player, string command)
    {
        // Rewrites e.g. "/str 10" to "/add str 10".
        command = "/add str" + command[Command.Length..];
        await base.HandleCommandAsync(player, command).ConfigureAwait(false);
    }
}