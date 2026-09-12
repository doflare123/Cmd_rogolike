namespace CmdRoguelike.Domain.Items;

public enum ItemLocation { Backpack, Equipment }
public enum EquipmentState { Active, Dormant }

/// <summary>Only ActorInventory creates and moves instances. A stack has one identity.</summary>
public sealed class ItemInstance
{
	public Guid Id { get; } = Guid.NewGuid();
	public Guid OwnerId { get; }
	public ItemDefinition Definition { get; }
	public int Quantity { get; internal set; }
	public ItemLocation Location { get; internal set; }
	public EquipmentSlot? Slot { get; internal set; }
	public EquipmentState State { get; internal set; } = EquipmentState.Dormant;
	internal ItemInstance(Guid ownerId, ItemDefinition definition, int quantity)
	{
		OwnerId = ownerId;
		Definition = definition;
		Quantity = quantity;
	}
}
