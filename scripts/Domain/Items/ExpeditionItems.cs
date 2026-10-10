using CmdRoguelike.Domain.Combat;

namespace CmdRoguelike.Domain.Items;

public static class ExpeditionItems
{
	public static ItemDefinition FireTorch { get; } = new("fire-torch", "Огненный факел",
		slots: new[] { EquipmentSlot.MainHand }, category: ItemCategory.Weapon,
		combatCards: new[] { new EquipmentCardGrant(new("flame", "Язык пламени", CombatCardKind.Attack, 1, 3, DamageAspect.Fire), 3) });
}
