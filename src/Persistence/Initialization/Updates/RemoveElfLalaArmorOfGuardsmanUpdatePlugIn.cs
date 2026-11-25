// <copyright file="RemoveElfLalaArmorOfGuardsmanUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Removes Armor of Guardsman (group 13, number 29) from Elf Lala (NPC 242) merchant store.
/// </summary>
[PlugIn]
[Guid("A7C4E2B1-9F60-4D8E-B3A5-1C2D0E8F6A49")]
public class RemoveElfLalaArmorOfGuardsmanUpdatePlugIn : UpdatePlugInBase
{
    private const short ElfLalaNumber = 242;
    private const byte ArmorOfGuardsmanGroup = 13;
    private const short ArmorOfGuardsmanNumber = 29;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => "Remove Armor of Guardsman from Elf Lala";

    /// <inheritdoc />
    public override string Description =>
        "Removes Armor of Guardsman from the Noria Elf Lala merchant store (ProMU).";

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 25, 14, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override async ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var elfLala = gameConfiguration.Monsters.FirstOrDefault(m => m.Number == ElfLalaNumber);
        if (elfLala?.MerchantStore?.Items is not { } items)
        {
            return;
        }

        var toRemove = items
            .Where(item => item.Definition is { } def
                && def.Group == ArmorOfGuardsmanGroup
                && def.Number == ArmorOfGuardsmanNumber)
            .ToList();

        foreach (var item in toRemove)
        {
            items.Remove(item);
            await context.DeleteAsync(item).ConfigureAwait(false);
        }
    }
}
