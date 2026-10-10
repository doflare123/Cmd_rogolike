using CmdRoguelike.Domain.Items;
using CmdRoguelike.Generation;

namespace CmdRoguelike.World;

/// <summary>Stable content identifiers, shared between saves, loot and the discovery book.</summary>
internal static class ContentCatalog
{
	public static IReadOnlyList<ItemDefinition> All { get; } = Array.AsReadOnly(PrototypeItems.StartingItems.Select(i => i.Definition)
		.Concat(PrototypeItems.AffixSamples.Select(i => i.Definition)).Concat(RewardItemCatalog.Equipment)
		.Concat(new[] { ExpeditionItems.FireTorch, RewardItemCatalog.HealingPotion, RewardItemCatalog.ManaPotion })
		.DistinctBy(i => i.Id, StringComparer.Ordinal)
		.OrderBy(i => i.Id, StringComparer.Ordinal).ToArray());
	public static ItemDefinition Find(string id) => All.FirstOrDefault(i => i.Id == id)
		?? throw new ArgumentException($"Unknown saved item: {id}");
}
