using CmdRoguelike.Domain.Items;

namespace CmdRoguelike.Generation;

/// <summary>Uses its own RNG stream. No geometry, entity or combat RNG is consumed.</summary>
internal sealed class ItemRollGenerator
{
	private readonly IRandomSource _random;
	private readonly AffixDefinition[] _catalog;
	private readonly ItemRarityRules _rules;
	public ItemRollGenerator(IRandomSource random, IEnumerable<AffixDefinition> catalog, ItemRarityRules? rules = null)
	{
		ArgumentNullException.ThrowIfNull(random);
		ArgumentNullException.ThrowIfNull(catalog);
		_catalog = catalog.ToArray();
		if (_catalog.Any(a => a is null) || _catalog.Select(a => a.Id).Distinct(StringComparer.Ordinal).Count() != _catalog.Length)
			throw new ArgumentException("Affix catalog contains null or duplicate identifiers.");
		_catalog = _catalog.OrderBy(a => a.Id, StringComparer.Ordinal).ToArray();
		_random = random; _rules = rules ?? ItemRarityRules.Default;
	}

	public ItemRoll Roll(ItemDefinition definition, ItemRarity rarity, int itemLevel)
	{
		ArgumentNullException.ThrowIfNull(definition);
		if (itemLevel < 1) throw new ArgumentOutOfRangeException(nameof(itemLevel));
		if (rarity is ItemRarity.Epic or ItemRarity.Legendary or ItemRarity.Unique)
			throw new ArgumentException("Random affix rules for this rarity have not been defined.", nameof(rarity));
		var (minimum, prefixes, suffixes) = _rules.Limits(rarity);
		if (rarity == ItemRarity.Common) return new ItemRoll(definition, rarity, itemLevel, rules: _rules);
		var pool = _catalog.Where(a => a.Categories.Contains(definition.Category)
			&& a.Tiers.Any(t => t.MinimumItemLevel <= itemLevel)).ToArray();
		int maximum = Math.Min(prefixes + suffixes, pool.Select(a => a.Group).Distinct().Count());
		while (maximum >= minimum && !CanComplete(pool, maximum, prefixes, suffixes)) maximum--;
		// Reject before consuming randomness rather than silently generating an invalid rarity.
		if (maximum < minimum) throw new InvalidOperationException("No valid affix combination for this rarity, category and item level.");
		int count = _random.NextInt(minimum, maximum);
		var chosen = new List<ItemAffix>();
		while (chosen.Count < count)
		{
			int remaining = count - chosen.Count - 1;
			var eligible = pool.Where(a => CanPick(a, prefixes, suffixes)
				&& CanComplete(pool.Where(other => other.Group != a.Group).ToArray(), remaining,
					prefixes - (a.Kind == AffixKind.Prefix ? 1 : 0), suffixes - (a.Kind == AffixKind.Suffix ? 1 : 0))).ToArray();
			var affix = eligible[_random.NextInt(0, eligible.Length - 1)];
			var tiers = affix.Tiers.Where(t => t.MinimumItemLevel <= itemLevel).ToArray();
			var tier = tiers[_random.NextInt(0, tiers.Length - 1)];
			chosen.Add(new ItemAffix(affix, tier, _random.NextInt(tier.MinimumValue, tier.MaximumValue)));
			if (affix.Kind == AffixKind.Prefix) prefixes--; else suffixes--;
			pool = pool.Where(other => other.Group != affix.Group).ToArray();
		}
		return new ItemRoll(definition, rarity, itemLevel, chosen, _rules);
	}

	private static bool CanPick(AffixDefinition affix, int prefixes, int suffixes)
		=> affix.Kind == AffixKind.Prefix ? prefixes > 0 : suffixes > 0;

	// Small equipment pools, at most six slots. Look ahead across groups/kinds so a
	// random pick cannot leave an unfillable rare item when a valid combination exists.
	private static bool CanComplete(AffixDefinition[] pool, int count, int prefixes, int suffixes)
	{
		if (count == 0) return true;
		if (count < 0 || count > prefixes + suffixes || count > pool.Length) return false;
		for (int i = 0; i < pool.Length; i++)
		{
			var candidate = pool[i];
			if (!CanPick(candidate, prefixes, suffixes)) continue;
			var rest = pool.Skip(i + 1).Where(a => a.Group != candidate.Group).ToArray();
			if (CanComplete(rest, count - 1, prefixes - (candidate.Kind == AffixKind.Prefix ? 1 : 0),
				suffixes - (candidate.Kind == AffixKind.Suffix ? 1 : 0))) return true;
		}
		return false;
	}
}
