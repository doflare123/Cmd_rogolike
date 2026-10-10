using System.Collections.ObjectModel;
using CmdRoguelike.Domain.Stats;

namespace CmdRoguelike.Domain.Items;

/// <summary>Validated immutable properties; inventory alone creates the owned instance.</summary>
public sealed class ItemRoll
{
	public ItemDefinition Definition { get; }
	public ItemRarity Rarity { get; }
	public int ItemLevel { get; }
	public IReadOnlyList<ItemAffix> Affixes { get; }
	public string DisplayName { get; }
	public IReadOnlyDictionary<AttributeId, int> AttributeBonuses { get; }
	public IReadOnlyDictionary<DerivedStatId, int> StatBonuses { get; }

	public ItemRoll(ItemDefinition definition, ItemRarity rarity = ItemRarity.Common, int itemLevel = 1,
		IEnumerable<ItemAffix>? affixes = null, ItemRarityRules? rules = null)
	{
		ArgumentNullException.ThrowIfNull(definition);
		if (itemLevel < 1) throw new ArgumentOutOfRangeException(nameof(itemLevel));
		var limits = (rules ?? ItemRarityRules.Default).Limits(rarity);
		var values = (affixes ?? Array.Empty<ItemAffix>()).ToArray();
		if (values.Any(a => a is null)) throw new ArgumentException("Null affix.");
		if (values.Length < limits.Minimum || values.Count(a => a.Definition.Kind == AffixKind.Prefix) > limits.Prefixes
			|| values.Count(a => a.Definition.Kind == AffixKind.Suffix) > limits.Suffixes
			|| values.Select(a => a.Definition.Group).Distinct(StringComparer.Ordinal).Count() != values.Length
			|| values.Any(a => !a.Definition.Categories.Contains(definition.Category) || a.Tier.MinimumItemLevel > itemLevel)
			|| (definition.Slots.Count == 0 && rarity != ItemRarity.Common))
			throw new ArgumentException("Affixes violate rarity, group, category or item-level rules.");
		Definition = definition; Rarity = rarity; ItemLevel = itemLevel;
		Affixes = Array.AsReadOnly(values.OrderBy(a => a.Definition.Kind).ThenBy(a => a.Definition.Id, StringComparer.Ordinal).ToArray());
		DisplayName = string.Join(" ", Affixes.Where(a => a.Definition.Kind == AffixKind.Prefix)
			.Select(a => a.Definition.NameFor(definition.NameGender)).Append(definition.Name)
			.Concat(Affixes.Where(a => a.Definition.Kind == AffixKind.Suffix).Select(a => a.Definition.Name)));
		AttributeBonuses = Sum(definition.AttributeBonuses,
			Affixes.Where(a => a.Definition.Attribute is not null).Select(a => (a.Definition.Attribute!.Value, a.Value)));
		StatBonuses = Sum(definition.StatBonuses,
			Affixes.Where(a => a.Definition.Stat is not null).Select(a => (a.Definition.Stat!.Value, a.Value)));
	}
	private static IReadOnlyDictionary<T, int> Sum<T>(IReadOnlyDictionary<T, int> basis, IEnumerable<(T Stat, int Value)> affixes)
		where T : struct, Enum
	{
		var result = new Dictionary<T, int>(basis);
		foreach (var (stat, value) in affixes) result[stat] = checked(result.GetValueOrDefault(stat) + value);
		return new ReadOnlyDictionary<T, int>(result);
	}
}
