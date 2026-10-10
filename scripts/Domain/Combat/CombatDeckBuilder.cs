using CmdRoguelike.Domain.Entities;
using CmdRoguelike.Domain.Items;

namespace CmdRoguelike.Domain.Combat;

/// <summary>An immutable contribution from one equipment definition.</summary>
public sealed record EquipmentCardGrant
{
	public CombatCard Card { get; }
	public int Copies { get; }
	public EquipmentCardGrant(CombatCard card, int copies)
	{
		ArgumentNullException.ThrowIfNull(card);
		if (copies is < 1 or > 1000 || card.SourceItemId is not null)
			throw new ArgumentException("Invalid equipment card grant.");
		Card = card;
		Copies = copies;
	}
}

/// <summary>Shared by preparation and combat. Slot order is stable across newly created heroes.</summary>
public static class CombatDeckBuilder
{
	public static IReadOnlyList<CombatCard> Build(PlayerCharacter hero, CombatOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(hero);
		options ??= new CombatOptions();
		var cards = new List<CombatCard>();
		cards.AddRange(Enumerable.Repeat(options.Attack, options.AttackCards));
		cards.AddRange(Enumerable.Repeat(options.Defense, options.DefenseCards));
		foreach (var item in hero.Inventory.Items.Where(item => item.Location == ItemLocation.Equipment
			&& item.State == EquipmentState.Active).OrderBy(item => item.Slot))
			foreach (var grant in item.Definition.CombatCards)
			{
				cards.AddRange(Enumerable.Repeat(grant.Card.FromEquipment(item.Id, item.Definition.Name), grant.Copies));
			}
		return cards.AsReadOnly();
	}

	public static bool IsAvailable(PlayerCharacter hero, CombatCard card)
		=> card.SourceItemId is not Guid id || hero.Inventory.Items.Any(item => item.Id == id
			&& item.OwnerId == hero.Id && item.Location == ItemLocation.Equipment && item.State == EquipmentState.Active);
}
