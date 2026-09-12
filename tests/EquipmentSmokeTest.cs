using CmdRoguelike.Domain.Entities;
using CmdRoguelike.Domain.Items;
using CmdRoguelike.Domain.Stats;
using CmdRoguelike.Generation;
using CmdRoguelike.World;
using Godot;

namespace CmdRoguelike.Tests;

public partial class EquipmentSmokeTest : Node
{
	public override void _Ready()
	{
		try
		{
			SelfCreditAndResources();
			Cascade(false);
			Cascade(true);
			StacksAndOwnership();
			ExpeditionBoundary();
			InvalidEquipmentIsAtomic();
			GD.Print("Equipment smoke test passed: self-credit, cascades, resources, stacks, ownership, expedition boundary.");
			GetTree().Quit();
		}
		catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
	}

	private static void Check(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}

	private static ItemDefinition Gear(string id, EquipmentSlot slot, int requirement, int strength, int hp = 0) =>
		new(id, id, slots: new[] { slot },
			requirements: new Dictionary<AttributeId, int> { [AttributeId.Strength] = requirement },
			attributeBonuses: new Dictionary<AttributeId, int> { [AttributeId.Strength] = strength },
			statBonuses: new Dictionary<DerivedStatId, int> { [DerivedStatId.MaxHealth] = hp });

	private static ItemInstance Acquire(Actor hero, ItemDefinition definition)
	{
		Check(hero.Inventory.TryAcquire(definition) == InventoryResult.Success, "Acquire failed.");
		return hero.Inventory.Items.Last();
	}

	private static void SelfCreditAndResources()
	{
		var hero = new PlayerCharacter(Vector2I.Zero);
		var sword = Acquire(hero, Gear("sword", EquipmentSlot.MainHand, 2, 2, 5));
		Check(hero.Race is null, "Race placeholder must be null.");
		Check(hero.Inventory.TryEquip(sword.Id, EquipmentSlot.MainHand) == InventoryResult.RequirementsNotMet, "Item bootstrapped itself.");
		hero.Attributes.AddModifier(AttributeId.Strength, new("temporary", StatModifierOperation.Flat, 2));
		hero.TakeDamage(3);
		Check(hero.Inventory.TryEquip(sword.Id, EquipmentSlot.MainHand) == InventoryResult.Success, "Buff did not enable equipment.");
		Check(hero.Health == 7 && hero.MaxHealth == 15, "Equipping healed the hero.");
		hero.Attributes.RemoveModifiersFromSource("temporary");
		Check(sword.State == EquipmentState.Active && hero.Attributes.GetValue(AttributeId.Strength) == 2, "Self-credit lost.");
		hero.RestoreHealth(100);
		Check(hero.Inventory.TryUnequip(sword.Id) == InventoryResult.Success, "Unequip failed.");
		Check(hero.Health == 10 && hero.MaxHealth == 10, "Resource clamp failed.");
		Check(hero.Inventory.TryEquip(sword.Id, EquipmentSlot.MainHand) == InventoryResult.RequirementsNotMet, "Removed bonus remained.");
	}

	private static void Cascade(bool reverse)
	{
		var hero = new PlayerCharacter(Vector2I.Zero);
		var a = Gear("a", EquipmentSlot.RingLeft, 3, 1, 2);
		var b = Gear("b", EquipmentSlot.RingRight, 3, 1, 2);
		var first = Acquire(hero, reverse ? b : a);
		var second = Acquire(hero, reverse ? a : b);
		hero.Attributes.AddModifier(AttributeId.Strength, new("buff", StatModifierOperation.Flat, 3));
		Check(hero.Inventory.TryEquip(first.Id, first.Definition.Slots[0]) == InventoryResult.Success, "First equip failed.");
		Check(hero.Inventory.TryEquip(second.Id, second.Definition.Slots[0]) == InventoryResult.Success, "Second equip failed.");
		var material = new ItemDefinition("filler", "filler");
		Check(hero.Inventory.TryAcquire(material, 12) == InventoryResult.Success, "Could not fill bag.");
		hero.Attributes.RemoveModifiersFromSource("buff");
		Check(first.State == EquipmentState.Dormant && second.State == EquipmentState.Dormant, "Cascade failed.");
		Check(hero.Inventory.UsedSlots == 12 && first.Slot is not null && second.Slot is not null, "Dormant item lost its slot.");
		Check(hero.MaxHealth == 10 && hero.Attributes.GetValue(AttributeId.Strength) == 0, "Dormant bonuses remained.");
		hero.Attributes.AddModifier(AttributeId.Strength, new("small", StatModifierOperation.Flat, 1));
		Check(first.State == EquipmentState.Dormant && second.State == EquipmentState.Dormant, "Dormant cycle bootstrapped itself.");
		hero.Attributes.AddModifier(AttributeId.Strength, new("enough", StatModifierOperation.Flat, 2));
		Check(first.State == EquipmentState.Active && second.State == EquipmentState.Active, "Reactivation failed.");
		Check(hero.MaxHealth == 14 && hero.Health == 10, "Reactivation healed hero.");
		Check(hero.Inventory.TryUnequip(first.Id) == InventoryResult.Full && first.Location == ItemLocation.Equipment, "Full bag caused item loss.");
	}

