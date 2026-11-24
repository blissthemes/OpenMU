// <copyright file="GuildWarAliasChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// ProMU QoL: A chat command plugin which handles the alias <c>/guildwar</c> of the guild war command <c>/war</c>.
/// It has the same behavior as <see cref="GuildWarChatCommandPlugIn"/> and is enabled by default.
/// </summary>
[Guid("762086D1-B6A3-4284-B568-7E0E37DC162C")]
[PlugIn]
[Display(Name = "Guild war alias (/guildwar)", Description = "Requests a guild war against another guild. Usage: /guildwar <guild name>. Alias of /war.")]
[ChatCommandHelp(Command, typeof(GuildWarChatCommandArgs), CharacterStatus.Normal)]
public class GuildWarAliasChatCommandPlugIn : GuildWarChatCommandPlugIn
{
    private const string Command = "/guildwar";

    /// <inheritdoc />
    public override string Key => Command;
}