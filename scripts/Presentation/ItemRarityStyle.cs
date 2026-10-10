using CmdRoguelike.Domain.Items;
using Godot;

namespace CmdRoguelike.Presentation;

/// <summary>Shared palette; animation uses presentation time and never item RNG.</summary>
internal static class ItemRarityStyle
{
	internal static Color Color(ItemRarity rarity, double? seconds = null) => rarity switch
	{
		ItemRarity.Common => Colors.White,
		ItemRarity.Rare => new Color("65d879"),
		ItemRarity.Uncommon => new Color("619fff"),
		ItemRarity.Epic => new Color("bd7aff"),
		ItemRarity.Legendary => new Color("ffe05c"),
		ItemRarity.Unique => new Color("830d18").Lerp(new Color("ffb52e"),
			(float)(0.5 - 0.5 * Math.Cos((seconds ?? Time.GetTicksMsec() / 1000.0) * Math.PI))),
		_ => throw new ArgumentOutOfRangeException(nameof(rarity)),
	};
	internal static string Name(ItemRarity rarity) => rarity switch
	{
		ItemRarity.Common => "Обычный", ItemRarity.Rare => "Редкий", ItemRarity.Uncommon => "Необычный",
		ItemRarity.Epic => "Эпический", ItemRarity.Legendary => "Легендарный", ItemRarity.Unique => "Уникальный",
		_ => throw new ArgumentOutOfRangeException(nameof(rarity)),
	};
	internal static string Code(ItemRarity rarity) => rarity switch
	{
		ItemRarity.Common => "O", ItemRarity.Rare => "R", ItemRarity.Uncommon => "N",
		ItemRarity.Epic => "E", ItemRarity.Legendary => "L", ItemRarity.Unique => "U",
		_ => throw new ArgumentOutOfRangeException(nameof(rarity)),
	};
}
