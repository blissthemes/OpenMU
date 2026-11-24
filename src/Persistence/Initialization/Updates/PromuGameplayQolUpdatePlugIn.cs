// <copyright file="PromuGameplayQolUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence.Initialization.Items;
using MUnique.OpenMU.Persistence.Initialization.Skills;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Maps;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// ProMU gameplay quality of life update: separates the quest NPC "Luke the Helper" (258) from the buffing
/// Shadow Phantom Soldier (257), makes bolts/arrows free, removes the spiders of Noria and
/// makes potions and apples sold in stacks.
/// </summary>
/// <remarks>
/// New databases get these changes through the initialization of the game configuration.
/// This update brings the existing databases to the same state.
/// </remarks>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("4CE0DAE0-9150-4FDF-9FFF-97E1508ED77D")]
public class PromuGameplayQolUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plug in name.
    /// </summary>
    internal const string PlugInName = "ProMU Gameplay QoL";

    /// <summary>
    /// The plug in description.
    /// </summary>
    internal const string PlugInDescription = "Moves the quests of the Shadow Phantom Soldier (257) to the new NPC Luke the Helper (258) and spawns him next to each Shadow Phantom Soldier, so that the soldier only buffs. Bolts and arrows are not consumed anymore. Removes the spiders of Noria. Apples, healing/mana potions and antidotes are sold and stacked up to 255 (antidote 10).";

    private const short ShadowPhantomNumber = 257;
    private const short LukeTheHelperNumber = 258;
    private const short SpiderNumber = 3;
    private const byte PotionGroup = 14;
    private const byte AppleNumber = 0;
    private const byte LastStackedPotionNumber = 6;
    private const byte AntidoteNumber = 8;
    private const byte PotionStackSize = 255;
    private const byte AntidoteStackSize = 10;
    private const int QuestGiverOffsetX = 2;

    /// <summary>
    /// The quest NPC spawns which are created next to the Shadow Phantom Soldier spawns.
    /// They have the same spawn numbers as in the map initializers.
    /// </summary>
    private static readonly (byte MapNumber, short SoldierSpawnNumber, short QuestNpcSpawnNumber)[] QuestNpcSpawns =
    [
        (Lorencia.Number, 18, 50),
        (Lorencia.Number, 19, 51),
        (Lorencia.Number, 20, 52),
        (Lorencia.Number, 21, 53),
        (Noria.Number, 11, 50),
        (Noria.Number, 15, 51),
        (Noria.Number, 16, 52),
        (Devias.Number, 36, 50),
        (Devias.Number, 43, 51),
        (Elvenland.Number, 5, 50),
        (Elvenland.Number, 6, 51),
        (Elvenland.Number, 7, 52),
    ];

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    public override IEnumerable<UpdateDependency> DependsOn => [new UpdateDependency(typeof(AddElfSoldierBuffPlugIn).GUID)];

    /// <inheritdoc />
    protected override async ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var shadowPhantom = gameConfiguration.Monsters.FirstOrDefault(m => m.Number == ShadowPhantomNumber);
        var questNpc = EnsureQuestNpc(context, gameConfiguration);
        await ClearBuffsAsync(context, questNpc).ConfigureAwait(false);
        if (shadowPhantom is not null)
        {
            MoveQuests(shadowPhantom, questNpc);
            EnsureShadowPhantomBuff(context, gameConfiguration, shadowPhantom);
        }

        AddQuestNpcSpawns(context, gameConfiguration, questNpc);
        await RemoveNoriaSpidersAsync(context, gameConfiguration).ConfigureAwait(false);
        ApplyFreeAmmunition(gameConfiguration);
        await ApplyPotionStacksAsync(context, gameConfiguration).ConfigureAwait(false);
    }

    private static MonsterDefinition EnsureQuestNpc(IContext context, GameConfiguration gameConfiguration)
    {
        var npc = gameConfiguration.Monsters.FirstOrDefault(m => m.Number == LukeTheHelperNumber);
        if (npc is null)
        {
            npc = context.CreateNew<MonsterDefinition>();
            npc.Number = LukeTheHelperNumber;
            npc.Designation = "Luke the Helper";
            npc.ObjectKind = NpcObjectKind.PassiveNpc;
            npc.SetGuid(npc.Number);
            gameConfiguration.Monsters.Add(npc);
        }

        npc.NpcWindow = NpcWindow.NpcDialog;
        return npc;
    }

    private static void MoveQuests(MonsterDefinition from, MonsterDefinition to)
    {
        foreach (var quest in from.Quests.ToList())
        {
            from.Quests.Remove(quest);
            if (!to.Quests.Contains(quest))
            {
                to.Quests.Add(quest);
            }

            quest.QuestGiver = to;
        }
    }

    private static void EnsureShadowPhantomBuff(IContext context, GameConfiguration gameConfiguration, MonsterDefinition shadowPhantom)
    {
        if (shadowPhantom.Buffs.Any())
        {
            return;
        }

        var buffEffect = gameConfiguration.MagicEffects.FirstOrDefault(e => e.Number == (short)MagicEffectNumber.ElfSoldierBuff);
        if (buffEffect is null)
        {
            // Shouldn't happen, because this update depends on the AddElfSoldierBuffPlugIn which creates it.
            return;
        }

        var buff = context.CreateNew<Buff>();
        buff.MagicEffectDefinition = buffEffect;
        buff.MaximumLevel = 220;
        shadowPhantom.Buffs.Add(buff);
    }

    private static void ApplyFreeAmmunition(GameConfiguration gameConfiguration)
    {
        var bows = gameConfiguration.Items.Where(i => i.Group == (byte)ItemGroups.Bows && !i.IsAmmunition);
        foreach (var bow in bows)
        {
            foreach (var powerUp in bow.BasePowerUpAttributes.Where(p => p.TargetAttribute?.Id == Stats.AmmunitionConsumptionRate.Id))
            {
                powerUp.BaseValue = 0;
            }
        }
    }

    private static bool IsStackedPotion(ItemDefinition? definition)
    {
        return definition is { Group: PotionGroup, Number: (>= AppleNumber and <= LastStackedPotionNumber) or AntidoteNumber };
    }

    private static byte GetStackSize(ItemDefinition definition)
    {
        return definition.Number == AntidoteNumber ? AntidoteStackSize : PotionStackSize;
    }

    private static async ValueTask ClearBuffsAsync(IContext context, MonsterDefinition npc)
    {
        foreach (var buff in npc.Buffs.ToList())
        {
            npc.Buffs.Remove(buff);
            await context.DeleteAsync(buff).ConfigureAwait(false);
        }
    }

    private static void AddQuestNpcSpawns(IContext context, GameConfiguration gameConfiguration, MonsterDefinition questNpc)
    {
        foreach (var (mapNumber, soldierSpawnNumber, questNpcSpawnNumber) in QuestNpcSpawns)
        {
            if (gameConfiguration.Maps.FirstOrDefault(m => m.Number == mapNumber) is not { } map)
            {
                continue;
            }

            var soldierSpawnId = GuidHelper.CreateGuid<MonsterSpawnArea>(map.Number, soldierSpawnNumber);
            var soldierSpawn = map.MonsterSpawns.FirstOrDefault(s => s.GetId() == soldierSpawnId && s.MonsterDefinition?.Number == ShadowPhantomNumber);
            if (soldierSpawn is null)
            {
                continue;
            }

            var questNpcSpawnId = GuidHelper.CreateGuid<MonsterSpawnArea>(map.Number, questNpcSpawnNumber);
            var alreadySpawned = map.MonsterSpawns.Any(s => s.GetId() == questNpcSpawnId
                                                            || (s.MonsterDefinition?.Number == LukeTheHelperNumber
                                                                && Math.Abs(s.X1 - soldierSpawn.X1) <= QuestGiverOffsetX + 1
                                                                && Math.Abs(s.Y1 - soldierSpawn.Y1) <= QuestGiverOffsetX + 1));
            if (alreadySpawned)
            {
                continue;
            }

            var spawn = context.CreateNew<MonsterSpawnArea>();
            spawn.SetGuid(questNpcSpawnId);
            spawn.GameMap = map;
            spawn.MonsterDefinition = questNpc;
            spawn.Quantity = 1;
            spawn.SpawnTrigger = SpawnTrigger.Automatic;
            spawn.Direction = soldierSpawn.Direction;
            spawn.X1 = (byte)Math.Min(byte.MaxValue, soldierSpawn.X1 + QuestGiverOffsetX);
            spawn.X2 = (byte)Math.Min(byte.MaxValue, soldierSpawn.X2 + QuestGiverOffsetX);
            spawn.Y1 = soldierSpawn.Y1;
            spawn.Y2 = soldierSpawn.Y2;
            map.MonsterSpawns.Add(spawn);
        }
    }

    private static async ValueTask RemoveNoriaSpidersAsync(IContext context, GameConfiguration gameConfiguration)
    {
        if (gameConfiguration.Maps.FirstOrDefault(m => m.Number == Noria.Number) is not { } noria)
        {
            return;
        }

        var spiderSpawns = noria.MonsterSpawns
            .Where(s => s.MonsterDefinition?.Number == SpiderNumber)
            .ToList();
        foreach (var spawn in spiderSpawns)
        {
            noria.MonsterSpawns.Remove(spawn);
            await context.DeleteAsync(spawn).ConfigureAwait(false);
        }
    }

    private static async ValueTask ApplyPotionStacksAsync(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var definition in gameConfiguration.Items.Where(IsStackedPotion))
        {
            definition.Durability = GetStackSize(definition);
        }

        var stores = gameConfiguration.Monsters
            .Select(m => m.MerchantStore)
            .OfType<ItemStorage>()
            .Distinct()
            .ToList();
        foreach (var store in stores)
        {
            var potionItems = store.Items.Where(i => IsStackedPotion(i.Definition)).OrderBy(i => i.ItemSlot).ToList();
            var keptItems = new HashSet<(ItemDefinition?, byte)>();
            foreach (var item in potionItems)
            {
                if (!keptItems.Add((item.Definition, item.Level)))
                {
                    // The former shop had a x1 and a x3 row of the same potion; now there's only one stack.
                    store.Items.Remove(item);
                    await context.DeleteAsync(item).ConfigureAwait(false);
                    continue;
                }

                item.Durability = GetStackSize(item.Definition!);
            }
        }
    }
}
