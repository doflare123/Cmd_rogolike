using CmdRoguelike.Domain.Entities;
using CmdRoguelike.Domain.Stats;

namespace CmdRoguelike.Domain.Items;

public enum InventoryResult { Success, Locked, NotFound, InvalidSlot, Occupied, RequirementsNotMet, Full }

/// <summary>Single owner of backpack/equipment locations and sourced equipment bonuses.</summary>
public sealed class ActorInventory
{
	private readonly Actor _actor;
	private readonly List<ItemInstance> _items = new();
	private bool _recalculating;
	public BodyPlan Body { get; }
	public int Capacity { get; }
	public bool IsLocked { get; private set; }
	public IReadOnlyList<ItemInstance> Items => _items.AsReadOnly();
	public int UsedSlots => _items.Count(item => item.Location == ItemLocation.Backpack);

	internal ActorInventory(Actor actor, BodyPlan body, int capacity = 12)
	{
		if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
		_actor = actor;
		Body = body;
		Capacity = capacity;
		actor.Attributes.ValueChanged += (_, _, _) => Recalculate();
	}

	public void LockForExpedition() => IsLocked = true;

	/// <summary>Adds newly acquired loot; failure never partially fills existing stacks.</summary>
	public InventoryResult TryAcquire(ItemDefinition definition, int quantity = 1)
	{
		ArgumentNullException.ThrowIfNull(definition);
		if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
		var stacks = _items.Where(item => item.Location == ItemLocation.Backpack
			&& ReferenceEquals(item.Definition, definition)).ToArray();
		long room = stacks.Sum(item => (long)definition.MaximumStack - item.Quantity);
		long remainder = Math.Max(0, quantity - room);
		long needed = (remainder + definition.MaximumStack - 1) / definition.MaximumStack;
		if (needed > Capacity - UsedSlots) return InventoryResult.Full;
		foreach (var stack in stacks)
		{
			int added = Math.Min(quantity, definition.MaximumStack - stack.Quantity);
			stack.Quantity += added;
			quantity -= added;
		}
		while (quantity > 0)
		{
			int added = Math.Min(quantity, definition.MaximumStack);
			_items.Add(new ItemInstance(_actor.Id, definition, added));
			quantity -= added;
		}
		return InventoryResult.Success;
	}

	public InventoryResult TryEquip(Guid id, EquipmentSlot slot)
	{
		if (IsLocked || !_actor.IsAlive) return InventoryResult.Locked;
		var item = _items.Find(item => item.Id == id && item.Location == ItemLocation.Backpack);
		if (item is null) return InventoryResult.NotFound;
		if (!Body.Slots.Contains(slot) || !item.Definition.Slots.Contains(slot)) return InventoryResult.InvalidSlot;
		if (_items.Any(item => item.Slot == slot)) return InventoryResult.Occupied;
		if (!MeetsRequirements(item, ActiveItems())) return InventoryResult.RequirementsNotMet;
		item.Location = ItemLocation.Equipment;
		item.Slot = slot;
		item.State = EquipmentState.Active;
		try { Recalculate(); }
		catch
		{
			item.Location = ItemLocation.Backpack;
			item.Slot = null;
			item.State = EquipmentState.Dormant;
			throw;
		}
		return InventoryResult.Success;
	}

	public InventoryResult TryUnequip(Guid id)
	{
		if (IsLocked || !_actor.IsAlive) return InventoryResult.Locked;
		var item = _items.Find(item => item.Id == id && item.Location == ItemLocation.Equipment);
		if (item is null) return InventoryResult.NotFound;
		if (UsedSlots >= Capacity) return InventoryResult.Full;
		var oldSlot = item.Slot;
		var oldState = item.State;
		item.Location = ItemLocation.Backpack;
		item.Slot = null;
		item.State = EquipmentState.Dormant;
		try { Recalculate(); }
		catch
		{
			item.Location = ItemLocation.Equipment;
			item.Slot = oldSlot;
			item.State = oldState;
			throw;
		}
		return InventoryResult.Success;
	}

	private HashSet<ItemInstance> ActiveItems() => _items.Where(item => item.Location == ItemLocation.Equipment
		&& item.State == EquipmentState.Active).ToHashSet();

	private bool MeetsRequirements(ItemInstance item, HashSet<ItemInstance> active) =>
		item.Definition.Requirements.All(requirement => _actor.Attributes.PreviewEquipment(requirement.Key,
			AttributeModifiers(active, requirement.Key)) >= requirement.Value);

	private static IEnumerable<StatModifier> AttributeModifiers(IEnumerable<ItemInstance> active, AttributeId stat) =>
		active.Where(item => item.Definition.AttributeBonuses.ContainsKey(stat))
		.Select(item => new StatModifier($"item:{item.Id}", StatModifierOperation.Flat, item.Definition.AttributeBonuses[stat]));

	private void Recalculate()
	{
		if (_recalculating) return;
		_recalculating = true;
		try
		{
			var equipped = _items.Where(item => item.Location == ItemLocation.Equipment).ToArray();
			var active = ActiveItems();
			// Simultaneous removal preserves self-credit and supported cycles. Reactivation
			// uses only already active items, never a dormant item's own bonuses.
			while (true)
			{
				var invalid = active.Where(item => !MeetsRequirements(item, active)).ToArray();
				if (invalid.Length == 0) break;
				active.ExceptWith(invalid);
			}
			while (true)
			{
				var restored = equipped.Where(item => !active.Contains(item) && MeetsRequirements(item, active)).ToArray();
				if (restored.Length == 0) break;
				active.UnionWith(restored);
			}
			var attributes = Enum.GetValues<AttributeId>().ToDictionary(stat => stat,
				stat => AttributeModifiers(active, stat).ToList());
			var stats = Enum.GetValues<DerivedStatId>().ToDictionary(stat => stat,
				stat => active.Where(item => item.Definition.StatBonuses.ContainsKey(stat))
				.Select(item => new StatModifier($"item:{item.Id}", StatModifierOperation.Flat, item.Definition.StatBonuses[stat])).ToList());
			// Validate both collections before publishing any state or clamping resources.
			foreach (var pair in attributes) _ = _actor.Attributes.PreviewEquipment(pair.Key, pair.Value);
			foreach (var pair in stats) _ = _actor.DerivedStats.PreviewEquipment(pair.Key, pair.Value);
			foreach (var item in equipped) item.State = active.Contains(item) ? EquipmentState.Active : EquipmentState.Dormant;
			_actor.Attributes.SetEquipment(attributes);
			_actor.DerivedStats.SetEquipment(stats);
		}
		finally { _recalculating = false; }
	}
}
