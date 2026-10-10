using CmdRoguelike.Domain.Combat;
using CmdRoguelike.Domain.Items;
using CmdRoguelike.Domain.Rewards;
using CmdRoguelike.Domain.Stats;

namespace CmdRoguelike.Generation;

internal sealed class CombatRewardGenerator
{
	private readonly IRandomSource _random;
	private readonly ItemRollGenerator _items;
	private readonly CombatRewardOptions _options;
	public CombatRewardGenerator(IRandomSource random, CombatRewardOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(random);
		_random = random; _items = new ItemRollGenerator(random, PrototypeAffixes.All);
		_options = options ?? new CombatRewardOptions();
	}
	public CombatReward Generate(CombatVictorySummary summary)
	{
		var budget = CombatRewardRules.Calculate(summary, _options);
		var definition = RewardItemCatalog.Equipment[_random.NextInt(0, RewardItemCatalog.Equipment.Count - 1)];
		int common = (int)Math.Clamp(70L - (long)budget.EquipmentBudget * 2, 10, 70);
		int rare = (int)Math.Clamp(((long)budget.EquipmentBudget - 8) * 2, 0, 50);
		int rarityRoll = _random.NextInt(1, 100);
		var rarity = rarityRoll <= common ? ItemRarity.Common : rarityRoll > 100 - rare ? ItemRarity.Rare : ItemRarity.Uncommon;
		int level = (int)Math.Clamp(1L + budget.EquipmentBudget / 5, 1, 10);
		var items = new List<RewardItem> { new(_items.Roll(definition, rarity, level)) };
		if (budget.HealthSupport) items.Add(new(new ItemRoll(RewardItemCatalog.HealingPotion)));
		if (budget.ManaSupport) items.Add(new(new ItemRoll(RewardItemCatalog.ManaPotion)));
		return new CombatReward(budget, items);
	}
}

/// <summary>Shared definitions keep consumable stacks compatible across encounters.</summary>
internal static class RewardItemCatalog
{
	public static ItemDefinition HealingPotion { get; } = new("healing-potion", "Зелье здоровья", maximumStack: 10,
		category: ItemCategory.Consumable, restoration: new(RestorationResource.Health, 40));
	public static ItemDefinition ManaPotion { get; } = new("mana-potion", "Зелье маны", maximumStack: 10,
		category: ItemCategory.Consumable, restoration: new(RestorationResource.Mana, 40));
	public static IReadOnlyList<ItemDefinition> Equipment { get; } = Array.AsReadOnly(new ItemDefinition[]
	{
		ExpeditionItems.FireTorch,
		new("loot-sword", "Меч", slots: new[] { EquipmentSlot.MainHand }, category: ItemCategory.Weapon,
			combatCards: new[] { new EquipmentCardGrant(new("slash", "Рубящий удар", CombatCardKind.Attack, 1, 2), 3) }),
		new("loot-mace", "Булава", slots: new[] { EquipmentSlot.MainHand }, category: ItemCategory.Weapon,
			nameGender: ItemNameGender.Feminine,
			combatCards: new[] { new EquipmentCardGrant(new("crush", "Сокрушение", CombatCardKind.Attack, 2, 4), 2) }),
		new("loot-armor", "Доспех", slots: new[] { EquipmentSlot.Torso }, category: ItemCategory.Armor,
			statBonuses: new Dictionary<DerivedStatId, int> { [DerivedStatId.Armor] = 2 }),
		new("loot-shield", "Щит", slots: new[] { EquipmentSlot.OffHand }, category: ItemCategory.Shield,
			statBonuses: new Dictionary<DerivedStatId, int> { [DerivedStatId.Armor] = 1 },
			combatCards: new[] { new EquipmentCardGrant(new("shield", "Щит", CombatCardKind.Defense, 1, 200), 2) }),
		new("loot-ring", "Кольцо", slots: new[] { EquipmentSlot.RingLeft, EquipmentSlot.RingRight },
			category: ItemCategory.Jewelry, nameGender: ItemNameGender.Neuter,
			statBonuses: new Dictionary<DerivedStatId, int> { [DerivedStatId.MaxHealth] = 2 }),
	});
}
