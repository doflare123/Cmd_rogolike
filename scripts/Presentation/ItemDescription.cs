using CmdRoguelike.Domain.Combat;
using CmdRoguelike.Domain.Items;
using CmdRoguelike.Domain.Stats;

namespace CmdRoguelike.Presentation;

internal static class ItemDescription
{
	internal static string Bonuses(IReadOnlyDictionary<AttributeId, int> attributes, IReadOnlyDictionary<DerivedStatId, int> stats)
	{
		string text = string.Join(", ", attributes.Select(p => $"+{p.Value} {InventoryPanel.AttributeName(p.Key)}")
			.Concat(stats.Select(p => $"+{p.Value} {InventoryPanel.StatName(p.Key)}")));
		return text;
	}
	internal static string Cards(ItemDefinition item) => string.Join("; ", item.CombatCards.Select(Card));
	internal static string Card(EquipmentCardGrant g) => $"{g.Card.Name} x{g.Copies}: " + Effect(g.Card);
	internal static string Effect(CombatCard card) => $"{card.ActionPointCost} AP, " + (card.Kind == CombatCardKind.Attack
		? $"{card.Power} {(card.DamageAspect == DamageAspect.Fire ? "огненного" : "физического")} урона" : $"блок {card.Power}% брони");
}
