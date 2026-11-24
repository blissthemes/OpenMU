// <copyright file="VitStatChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// ProMU QoL: A chat command plugin which handles the short command <c>/vit</c> to add vitality stat points.
/// It's an alias of <c>/add vit</c>, and is enabled by default.
/// </summary>
[Guid("1A5181B8-D654-487D-981C-396CC034A705")]
[PlugIn]
[Display(Name = "Add vitality stat (/vit)", Description = "Adds vitality stat points. Usage: /vit <amount>. Short alias of /add vit <amount>.")]
[ChatCommandHelp(Command, typeof(AddSingleStatChatCommandArgs), MinimumStatus)]
public class VitStatChatCommandPlugIn : AddStatChatCommandPlugIn
{
    private const string Command = "/vit";

    private const CharacterStatus MinimumStatus = CharacterStatus.Normal;

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    public override CharacterStatus MinCharacterStatusRequirement => MinimumStatus;

    /// <inheritdoc />
    public override async ValueTask HandleCommandAsync(Player player, string command)
    {
        // Rewrites e.g. "/vit 10" to "/add vit 10".
        command = "/add vit" + command[Command.Length..];
        await base.HandleCommandAsync(player, command).ConfigureAwait(false);
    }
}