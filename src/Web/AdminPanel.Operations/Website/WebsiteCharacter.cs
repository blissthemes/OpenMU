// <copyright file="WebsiteCharacter.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Operations.Website;

/// <summary>
/// The public profile of a character, as a website shows it, e.g. in an armory.
/// </summary>
/// <param name="Name">The name of the character.</param>
/// <param name="ClassName">The name of the character class.</param>
/// <param name="Level">The level.</param>
/// <param name="MasterLevel">The master level.</param>
/// <param name="Resets">The number of resets.</param>
/// <param name="GuildName">The name of the guild of the character; <c>null</c>, if it's not a member of a guild.</param>
/// <param name="Strength">The base strength of the character (saved attribute, without live item totals).</param>
/// <param name="Agility">The base agility of the character.</param>
/// <param name="Vitality">The base vitality of the character.</param>
/// <param name="Energy">The base energy of the character.</param>
/// <param name="Leadership">The base leadership of the character (Dark Lord line; 0 for other classes).</param>
/// <param name="EquippedItems">The equipped items.</param>
public sealed record WebsiteCharacter(
    string Name,
    string ClassName,
    int Level,
    int MasterLevel,
    int Resets,
    string? GuildName,
    int Strength,
    int Agility,
    int Vitality,
    int Energy,
    int Leadership,
    IReadOnlyList<WebsiteItem> EquippedItems);
