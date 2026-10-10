using CmdRoguelike.Domain.Entities;
using CmdRoguelike.Domain.Items;
using CmdRoguelike.Domain.Stats;
using CmdRoguelike.Domain.Combat;
using CmdRoguelike.Generation;
using Godot;

namespace CmdRoguelike.World;

/// <summary>Preparation and expedition have the same hero and item identities.</summary>
public sealed class PreparationSession
{
	public PlayerCharacter Hero { get; } = new(Vector2I.Zero);
	public DungeonMap? Map { get; private set; }

	public PreparationSession()
	{
		foreach ((ItemDefinition definition, int quantity) in PrototypeItems.StartingItems)
			if (Hero.Inventory.TryAcquire(definition, quantity) != InventoryResult.Success)
				throw new InvalidOperationException("Starting items do not fit in backpack.");
		// Isolated fixed seed keeps debug resets and tests reproducible without touching world RNG.
		var generator = new ItemRollGenerator(new GodotRandomSource(73013), PrototypeAffixes.All);
		foreach (var (definition, rarity, level) in PrototypeItems.AffixSamples)
			if (Hero.Inventory.TryAcquireRolled(generator.Roll(definition, rarity, level)) != InventoryResult.Success)
				throw new InvalidOperationException("Affix samples do not fit in backpack.");
	}

	public DungeonMap StartExpedition(int seed, DungeonGenerationOptions options)
	{
		if (Map is not null) throw new InvalidOperationException("Expedition has already started.");
		Map = new DungeonMap(seed, options, Hero);
		return Map;
	}
}

/// <summary>Temporary content for testing preparation, not final item balance.</summary>
internal static class PrototypeItems
{
	public static IReadOnlyList<(ItemDefinition Definition, int Quantity)> StartingItems { get; } =
		Array.AsReadOnly(new (ItemDefinition, int)[]
		{
			(new("training-sword", "Учебный меч", slots: new[] { EquipmentSlot.MainHand },
				requirements: new Dictionary<AttributeId, int> { [AttributeId.Strength] = 2 },
				attributeBonuses: new Dictionary<AttributeId, int> { [AttributeId.Strength] = 2 },
				combatCards: new[] { new EquipmentCardGrant(new("slash", "Рубящий удар", CombatCardKind.Attack, 1, 2), 3) }, category: ItemCategory.Weapon), 1),
			(new("training-armor", "Учебный доспех", slots: new[] { EquipmentSlot.Torso },
				requirements: new Dictionary<AttributeId, int> { [AttributeId.Strength] = 2 },
				statBonuses: new Dictionary<DerivedStatId, int> { [DerivedStatId.MaxHealth] = 5, [DerivedStatId.Armor] = 2 }, category: ItemCategory.Armor), 1),
			(new("strength-ring", "Кольцо силы", slots: new[] { EquipmentSlot.RingLeft, EquipmentSlot.RingRight },
				attributeBonuses: new Dictionary<AttributeId, int> { [AttributeId.Strength] = 2 }, category: ItemCategory.Jewelry,
				nameGender: ItemNameGender.Neuter), 1),
			(new("feather", "Перо", maximumStack: 100), 125),
			(new("training-mace", "Учебная булава", slots: new[] { EquipmentSlot.MainHand },
				requirements: new Dictionary<AttributeId, int> { [AttributeId.Strength] = 2 },
				combatCards: new[] { new EquipmentCardGrant(new("crush", "Сокрушение", CombatCardKind.Attack, 2, 4), 2) }, category: ItemCategory.Weapon,
				nameGender: ItemNameGender.Feminine), 1),
			(new("training-shield", "Учебный щит", slots: new[] { EquipmentSlot.OffHand },
				statBonuses: new Dictionary<DerivedStatId, int> { [DerivedStatId.Armor] = 1 },
				combatCards: new[] { new EquipmentCardGrant(new("shield", "Щит", CombatCardKind.Defense, 1, 200), 2) }, category: ItemCategory.Shield), 1),
		});

	public static IReadOnlyList<(ItemDefinition Definition, ItemRarity Rarity, int Level)> AffixSamples { get; } =
		Array.AsReadOnly(new (ItemDefinition, ItemRarity, int)[]
		{
			(new("sample-armor", "Кираса", slots: new[] { EquipmentSlot.Torso },
				requirements: new Dictionary<AttributeId, int> { [AttributeId.Strength] = 2 },
				statBonuses: new Dictionary<DerivedStatId, int> { [DerivedStatId.Armor] = 2 },
				category: ItemCategory.Armor, nameGender: ItemNameGender.Feminine), ItemRarity.Uncommon, 1),
			(new("sample-shield", "Щит стража", slots: new[] { EquipmentSlot.OffHand },
				statBonuses: new Dictionary<DerivedStatId, int> { [DerivedStatId.Armor] = 1 },
				combatCards: new[] { new EquipmentCardGrant(new("shield", "Щит", CombatCardKind.Defense, 1, 200), 2) },
				category: ItemCategory.Shield), ItemRarity.Rare, 5),
		});
}
