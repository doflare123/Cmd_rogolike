using System.Collections.ObjectModel;
using CmdRoguelike.Domain.Stats;
using CmdRoguelike.Domain.Combat;

namespace CmdRoguelike.Domain.Items;

public enum EquipmentSlot
{
	Head, Torso, Hands, Legs, Feet, Back, Belt, Amulet,
	RingLeft, RingRight, MainHand, OffHand, Talisman, Backpack,
}

public sealed class BodyPlan
{
	public IReadOnlyList<EquipmentSlot> Slots { get; }
	public static BodyPlan Humanoid { get; } = new(Enum.GetValues<EquipmentSlot>());
	public static BodyPlan None { get; } = new(Array.Empty<EquipmentSlot>());
	public BodyPlan(IEnumerable<EquipmentSlot> slots)
	{
		var values = slots.Distinct().Order().ToArray();
		if (values.Any(slot => !Enum.IsDefined(slot))) throw new ArgumentException("Invalid body slot.");
		Slots = Array.AsReadOnly(values);
	}
}

/// <summary>Immutable equipment/material definition with nonnegative flat bonuses and card grants.</summary>
public sealed class ItemDefinition
{
	public string Id { get; }
	public string Name { get; }
	public int MaximumStack { get; }
	public IReadOnlyList<EquipmentSlot> Slots { get; }
	public IReadOnlyDictionary<AttributeId, int> Requirements { get; }
	public IReadOnlyDictionary<AttributeId, int> AttributeBonuses { get; }
	public IReadOnlyDictionary<DerivedStatId, int> StatBonuses { get; }
	public IReadOnlyList<EquipmentCardGrant> CombatCards { get; }

	public ItemDefinition(string id, string name, int maximumStack = 1,
		IEnumerable<EquipmentSlot>? slots = null,
		IReadOnlyDictionary<AttributeId, int>? requirements = null,
		IReadOnlyDictionary<AttributeId, int>? attributeBonuses = null,
		IReadOnlyDictionary<DerivedStatId, int>? statBonuses = null,
		IEnumerable<EquipmentCardGrant>? combatCards = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		if (maximumStack < 1) throw new ArgumentOutOfRangeException(nameof(maximumStack));
		Id = id;
		Name = name;
		MaximumStack = maximumStack;
		Slots = Array.AsReadOnly((slots ?? Array.Empty<EquipmentSlot>()).Distinct().ToArray());
		if (Slots.Any(slot => !Enum.IsDefined(slot)) || (Slots.Count > 0 && maximumStack != 1))
			throw new ArgumentException("Equipment must be non-stackable and use valid slots.");
		Requirements = Copy(requirements);
		AttributeBonuses = Copy(attributeBonuses);
		StatBonuses = Copy(statBonuses);
		var grants = (combatCards ?? Array.Empty<EquipmentCardGrant>()).ToArray();
		if (grants.Any(grant => grant is null) || (grants.Length > 0 && Slots.Count == 0)
			|| grants.Sum(grant => (long)grant.Copies) > 1000)
			throw new ArgumentException("Cards require non-stackable equipment and a valid deck size.");
		CombatCards = Array.AsReadOnly(grants);
	}

	private static IReadOnlyDictionary<T, int> Copy<T>(IReadOnlyDictionary<T, int>? source) where T : struct, Enum
	{
		var copy = source is null ? new Dictionary<T, int>() : new Dictionary<T, int>(source);
		if (copy.Any(pair => !Enum.IsDefined(pair.Key) || pair.Value < 0))
			throw new ArgumentException("Only valid stats and nonnegative values are supported.");
		return new ReadOnlyDictionary<T, int>(copy);
	}
}
