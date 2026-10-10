using CmdRoguelike.Domain.Stats;

namespace CmdRoguelike.Domain.Items;

/// <summary>Prototype content/ranges, not the final drop table or item balance.</summary>
internal static class PrototypeAffixes
{
	private static readonly ItemCategory[] Equipment =
		{ ItemCategory.Equipment, ItemCategory.Weapon, ItemCategory.Armor, ItemCategory.Shield, ItemCategory.Jewelry };
	private static readonly AffixTier[] AttributeTiers = { new(1, 1, 1, 2), new(2, 5, 3, 4), new(3, 10, 5, 6) };
	private static readonly AffixTier[] ResourceTiers = { new(1, 1, 2, 4), new(2, 5, 5, 7), new(3, 10, 8, 10) };
	public static AffixDefinition Armor { get; } = new("sturdy", "armor", AffixKind.Prefix, "Крепкий",
		new[] { ItemCategory.Armor, ItemCategory.Shield }, AttributeTiers, stat: DerivedStatId.Armor,
		feminineName: "Крепкая", neuterName: "Крепкое");
	public static AffixDefinition Health { get; } = new("healthy", "health", AffixKind.Prefix, "Живучий",
		Equipment, ResourceTiers, stat: DerivedStatId.MaxHealth, feminineName: "Живучая", neuterName: "Живучее");
	public static AffixDefinition Mana { get; } = new("mystic", "mana", AffixKind.Prefix, "Мистический",
		Equipment, ResourceTiers, stat: DerivedStatId.MaxMana, feminineName: "Мистическая", neuterName: "Мистическое");
	public static IReadOnlyList<AffixDefinition> All { get; } = Array.AsReadOnly(new[] { Armor, Health, Mana }
		.Concat(Enum.GetValues<AttributeId>().Select(attribute => new AffixDefinition(
			"attribute-" + attribute, "attribute-" + attribute, AffixKind.Suffix, attribute switch
			{
				AttributeId.Strength => "силы", AttributeId.Dexterity => "ловкости",
				AttributeId.Constitution => "стойкости", AttributeId.Intelligence => "разума",
				AttributeId.Wisdom => "мудрости", AttributeId.Willpower => "воли",
				AttributeId.Perception => "зоркости", AttributeId.Luck => "удачи", _ => throw new ArgumentOutOfRangeException(),
			}, Equipment, AttributeTiers, attribute: attribute))).ToArray());
}
