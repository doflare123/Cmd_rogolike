using CmdRoguelike.Domain.Combat;
using CmdRoguelike.Domain.Entities;
using CmdRoguelike.Domain.Items;
using CmdRoguelike.Domain.Stats;
using CmdRoguelike.Generation;
using CmdRoguelike.Presentation;
using CmdRoguelike.World;
using Godot;

namespace CmdRoguelike.Tests;

public partial class ItemAffixSmokeTest : Node
{
	public override async void _Ready()
	{
		try
		{
			GenerationAndLimits();
			GroupConflictsAndFailure();
			InstanceStatsAndDormant();
			ValidationAndCapacity();
			RarityPresentation();
			await CheckUi();
			GD.Print("Item affix smoke test passed: 900 seeded rolls, tier gates, group conflicts, immutable instances, Dormant, cards, capacity and UI.");
			GetTree().Quit();
		}
		catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
	}
	private static void Check(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
	private static void Reject(Action action, string message)
	{
		try { action(); }
		catch (ArgumentException) { return; }
		throw new InvalidOperationException(message);
	}
	private static ItemDefinition Armor(int requirement = 0) => new("armor", "Доспех", slots: new[] { EquipmentSlot.Torso },
		requirements: new Dictionary<AttributeId, int> { [AttributeId.Strength] = requirement },
		statBonuses: new Dictionary<DerivedStatId, int> { [DerivedStatId.Armor] = 2 }, category: ItemCategory.Armor,
		combatCards: new[] { new EquipmentCardGrant(new("guard", "Защита предмета", CombatCardKind.Defense, 1, 200), 1) });
	private static AffixDefinition Strength => PrototypeAffixes.All.Single(a => a.Attribute == AttributeId.Strength);
	private static string Signature(ItemRoll roll) => $"{roll.Rarity}:{roll.ItemLevel}:{roll.DisplayName}:"
		+ string.Join(';', roll.Affixes.Select(a => $"{a.Definition.Id}/{a.Tier.Tier}/{a.Value}"));
	private static ItemInstance Acquire(PlayerCharacter hero, ItemRoll roll)
	{
		Check(hero.Inventory.TryAcquireRolled(roll) == InventoryResult.Success, "Roll acquisition failed.");
		return hero.Inventory.Items.Last();
	}

	private static void GenerationAndLimits()
	{
		var definition = Armor();
		var signatures = new HashSet<string>();
		for (int seed = 0; seed < 100; seed++)
		{
			var first = new ItemRollGenerator(new GodotRandomSource(seed), PrototypeAffixes.All);
			var second = new ItemRollGenerator(new GodotRandomSource(seed), PrototypeAffixes.All.Reverse());
			foreach (int level in new[] { 1, 5, 10 })
				foreach (var rarity in new[] { ItemRarity.Common, ItemRarity.Uncommon, ItemRarity.Rare })
				{
					var roll = first.Roll(definition, rarity, level);
					Check(Signature(roll) == Signature(second.Roll(definition, rarity, level)), "RNG depends on catalog order/identity.");
					var limits = ItemRarityRules.Default.Limits(rarity);
					Check(roll.Affixes.Count >= limits.Minimum
						&& roll.Affixes.Count(a => a.Definition.Kind == AffixKind.Prefix) <= limits.Prefixes
						&& roll.Affixes.Count(a => a.Definition.Kind == AffixKind.Suffix) <= limits.Suffixes, "Rarity limits broken.");
					Check(roll.Affixes.Select(a => a.Definition.Group).Distinct().Count() == roll.Affixes.Count, "Affix group repeated.");
					Check(roll.Affixes.All(a => a.Definition.Categories.Contains(definition.Category)
						&& a.Tier.MinimumItemLevel <= level && a.Value >= a.Tier.MinimumValue && a.Value <= a.Tier.MaximumValue), "Invalid category/tier/value.");
					if (rarity == ItemRarity.Common) Check(roll.Affixes.Count == 0 && roll.StatBonuses[DerivedStatId.Armor] == 2, "Common item changed.");
					signatures.Add(Signature(roll));
				}
		}
		Check(signatures.Count > 100 && definition.StatBonuses[DerivedStatId.Armor] == 2 && definition.AttributeBonuses.Count == 0, "Rolls mutate content or lack variety.");
		var maximum = new ItemRollGenerator(new ExtremeRandom(), new[] { PrototypeAffixes.Armor, Strength });
		var low = maximum.Roll(definition, ItemRarity.Uncommon, 1);
		var high = maximum.Roll(definition, ItemRarity.Uncommon, 10);
		Check(low.DisplayName == "Крепкий Доспех силы" && low.Affixes.All(a => a.Tier.Tier == 1 && a.Value == 2), "Fixed low-level roll incorrect.");
		Check(high.Affixes.All(a => a.Tier.Tier == 3 && a.Value == 6), "Higher tiers cannot roll.");
		var rules = new ItemRarityRules(3, 3, 6);
		var six = new ItemRollGenerator(new ExtremeRandom(), PrototypeAffixes.All, rules).Roll(definition, ItemRarity.Rare, 10);
		Check(six.Affixes.Count == 6, "Configurable rare limits ignored.");
	}

	private static void GroupConflictsAndFailure()
	{
		AffixDefinition Make(string id, string group, AffixKind kind) => new(id, group, kind, id,
			new[] { ItemCategory.Armor }, new[] { new AffixTier(1, 1, 1, 1) }, attribute: AttributeId.Strength);
		// Picking suffix shared first would make the three-affix result impossible.
		var catalog = new[] { Make("a", "shared", AffixKind.Suffix), Make("b", "shared", AffixKind.Prefix),
			Make("c", "second", AffixKind.Suffix), Make("d", "third", AffixKind.Suffix) };
		for (int seed = 0; seed < 20; seed++)
		{
			var roll = new ItemRollGenerator(new GodotRandomSource(seed), catalog).Roll(Armor(), ItemRarity.Rare, 1);
			Check(roll.Affixes.Count == 3 && roll.Affixes.Any(a => a.Definition.Id == "b"), "Greedy group selection prevented a feasible roll.");
		}
		var random = new ExtremeRandom();
		var generator = new ItemRollGenerator(random, new[] { PrototypeAffixes.Armor });
		bool failed = false;
		try { generator.Roll(new("weapon", "Меч", slots: new[] { EquipmentSlot.MainHand }, category: ItemCategory.Weapon), ItemRarity.Uncommon, 1); }
		catch (InvalidOperationException) { failed = true; }
		Check(failed && random.Calls == 0, "Impossible roll consumed RNG or ignored category restriction.");
		Check(generator.Roll(new("material", "Перо", maximumStack: 100), ItemRarity.Common, 1).Affixes.Count == 0 && random.Calls == 0, "Common material consumed RNG.");
	}

	private static void InstanceStatsAndDormant()
	{
		var definition = Armor(3);
		var roll = new ItemRoll(definition, ItemRarity.Uncommon, 1, new[] {
			new ItemAffix(PrototypeAffixes.Armor, PrototypeAffixes.Armor.Tiers[0], 2), new ItemAffix(Strength, Strength.Tiers[0], 2) });
		var hero = new PlayerCharacter(Vector2I.Zero);
		var item = Acquire(hero, roll);
		var sibling = Acquire(hero, new ItemRoll(definition));
		Check(item.Id != sibling.Id && sibling.StatBonuses[DerivedStatId.Armor] == 2 && item.StatBonuses[DerivedStatId.Armor] == 4, "Instance rolls leaked into base/sibling.");
		Check(hero.Inventory.TryEquip(item.Id, EquipmentSlot.Torso) == InventoryResult.RequirementsNotMet, "Affix bootstrapped first equip.");
		hero.Attributes.AddModifier(AttributeId.Strength, new("support", StatModifierOperation.Flat, 3));
		Check(hero.Inventory.TryEquip(item.Id, EquipmentSlot.Torso) == InventoryResult.Success
			&& hero.DerivedStats.GetValue(DerivedStatId.Armor) == 4 && hero.Attributes.GetValue(AttributeId.Strength) == 5, "Affix equipment bonuses absent.");
		var card = CombatDeckBuilder.Build(hero).Single(c => c.SourceItemId == item.Id);
		Check(card.SourceName == item.DisplayName && CombatRules.DefenseBlock(hero, card) == 8, "Card source/armor ignores affixes.");
		hero.Attributes.RemoveModifiersFromSource("support");
		Check(item.State == EquipmentState.Dormant && item.Slot == EquipmentSlot.Torso
			&& hero.DerivedStats.GetValue(DerivedStatId.Armor) == 0 && hero.Attributes.GetValue(AttributeId.Strength) == 0
			&& !CombatDeckBuilder.IsAvailable(hero, card), "Dormant retained affix bonuses/cards or released slot.");
		hero.Attributes.AddModifier(AttributeId.Strength, new("insufficient", StatModifierOperation.Flat, 1));
		Check(item.State == EquipmentState.Dormant, "Dormant affix reactivated with its own strength.");
		hero.Attributes.AddModifier(AttributeId.Strength, new("restore", StatModifierOperation.Flat, 2));
		Check(item.State == EquipmentState.Active && CombatDeckBuilder.IsAvailable(hero, card) && ReferenceEquals(item.Roll, roll), "Reactivation rerolled or lost source.");
		hero.Attributes.RemoveModifiersFromSource("restore");
		Check(item.State == EquipmentState.Active && hero.Attributes.GetValue(AttributeId.Strength) == 3, "Active affix cannot support itself.");
		Check(hero.Inventory.TryUnequip(item.Id) == InventoryResult.Success && hero.Inventory.TryEquip(item.Id, EquipmentSlot.Torso) == InventoryResult.RequirementsNotMet,
			"Unequipped affixes still give stats.");
		Check(Signature(item.Roll) == Signature(roll), "Equip cycle changed roll.");

		var healthy = new ItemRoll(Armor(), ItemRarity.Uncommon, affixes: new[] { new ItemAffix(PrototypeAffixes.Health, PrototypeAffixes.Health.Tiers[0], 4) });
		var healthItem = Acquire(hero, healthy);
		hero.TakeDamage(3);
		hero.Inventory.TryEquip(healthItem.Id, EquipmentSlot.Torso);
		Check(hero.MaxHealth == 14 && hero.Health == 7, "Health affix healed on equip.");
		hero.RestoreHealth(100);
		hero.Inventory.TryUnequip(healthItem.Id);
		Check(hero.MaxHealth == 10 && hero.Health == 10, "Health affix removal failed to clamp.");
	}

	private static void ValidationAndCapacity()
	{
		var definition = Armor();
		var armor = new ItemAffix(PrototypeAffixes.Armor, PrototypeAffixes.Armor.Tiers[0], 1);
		Reject(() => new ItemRoll(definition, affixes: new[] { armor }), "Common affix accepted.");
		Reject(() => new ItemRoll(definition, ItemRarity.Uncommon, affixes: new[] { armor, armor }), "Duplicate group accepted.");
		Reject(() => new ItemRoll(definition, ItemRarity.Rare, affixes: new[] { armor }), "Underfilled rare accepted.");
		Reject(() => new ItemRoll(definition, ItemRarity.Uncommon, 1, new[] { new ItemAffix(PrototypeAffixes.Armor, PrototypeAffixes.Armor.Tiers[2], 5) }), "High tier accepted on low item level.");
		Reject(() => new ItemAffix(PrototypeAffixes.Armor, PrototypeAffixes.Armor.Tiers[0], 3), "Out-of-range value accepted.");
		Reject(() => new ItemRarityRules(0, 3), "Invalid rare policy accepted.");
		Reject(() => new AffixDefinition("ap", "ap", AffixKind.Prefix, "AP", new[] { ItemCategory.Armor },
			new[] { new AffixTier(1, 1, 1, 2) }, stat: DerivedStatId.MaxActionPoints), "Unsupported AP affix accepted.");
		var full = new PlayerCharacter(Vector2I.Zero);
		var filler = new ItemDefinition("filler", "Материал");
		full.Inventory.TryAcquire(filler, 11);
		var roll = new ItemRoll(definition, ItemRarity.Uncommon, affixes: new[] { armor });
		Check(full.Inventory.TryAcquireRolled(roll, 2) == InventoryResult.Full && full.Inventory.Items.Count == 11, "Roll batch partially acquired.");
		full.Inventory.TryAcquireRolled(roll);
		Check(full.Inventory.TryAcquireRolled(roll) == InventoryResult.Full && full.Inventory.Items.Last().Quantity == 1, "Affixed equipment stacked.");
		var other = new PlayerCharacter(Vector2I.Zero);
		Check(other.Inventory.TryEquip(full.Inventory.Items.Last().Id, EquipmentSlot.Torso) == InventoryResult.NotFound, "Foreign roll equip allowed.");
		var material = new ItemDefinition("feather", "Перо", maximumStack: 100);
		other.Inventory.TryAcquire(material, 10);
		other.Inventory.TryAcquireRolled(new ItemRoll(material, itemLevel: 2), 10);
		Check(other.Inventory.UsedSlots == 2, "Different item levels merged into one stack.");
		bool overflow = false;
		try { _ = new ItemRoll(new("overflow", "Overflow", slots: new[] { EquipmentSlot.Torso }, category: ItemCategory.Armor,
			statBonuses: new Dictionary<DerivedStatId, int> { [DerivedStatId.Armor] = int.MaxValue }), ItemRarity.Uncommon, affixes: new[] { armor }); }
		catch (OverflowException) { overflow = true; }
		Check(overflow, "Affix addition overflow accepted.");
	}

	private async Task CheckUi()
	{
		var session = new PreparationSession();
		Check(session.Hero.Inventory.UsedSlots == 9, "Missing affix samples.");
		var panel = new InventoryPanel(session.Hero, () => { }, () => { });
		AddChild(panel);
		var bag = session.Hero.Inventory.Items.ToArray();
		for (int i = 0; i < bag.Length - 1; i++) panel.HandleKey(new InputEventKey { Pressed = true, Keycode = Key.Down });
		var sample = bag.Last();
		Check(panel.ScreenText.Contains(sample.DisplayName) && panel.ScreenText.Contains("Редкий | Уровень предмета: 5")
			&& panel.ScreenText.Contains("Итого:"), "UI lacks rarity, level or rolled name/bonuses.");
		Check(!panel.ScreenText.Contains("Базовые бонусы:")
			&& !panel.ScreenText.Contains("T1:") && !panel.ScreenText.Contains("T2:"), "Inventory exposes sources without Alt.");
		if (DisplayServer.GetName() != "headless") await Capture("res://.godot/inventory-default.png");
		panel.HandleKey(new InputEventKey { Pressed = true, Keycode = Key.Alt });
		Check(panel.ScreenText.Contains("Базовые бонусы:"), "Holding Alt does not reveal item base.");
		foreach (var affix in sample.Affixes)
			Check(panel.ScreenText.Contains($"T{affix.Tier.Tier}: +{affix.Value}"), "UI omitted saved tier/value.");
		if (DisplayServer.GetName() != "headless")
		{
			await Capture("res://.godot/affix-ui.png");
			GetWindow().Size = new Vector2I(800, 600);
			await Capture("res://.godot/affix-ui-small.png");
		}
		panel.HandleKey(new InputEventKey { Pressed = false, Keycode = Key.Alt });
		Check(!panel.ScreenText.Contains("Базовые бонусы:") && !panel.ScreenText.Contains("T1:")
			&& !panel.ScreenText.Contains("T2:"), "Alt release did not hide source details.");
		panel.HandleKey(new InputEventKey { Pressed = true, Keycode = Key.Enter });
		Check(sample.State == EquipmentState.Active && sample.Location == ItemLocation.Equipment, "Sample cannot equip: " + panel.Status);
		panel.HandleKey(new InputEventKey { Pressed = true, Keycode = Key.Tab });
		for (int i = 0; i < session.Hero.Inventory.Body.Slots.TakeWhile(slot => slot != EquipmentSlot.OffHand).Count(); i++)
			panel.HandleKey(new InputEventKey { Pressed = true, Keycode = Key.Down });
		Check(panel.ScreenText.Contains(sample.DisplayName), "Equipped sample lost its displayed name.");
		Check(CombatDeckBuilder.Build(session.Hero).Any(c => c.SourceName == sample.DisplayName), "Sample cards missing full rolled source.");
		Check(new PreparationSession().Hero.Inventory.Items.Select(i => Signature(i.Roll)).SequenceEqual(bag.Select(i => Signature(i.Roll))), "Debug preparation rolls changed on reset.");
		GD.Print("Affix samples: " + string.Join(" | ", bag.Where(i => i.Rarity != ItemRarity.Common).Select(i => Signature(i.Roll))));
		panel.QueueFree();
		var paletteHero = new PlayerCharacter(Vector2I.Zero);
		foreach (var rarity in Enum.GetValues<ItemRarity>())
		{
			var definition = new ItemDefinition("palette-" + rarity, ItemRarityStyle.Name(rarity) + " меч",
				slots: new[] { EquipmentSlot.MainHand }, category: ItemCategory.Weapon);
			var roll = rarity is ItemRarity.Uncommon or ItemRarity.Rare
				? new ItemRollGenerator(new GodotRandomSource(17), PrototypeAffixes.All).Roll(definition, rarity, 1)
				: new ItemRoll(definition, rarity);
			Acquire(paletteHero, roll);
		}
		var palette = new InventoryPanel(paletteHero, () => { }, () => { }); AddChild(palette);
		if (DisplayServer.GetName() != "headless") await Capture("res://.godot/rarity-colors.png");
		palette.QueueFree();
	}
	private static void RarityPresentation()
	{
		Check(ItemRarityStyle.Color(ItemRarity.Common) == Colors.White, "Common must be white.");
		Check(ItemRarityStyle.Color(ItemRarity.Rare) == new Color("65d879"), "Rare must be green.");
		Check(ItemRarityStyle.Color(ItemRarity.Uncommon) == new Color("619fff"), "Uncommon must be blue.");
		Check(ItemRarityStyle.Color(ItemRarity.Epic) == new Color("bd7aff"), "Epic must be violet.");
		Check(ItemRarityStyle.Color(ItemRarity.Legendary) == new Color("ffe05c"), "Legendary must be yellow.");
		Check(ItemRarityStyle.Color(ItemRarity.Unique, 0) == new Color("830d18")
			&& ItemRarityStyle.Color(ItemRarity.Unique, 1) == new Color("ffb52e")
			&& ItemRarityStyle.Color(ItemRarity.Unique, 2) == ItemRarityStyle.Color(ItemRarity.Unique, 0), "Unique color does not cycle.");
		foreach (var rarity in new[] { ItemRarity.Epic, ItemRarity.Legendary, ItemRarity.Unique })
		{
			var roll = new ItemRoll(Armor(), rarity);
			Check(roll.Rarity == rarity && ItemRarityStyle.Name(rarity).Length > 0, "Authored rarity is not supported.");
			Reject(() => new ItemRollGenerator(new ExtremeRandom(), PrototypeAffixes.All).Roll(Armor(), rarity, 1), "Undefined random rarity silently generated.");
		}
	}
	private async Task Capture(string path)
	{
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		GetViewport().GetTexture().GetImage().SavePng(path);
	}
	private sealed class ExtremeRandom : IRandomSource
	{
		public int Calls { get; private set; }
		public int NextInt(int minimum, int maximum)
		{
			Check(minimum <= maximum, "Invalid RNG bounds."); Calls++; return maximum;
		}
		public float NextFloat() => throw new InvalidOperationException("Unexpected floating-point RNG call.");
	}
}
