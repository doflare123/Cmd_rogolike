using CmdRoguelike.Domain.Stats;

namespace CmdRoguelike.Domain.Items;

public enum ItemRarity { Common, Uncommon, Rare, Epic, Legendary, Unique }
public enum AffixKind { Prefix, Suffix }

/// <summary>Rarity limits affix count; it never multiplies item stats.</summary>
public sealed class ItemRarityRules
{
	public static ItemRarityRules Default { get; } = new();
	public int RarePrefixes { get; }
	public int RareSuffixes { get; }
	public int RareMinimum { get; }
	public ItemRarityRules(int rarePrefixes = 2, int rareSuffixes = 2, int rareMinimum = 3)
	{
		if (rarePrefixes is < 1 or > 3 || rareSuffixes is < 1 or > 3
			|| rareMinimum < 3 || rareMinimum > rarePrefixes + rareSuffixes)
			throw new ArgumentOutOfRangeException(nameof(rareMinimum), "Invalid rare affix limits.");
		RarePrefixes = rarePrefixes; RareSuffixes = rareSuffixes; RareMinimum = rareMinimum;
	}
	public (int Minimum, int Prefixes, int Suffixes) Limits(ItemRarity rarity) => rarity switch
	{
		ItemRarity.Common => (0, 0, 0),
		ItemRarity.Uncommon => (1, 1, 1),
		ItemRarity.Rare => (RareMinimum, RarePrefixes, RareSuffixes),
		// Authored content may use these rarities; random drop rules are not defined yet.
		ItemRarity.Epic or ItemRarity.Legendary or ItemRarity.Unique => (0, 3, 3),
		_ => throw new ArgumentOutOfRangeException(nameof(rarity)),
	};
}

public sealed record AffixTier
{
	public int Tier { get; }
	public int MinimumItemLevel { get; }
	public int MinimumValue { get; }
	public int MaximumValue { get; }
	public AffixTier(int tier, int minimumItemLevel, int minimumValue, int maximumValue)
	{
		if (tier < 1 || minimumItemLevel < 1 || minimumValue < 1 || maximumValue < minimumValue)
			throw new ArgumentOutOfRangeException(nameof(tier), "Invalid affix tier or value range.");
		Tier = tier; MinimumItemLevel = minimumItemLevel;
		MinimumValue = minimumValue; MaximumValue = maximumValue;
	}
}

/// <summary>Immutable single-stat affix content, independent of any rolled item.</summary>
public sealed class AffixDefinition
{
	public string Id { get; }
	public string Group { get; }
	public AffixKind Kind { get; }
	public string Name { get; }
	public string FeminineName { get; }
	public string NeuterName { get; }
	public AttributeId? Attribute { get; }
	public DerivedStatId? Stat { get; }
	public IReadOnlyList<ItemCategory> Categories { get; }
	public IReadOnlyList<AffixTier> Tiers { get; }
	public AffixDefinition(string id, string group, AffixKind kind, string name,
		IEnumerable<ItemCategory> categories, IEnumerable<AffixTier> tiers,
		AttributeId? attribute = null, DerivedStatId? stat = null,
		string? feminineName = null, string? neuterName = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		ArgumentException.ThrowIfNullOrWhiteSpace(group);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(categories);
		ArgumentNullException.ThrowIfNull(tiers);
		var categoryCopy = categories.Distinct().Order().ToArray();
		var tierCopy = tiers.ToArray();
		if (tierCopy.Any(tier => tier is null)) throw new ArgumentException("Null affix tier.");
		tierCopy = tierCopy.OrderBy(tier => tier.Tier).ToArray();
		if (!Enum.IsDefined(kind) || (attribute is null) == (stat is null)
			|| (attribute is AttributeId a && !Enum.IsDefined(a))
			|| (stat is DerivedStatId s && (!Enum.IsDefined(s) || s == DerivedStatId.MaxActionPoints))
			|| categoryCopy.Length == 0 || categoryCopy.Any(c => !Enum.IsDefined(c) || c is ItemCategory.Material or ItemCategory.Consumable)
			|| tierCopy.Length == 0 || tierCopy.Select(t => t.Tier).Distinct().Count() != tierCopy.Length)
			throw new ArgumentException("Affix needs one supported stat, valid equipment categories and unique tiers.");
		if (feminineName is not null) ArgumentException.ThrowIfNullOrWhiteSpace(feminineName);
		if (neuterName is not null) ArgumentException.ThrowIfNullOrWhiteSpace(neuterName);
		Id = id; Group = group; Kind = kind; Name = name;
		FeminineName = feminineName ?? name; NeuterName = neuterName ?? name;
		Attribute = attribute; Stat = stat;
		Categories = Array.AsReadOnly(categoryCopy); Tiers = Array.AsReadOnly(tierCopy);
	}
	public string NameFor(ItemNameGender gender) => gender switch
	{
		ItemNameGender.Feminine => FeminineName,
		ItemNameGender.Neuter => NeuterName,
		_ => Name,
	};
}

/// <summary>Actual tier and value are chosen once, never while rendering/equipping.</summary>
public sealed record ItemAffix
{
	public AffixDefinition Definition { get; }
	public AffixTier Tier { get; }
	public int Value { get; }
	public ItemAffix(AffixDefinition definition, AffixTier tier, int value)
	{
		ArgumentNullException.ThrowIfNull(definition);
		ArgumentNullException.ThrowIfNull(tier);
		if (!definition.Tiers.Contains(tier) || value < tier.MinimumValue || value > tier.MaximumValue)
			throw new ArgumentException("Roll is outside this affix's tier/value range.");
		Definition = definition; Tier = tier; Value = value;
	}
}