	private static void StacksAndOwnership()
	{
		var hero = new PlayerCharacter(Vector2I.Zero);
		var feather = new ItemDefinition("feather", "feather", maximumStack: 100);
		Check(hero.Inventory.TryAcquire(feather, 1150) == InventoryResult.Success, "Stack split failed.");
		Check(hero.Inventory.UsedSlots == 12 && hero.Inventory.Items.Sum(item => item.Quantity) == 1150, "Wrong quantity.");
		Check(hero.Inventory.TryAcquire(feather, 51) == InventoryResult.Full && hero.Inventory.Items.Sum(item => item.Quantity) == 1150, "Failed acquire partially changed stacks.");
		Check(hero.Inventory.TryAcquire(feather, 50) == InventoryResult.Success, "Full bag must still accept partial stack merge.");
		Check(hero.Inventory.Items.All(item => item.Quantity == 100 && item.OwnerId == hero.Id && item.Slot is null), "Invalid stack or ownership.");
		Check(hero.Inventory.Items.Select(item => item.Id).Distinct().Count() == 12, "Duplicate IDs.");
		var other = new PlayerCharacter(Vector2I.Zero);
		Check(other.Inventory.TryEquip(hero.Inventory.Items[0].Id, EquipmentSlot.MainHand) == InventoryResult.NotFound, "Foreign item accepted.");
		var enemy = new BasicEnemy(Vector2I.Zero);
		var helmet = Acquire(enemy, Gear("helmet", EquipmentSlot.Head, 0, 1));
		Check(enemy.Inventory.TryEquip(helmet.Id, EquipmentSlot.Head) == InventoryResult.InvalidSlot, "Unsupported body accepted armor.");
	}

	private static void ExpeditionBoundary()
	{
		var session = new PreparationSession();
		var hero = session.Hero;
		var ring = hero.Inventory.Items.Single(item => item.Definition.Id == "strength-ring");
		Check(hero.Inventory.TryEquip(ring.Id, EquipmentSlot.RingLeft) == InventoryResult.Success, "Preparation failed.");
		var map = session.StartExpedition(1701, new DungeonGenerationOptions());
		Check(ReferenceEquals(hero, map.Player) && ReferenceEquals(map.GetEntityAt(map.PlayerStart), hero), "Expedition replaced hero or broke index.");
		Check(hero.Inventory.Items.Contains(ring) && hero.Inventory.TryUnequip(ring.Id) == InventoryResult.Locked, "Expedition equipment editable.");
		var sword = hero.Inventory.Items.Single(item => item.Definition.Id == "training-sword");
		Check(hero.Inventory.TryEquip(sword.Id, EquipmentSlot.MainHand) == InventoryResult.Locked, "Expedition equip allowed.");
		Check(hero.Inventory.TryAcquire(new("loot", "loot")) == InventoryResult.Success, "Expedition loot blocked.");
	}

	private static void InvalidEquipmentIsAtomic()
	{
		var hero = new PlayerCharacter(Vector2I.Zero);
		var item = Acquire(hero, Gear("overflow", EquipmentSlot.MainHand, 0, 1, int.MaxValue));
		bool rejected = false;
		try { hero.Inventory.TryEquip(item.Id, EquipmentSlot.MainHand); }
		catch (OverflowException) { rejected = true; }
		Check(rejected && item.Location == ItemLocation.Backpack && item.Slot is null
			&& hero.MaxHealth == 10 && hero.Attributes.GetValue(AttributeId.Strength) == 0, "Invalid equipment partially applied.");
	}
}
