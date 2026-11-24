// <copyright file="ComStatChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// ProMU QoL: A chat command plugin which handles the short command <c>/com</c> to add command stat points.
/// It's an alias of <c>/add cmd</c>, and is enabled by default.
/// </summary>
[Guid("6ED6367B-6663-47DE-9BBD-F3D58EF71630")]
[PlugIn]
[Display(Name = "Add command stat (/com)", Description = "Adds command stat points. Usage: /com <amount>. Short alias of /add cmd <amount>.")]
[ChatCommandHelp(Command, typeof(AddSingleStatChatCommandArgs), MinimumStatus)]
public class ComStatChatCommandPlugIn : AddStatChatCommandPlugIn
{
    private const string Command = "/com";

    private const CharacterStatus MinimumStatus = CharacterStatus.Normal;

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc />
    public override CharacterStatus MinCharacterStatusRequirement => MinimumStatus;

    /// <inheritdoc />
    public override async ValueTask HandleCommandAsync(Player player, string command)
    {
        // Rewrites e.g. "/com 10" to "/add cmd 10".
        command = "/add cmd" + command[Command.Length..];
        await base.HandleCommandAsync(player, command).ConfigureAwait(false);
    }
}