using CmdRoguelike.Domain.Entities;
using CmdRoguelike.Domain.Stats;

namespace CmdRoguelike.Domain.Combat;

public static class CombatRules
{
	public static int DefenseBlock(Actor actor, CombatCard card)
		=> card.Kind == CombatCardKind.Defense
			? checked((int)((long)actor.DerivedStats.GetValue(DerivedStatId.Armor) * card.Power / 100)) : 0;
}
