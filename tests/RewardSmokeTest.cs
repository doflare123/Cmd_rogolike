using CmdRoguelike.Core;
using CmdRoguelike.Domain.Combat;
using CmdRoguelike.Domain.Entities;
using CmdRoguelike.Domain.Items;
using CmdRoguelike.Domain.Rewards;
using CmdRoguelike.Domain.Stats;
using CmdRoguelike.Generation;
using CmdRoguelike.Presentation;
using CmdRoguelike.World;
using Godot;

namespace CmdRoguelike.Tests;

public partial class RewardSmokeTest : Node
{
	private static void Check(bool condition, string message) => CombatSmokeTest.Check(condition, message);
	private static CombatVictorySummary Summary(int turns = 1, int health = 10, int mana = 10, int maxMana = 10) => new(
		new[] { new RewardEnemy(8, 4, EnemyRewardRole.Defender), new RewardEnemy(11, 5, EnemyRewardRole.Heavy) }, turns, health, 10, mana, maxMana);
	private static string Signature(CombatReward reward) => string.Join('|', reward.Items.Select(i =>
		$"{i.Roll.Definition.Id}/{i.Roll.Rarity}/{i.Roll.ItemLevel}/" + string.Join(';', i.Roll.Affixes.Select(a => $"{a.Definition.Id}:{a.Tier.Tier}:{a.Value}"))));
	public override async void _Ready()
	{
		try
		{
			BudgetAndGeneration();
			SnapshotAndConsumables();
			WorldOwnershipAndIsolation();
			await Presentation();
			GD.Print("Reward smoke test passed: difficulty/count/tempo budgets, bounded resource help, seeded single-item rewards, snapshots, ownership/full bag, consumables, isolation, sequential roulette/skip/settings/UI.");
			GetTree().Quit();
		}
		catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
	}
	private static void BudgetAndGeneration()
	{
		var healthy = CombatRewardRules.Calculate(Summary());
		var low = CombatRewardRules.Calculate(Summary(health: 1, mana: 0));
		Check(healthy.DifficultyBudget == 24 && healthy.EnemyCount == 2, "Group/difficulty/synergy not counted.");
		Check(healthy.EfficiencyBonus > 0 && healthy.EfficiencyBonus <= healthy.DifficultyBudget * 20 / 100, "Speed bonus is absent or unbounded.");
		Check(low.HealthSupport && low.ManaSupport && low.SupportPercent == 24 && low.EquipmentBudget < healthy.EquipmentBudget
			&& (long)low.EquipmentBudget * 100 >= (long)low.TotalBudget * 76, "Resource help failed or reduced quality too much.");
		Check(!CombatRewardRules.Calculate(Summary(mana: 0, maxMana: 0)).ManaSupport, "Mana-less hero received mana help.");
		var hpOnly = CombatRewardRules.Calculate(Summary(health: 3));
		Check(hpOnly.HealthSupport && !hpOnly.ManaSupport && hpOnly.SupportPercent == 12, "Health-only help incorrect.");
		Check(!CombatRewardRules.Calculate(Summary(health: 4, mana: 4)).HealthSupport, "Threshold ignored.");
		Check(CombatRewardRules.Calculate(Summary(turns: 100)).TotalBudget == CombatRewardRules.Calculate(Summary(turns: 101)).TotalBudget,
			"Stalling increased reward.");
		var single = CombatRewardRules.Calculate(new(new[] { new RewardEnemy(5, 3, EnemyRewardRole.Attacker) }, 1, 10, 10, 0, 0));
		Check(healthy.TotalBudget > single.TotalBudget, "Larger/harder encounter did not improve budget.");
		for (int seed = 0; seed < 100; seed++)
		{
			var first = new CombatRewardGenerator(new GodotRandomSource(seed)).Generate(Summary(health: 1, mana: 0));
			var second = new CombatRewardGenerator(new GodotRandomSource(seed)).Generate(Summary(health: 1, mana: 0));
			Check(Signature(first) == Signature(second), "Reward RNG not reproducible.");
			Check(first.Items.Count == 3 && first.Items.Count(i => i.Roll.Definition.Slots.Count > 0) == 1, "Reward is not one equipment plus help.");
			Check(ReferenceEquals(first.Items[1].Roll.Definition, RewardItemCatalog.HealingPotion)
				&& ReferenceEquals(first.Items[2].Roll.Definition, RewardItemCatalog.ManaPotion), "Consumable definitions are not reused.");
			Check(new CombatRewardGenerator(new GodotRandomSource(seed)).Generate(Summary()).Items.Count == 1, "Healthy hero got unnecessary supplies.");
		}
	}
	private static void SnapshotAndConsumables()
	{
		var hero = new PlayerCharacter(Vector2I.Zero);
		hero.DerivedStats.AddModifier(DerivedStatId.MaxMana, new("mana", StatModifierOperation.Flat, 10));
		hero.TakeDamage(9);
		var enemy = new BasicEnemy(Vector2I.One);
		var battle = new CombatEncounter(hero, new[] { enemy }, 1, new CombatOptions(attackCards: 10, defenseCards: 0, attackDamage: 100));
		battle.BeginRound(); battle.PlayCard(0, enemy.Id);
		var summary = battle.VictorySummary!;
		Check(summary.Health == 1 && summary.Mana == 0 && summary.Enemies[0].StartingHealth == 3, "Victory snapshot lost original enemy/resource values.");
		hero.RestoreHealth(100); hero.Resources.RestoreMana(100);
		Check(summary.Health == 1 && summary.Mana == 0, "Healing altered reward snapshot.");
		hero.Inventory.TryAcquire(RewardItemCatalog.HealingPotion, 2);
		var potion = hero.Inventory.Items.Last();
		Check(hero.Inventory.TryUseConsumable(potion.Id) == ConsumableUseResult.NoNeed && potion.Quantity == 2, "Full resource wasted potion.");
		hero.TakeDamage(9);
		Check(hero.Inventory.TryUseConsumable(potion.Id) == ConsumableUseResult.Success && hero.Health == 5 && potion.Quantity == 1, "Heal potion not consumed/restoring 40%.");
		hero.Inventory.TryAcquire(RewardItemCatalog.ManaPotion, 2);
		var mana = hero.Inventory.Items.Last(); hero.Resources.TrySpendMana(10);
		Check(hero.Inventory.TryUseConsumable(mana.Id) == ConsumableUseResult.Success && hero.Resources.Mana == 4 && mana.Quantity == 1, "Mana restoration missing.");
		hero.TakeDamage(100);
		Check(hero.Inventory.TryUseConsumable(potion.Id) == ConsumableUseResult.Unavailable && potion.Quantity == 1, "Consumable resurrected a dead hero.");
	}
	private static DungeonMap WinWorld(int seed, bool full = false)
	{
		var hero = new PlayerCharacter(Vector2I.Zero);
		hero.DerivedStats.AddModifier(DerivedStatId.MaxMana, new("mana", StatModifierOperation.Flat, 10));
		hero.TakeDamage(9);
		if (full)
		{
			hero.Inventory.TryAcquire(new("filler", "Перо"), 11);
			hero.Inventory.TryAcquire(RewardItemCatalog.HealingPotion);
		}
		var map = new DungeonMap(seed, new DungeonGenerationOptions(roomChance: 1, enemyRoomChance: 1), hero,
			new CombatOptions(attackCards: 10, defenseCards: 0, attackDamage: 100));
		ExpeditionTestDriver.FindCombat(map);
		Check(map.UseConsumable(Guid.NewGuid()) == ConsumableUseResult.Unavailable, "World allowed consumables in battle.");
		var battle = map.Combat!;
		map.BeginCombatRound();
		while (!battle.IsFinished && battle.ActionPoints > 0) map.PlayCombatCard(0, battle.Enemies.First(e => e.IsAlive).Id);
		Check(battle.Phase == CombatPhase.Victory && map.PendingReward is not null && map.Player.Health == 1, "Victory failed or auto-healed.");
		return map;
	}
	private static void WorldOwnershipAndIsolation()
	{
		var map = WinWorld(1701, full: true);
		var cache = map.PendingReward!;
		var signature = Signature(cache.Reward);
		Check(map.TryClaimReward(cache.Id, cache.Entries[0].Id) == RewardClaimResult.Unavailable, "Claimed during battle.");
		map.PlayCombatCard(0, Guid.NewGuid()); map.EndCombatTurn(); map.BeginCombatRound();
		Check(ReferenceEquals(map.PendingReward, cache), "Repeated victory command generated another reward.");
		map.LeaveVictoriousCombat();
		Check(map.TryClaimReward(cache.Id, cache.Entries[0].Id) == RewardClaimResult.Unavailable, "Claimed before reveal.");
		map.RevealReward(cache.Id);
		Check(map.TryClaimReward(cache.Id, cache.Entries[0].Id) == RewardClaimResult.Full && !cache.Entries[0].IsClaimed && map.Player.Inventory.UsedSlots == 12,
			"Full bag claim lost/partially acquired reward.");
		Check(map.GetAvailableReward() == cache && map.GetVisibleRewardAt(cache.Position) == cache, "Unclaimed reward not retained in section.");
		Check(map.UseConsumable(map.Player.Inventory.Items.Last().Id) == ConsumableUseResult.Success, "Cannot free potion slot between fights.");
		var heal = cache.Entries[1];
		Check(map.TryClaimReward(cache.Id, heal.Id) == RewardClaimResult.Success, "Cannot acquire saved healing supply.");
		Check(map.TryClaimReward(cache.Id, heal.Id) == RewardClaimResult.AlreadyClaimed, "Duplicate potion pickup possible.");
		map.UseConsumable(map.Player.Inventory.Items.Last().Id);
		Check(map.TryClaimReward(cache.Id, cache.Entries[0].Id) == RewardClaimResult.Success, "Saved gear disappeared after full bag.");
		Check(map.Player.Inventory.Items.Last().OwnerId == map.Player.Id && map.Player.Inventory.TryEquip(map.Player.Inventory.Items.Last().Id,
			cache.Reward.Equipment.Definition.Slots[0]) == InventoryResult.Locked, "Loot ownership/equipment expedition boundary broken.");
		Check(Signature(cache.Reward) == signature, "Claim/use rerolled reward.");
		var twin = WinWorld(1701); twin.LeaveVictoriousCombat();
		var twinCache = twin.PendingReward!; twin.RevealReward(twinCache.Id);
		foreach (var entry in twinCache.Entries) Check(twin.TryClaimReward(twinCache.Id, entry.Id) == RewardClaimResult.Success, "Cannot pick all reward items.");
		Check(twin.GetAvailableReward() is null && twin.GetVisibleRewardAt(twinCache.Position) is null, "Empty loot cache stayed available/visible.");
		for (int i = 0; i < 12; i++)
		{
			var first = map.GetClosedDoorPositions().OrderBy(p => p.Y).ThenBy(p => p.X).First();
			var second = twin.GetClosedDoorPositions().OrderBy(p => p.Y).ThenBy(p => p.X).First();
			Check(first == second, "Rewards changed geometry RNG."); map.OpenDoor(first); twin.OpenDoor(second);
		}
		Check(map.GetKnownTilePositions().ToHashSet().SetEquals(twin.GetKnownTilePositions()) && map.EnemyCount == twin.EnemyCount, "Reward/pickup changed future world.");
		var lost = new DungeonMap(1701, new DungeonGenerationOptions(roomChance: 1, enemyRoomChance: 1));
		ExpeditionTestDriver.FindCombat(lost); lost.Player.TakeDamage(100); lost.BeginCombatRound();
		Check(lost.Combat!.Phase == CombatPhase.Defeat && lost.PendingReward is null, "Defeat awarded loot.");
	}
	private async Task Presentation()
	{
		var reveal = new RewardReveal(3, true);
		reveal.Tick(0.7); Check(reveal.RevealedCount == 0, "First roulette skipped.");
		reveal.Tick(0.701); Check(reveal.RevealedCount == 1, "Affixes not revealed separately.");
		reveal.Tick(3); Check(reveal.IsComplete, "Sequential reveal did not finish.");
		var settings = new GameSettings("res://.godot/reward-settings-test.cfg");
		Check(settings.SetRewardAnimations(true), "Cannot save settings.");
		DungeonMap map = null!;
		for (int seed = 1; seed < 50; seed++)
		{
			map = WinWorld(seed); if (map.PendingReward!.Reward.Equipment.Affixes.Count >= 3) break;
		}
		var cache = map.PendingReward!; map.LeaveVictoriousCombat();
		Check(cache.Reward.Equipment.Affixes.Count >= 3, "Animation fixture lacks rare affixes.");
		var signature = Signature(cache.Reward);
		bool closed = false;
		var panel = new RewardPanel(map, cache, settings, () => closed = true) { ProcessMode = ProcessModeEnum.Disabled };
		AddChild(panel); panel._Process(0.3);
		Check(panel.ScreenText.Contains("[~]") && !panel.ScreenText.Contains("[+]"), "Roulette not visible before reveal.");
		panel.HandleKey(new InputEventKey { Pressed = true, Keycode = Key.Enter });
		panel.HandleKey(new InputEventKey { Pressed = true, Keycode = Key.Space });
		Check(!closed && map.Player.Inventory.Items.Count == 0, "Reveal allowed claim/close before completion.");
		await Capture("reward-roulette.png");
		panel.HandleKey(new InputEventKey { Pressed = true, Keycode = Key.Escape });
		Check(panel.Reveal.IsComplete && !closed && panel.ScreenText.Contains(cache.Reward.Equipment.DisplayName), "Escape did not only reveal reward.");
		Check(Signature(cache.Reward) == signature, "Skipping rerolled loot.");
		await Capture("reward-ui.png");
		if (DisplayServer.GetName() != "headless") { GetWindow().Size = new Vector2I(800, 600); await Capture("reward-ui-small.png"); }
		panel.HandleKey(new InputEventKey { Pressed = true, Keycode = Key.Down });
		panel.HandleKey(new InputEventKey { Pressed = true, Keycode = Key.Enter });
		Check(map.Player.Inventory.Items.Single().Definition.Restoration?.Resource == RestorationResource.Health, "Keyboard supply pickup failed.");
		Check(!panel.ScreenText.Contains(cache.Entries[1].Item.Roll.DisplayName) && !panel.ScreenText.Contains("[ЗАБРАН]"), "Claimed supply remained visible.");
		var inventory = new InventoryPanel(map.Player, () => { }, () => { }, map.UseConsumable) { Layer = 3 };
		AddChild(inventory);
		inventory.HandleKey(new InputEventKey { Pressed = true, Keycode = Key.Enter });
		Check(map.Player.Health == 5 && map.Player.Inventory.Items.Count == 0 && inventory.Status.Contains("восстановлен"),
			"Inventory keyboard did not consume reward potion between battles.");
		Check(Signature(cache.Reward) == signature, "Using reward supply changed its original result.");
		inventory.QueueFree();
		panel.QueueFree();
		var reopened = new RewardPanel(map, cache, settings, () => { });
		AddChild(reopened);
		Check(reopened.Reveal.IsComplete && !reopened.ScreenText.Contains(cache.Entries[1].Item.Roll.DisplayName), "Reopening shows claimed supply or reruns roulettes.");
		reopened.HandleKey(new InputEventKey { Pressed = true, Keycode = Key.Enter });
		Check(cache.Entries[0].IsClaimed && !reopened.ScreenText.Contains(cache.Reward.Equipment.DisplayName), "Claimed equipment remained on screen.");
		reopened.QueueFree();
		var suppliesOnly = new RewardPanel(map, cache, settings, () => { });
		AddChild(suppliesOnly);
		Check(!suppliesOnly.ScreenText.Contains(cache.Reward.Equipment.DisplayName)
			&& suppliesOnly.ScreenText.Contains(cache.Entries[2].Item.Roll.DisplayName)
			&& !suppliesOnly.ScreenText.Contains("СРАВНЕНИЕ С ЭКИПИРОВКОЙ"), "Supplies-only cache still renders claimed equipment.");
		await Capture("reward-remaining.png");
		suppliesOnly.HandleKey(new InputEventKey { Pressed = true, Keycode = Key.Enter });
		Check(cache.Entries.All(e => e.IsClaimed) && suppliesOnly.ScreenText.Contains("Добыча собрана"), "Filtered selection failed to claim last supply.");
		suppliesOnly.HandleKey(new InputEventKey { Pressed = true, Keycode = Key.Enter });
		suppliesOnly.HandleKey(new InputEventKey { Pressed = true, Keycode = Key.E });
		suppliesOnly.QueueFree();
		Check(settings.SetRewardAnimations(false) && !new GameSettings("res://.godot/reward-settings-test.cfg").RewardAnimations, "Animation setting not persistent.");
		var instantMap = WinWorld(map.Seed); var instantCache = instantMap.PendingReward!; instantMap.LeaveVictoriousCombat();
		var instant = new RewardPanel(instantMap, instantCache, settings, () => { });
		AddChild(instant);
		Check(instant.Reveal.IsComplete && Signature(instantCache.Reward) == signature, "Disabling animation changed result or failed instant reveal.");
		instant.QueueFree(); settings.SetRewardAnimations(true);
		DungeonMap commonMap = null!;
		for (int seed = 1; seed < 100; seed++)
		{
			commonMap = WinWorld(seed);
			if (commonMap.PendingReward!.Reward.Equipment.Affixes.Count == 0) break;
		}
		Check(commonMap.PendingReward!.Reward.Equipment.Affixes.Count == 0, "No common fixture found.");
		var commonCache = commonMap.PendingReward!; commonMap.LeaveVictoriousCombat();
		var common = new RewardPanel(commonMap, commonCache, settings, () => { }); AddChild(common);
		Check(common.Reveal.IsComplete && !common.ScreenText.Contains("аффикс", StringComparison.OrdinalIgnoreCase)
			&& !common.ScreenText.Contains("Свойств 0") && !common.ScreenText.Contains("Раскрыто 0"), "Common reward announces missing affixes.");
		common.QueueFree();
	}
	private async Task Capture(string filename)
	{
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		if (DisplayServer.GetName() == "headless") return;
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		GetViewport().GetTexture().GetImage().SavePng("res://.godot/" + filename);
	}
}
