using CmdRoguelike.Domain.Stats;

namespace CmdRoguelike.Domain.Items;

public enum ItemLocation { Backpack, Equipment, Storage }
public enum EquipmentState { Active, Dormant }

/// <summary>Only ActorInventory creates and moves instances. A stack has one identity.</summary>
public sealed class ItemInstance
{
	public Guid Id { get; }
	public Guid OwnerId { get; internal set; }
	public ItemRoll Roll { get; }
	public ItemDefinition Definition => Roll.Definition;
	public string DisplayName => Roll.DisplayName;
	public ItemRarity Rarity => Roll.Rarity;
	public int ItemLevel => Roll.ItemLevel;
	public IReadOnlyList<ItemAffix> Affixes => Roll.Affixes;
	public IReadOnlyDictionary<AttributeId, int> AttributeBonuses => Roll.AttributeBonuses;
	public IReadOnlyDictionary<DerivedStatId, int> StatBonuses => Roll.StatBonuses;
	public int Quantity { get; internal set; }
	public ItemLocation Location { get; internal set; }
	public EquipmentSlot? Slot { get; internal set; }
	public EquipmentState State { get; internal set; } = EquipmentState.Dormant;
	internal ItemInstance(Guid ownerId, ItemRoll roll, int quantity, Guid? id = null)
	{
		Id = id ?? Guid.NewGuid();
		if (Id == Guid.Empty || ownerId == Guid.Empty || quantity < 1 || quantity > roll.Definition.MaximumStack)
			throw new ArgumentException("Invalid item identity or quantity.");
		OwnerId = ownerId;
		Roll = roll;
		Quantity = quantity;
	}
}
