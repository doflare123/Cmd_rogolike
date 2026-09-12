using CmdRoguelike.Domain.Entities;
using CmdRoguelike.Domain.Items;
using CmdRoguelike.Domain.Stats;
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
				attributeBonuses: new Dictionary<AttributeId, int> { [AttributeId.Strength] = 2 }), 1),
			(new("training-armor", "Учебный доспех", slots: new[] { EquipmentSlot.Torso },
				requirements: new Dictionary<AttributeId, int> { [AttributeId.Strength] = 2 },
				statBonuses: new Dictionary<DerivedStatId, int> { [DerivedStatId.MaxHealth] = 5 }), 1),
			(new("strength-ring", "Кольцо силы", slots: new[] { EquipmentSlot.RingLeft, EquipmentSlot.RingRight },
				attributeBonuses: new Dictionary<AttributeId, int> { [AttributeId.Strength] = 2 }), 1),
			(new("feather", "Перо", maximumStack: 100), 125),
		});
}
