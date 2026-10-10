using CmdRoguelike.Core;
using CmdRoguelike.Domain.Campaign;
using CmdRoguelike.Domain.Combat;
using CmdRoguelike.Domain.Entities;
using CmdRoguelike.Domain.Items;
using CmdRoguelike.Domain.Stats;
using CmdRoguelike.Generation;
using CmdRoguelike.Presentation;
using CmdRoguelike.World;
using Godot;

namespace CmdRoguelike.Tests;

public partial class CampaignSmokeTest : Node
{
	private readonly string _directory = "res://.godot/campaign-test-" + Guid.NewGuid().ToString("N");
	public override async void _Ready()
	{
		try
		{
			SavesAndOwnership(); BasePlacement(); OakRules(); ExpeditionCycle();
			await Interface();
			GD.Print("Campaign smoke test passed: 3 isolated slots, atomic ownership, exact saved identities/rolls/stats/Dormant, base-only saving, safe quit vs combat loss, base layout, oak/fire/cadence, portal return, repeated expeditions, menu/storage/encyclopedia/input.");
			GetTree().Quit();
		}
		catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
	}
	private static void Check(bool value, string message) => CombatSmokeTest.Check(value, message);
	private static void Equip(CampaignSession campaign, string id, EquipmentSlot slot)
	{
		var item = campaign.Hero.Inventory.Items.Single(i => i.Definition.Id == id);
		Check(campaign.Hero.Inventory.TryEquip(item.Id, slot) == InventoryResult.Success, "Cannot equip " + id);
	}
	private static void FireBuild(CampaignSession campaign)
	{
		Check(campaign.Withdraw(campaign.Storage.Items.First(i => i.Definition.Id == "fire-torch").Id) == InventoryResult.Success, "No accessible fire weapon.");
		Equip(campaign, "strength-ring", EquipmentSlot.RingLeft); Equip(campaign, "training-armor", EquipmentSlot.Torso);
		Equip(campaign, "fire-torch", EquipmentSlot.MainHand); Equip(campaign, "training-shield", EquipmentSlot.OffHand);
		campaign.Rest();
	}
	private void SavesAndOwnership()
	{
		var store = new CampaignSaveStore(ProjectSettings.GlobalizePath(_directory));
		var first = new CampaignSession(1); var second = new CampaignSession(2); var third = new CampaignSession(3);
		Check(first.Hero.Id != second.Hero.Id && first.Storage.Id != second.Storage.Id, "Campaign ownership shared.");
		FireBuild(first);
		var generator = new ItemRollGenerator(new GodotRandomSource(42), PrototypeAffixes.All);
		var roll = generator.Roll(ContentCatalog.Find("sample-shield"), ItemRarity.Rare, 5);
		Check(first.Hero.Inventory.TryAcquireRolled(roll) == InventoryResult.Success, "Cannot add rolled fixture.");
		var rolled = first.Hero.Inventory.Items.Last();
		Check(first.Store(rolled.Id) == InventoryResult.Success && first.Storage.Items.Last().Id == rolled.Id
			&& first.Storage.Items.Last().OwnerId == first.Storage.Id && !first.Hero.Inventory.Items.Contains(rolled), "Storage duplicated/replaced an instance.");
		first.Observe(new[] { roll.Definition });
		Check(first.Layout.TryPlace(BaseObjectKind.Healer, new(18, 14), first.Hero.Position), "Cannot place healer.");
		first.Hero.Attributes.IncreasePermanent(AttributeId.Willpower, 2);
		first.Hero.Attributes.AddModifier(AttributeId.Strength, new("saved-weakness", StatModifierOperation.Flat, -5));
		first.Hero.TakeDamage(2);
		var armor = first.Hero.Inventory.Items.Single(i => i.Definition.Id == "training-armor");
		Check(armor.State == EquipmentState.Dormant, "Dormant fixture did not deactivate.");
		store.Save(first); store.Save(second); store.Save(third);
		var loaded = store.Load(1);
		Check(loaded.Hero.Id == first.Hero.Id && loaded.Storage.Id == first.Storage.Id && loaded.Hero.Health == first.Hero.Health, "Saved identity/resources lost.");
		Check(loaded.Hero.Inventory.Items.Single(i => i.Id == armor.Id).State == EquipmentState.Dormant
			&& loaded.Hero.Attributes.GetValue(AttributeId.Willpower) == 2, "Saved stats/Dormant changed.");
		var savedRoll = loaded.Storage.Items.Single(i => i.Id == rolled.Id);
		Check(savedRoll.DisplayName == roll.DisplayName && savedRoll.Affixes.SequenceEqual(roll.Affixes)
			&& savedRoll.Rarity == roll.Rarity && savedRoll.ItemLevel == roll.ItemLevel, "Save rerolled an item.");
		Check(store.Load(2).Hero.Id == second.Hero.Id && !store.Load(2).KnownItems.Contains(roll.Definition.Id), "Slots shared progress/discoveries.");
		loaded.Hero.Attributes.RemoveModifiersFromSource("saved-weakness");
		Check(loaded.Hero.Inventory.Items.Single(i => i.Id == armor.Id).State == EquipmentState.Active, "Loaded Dormant cannot reactivate.");
		int fill = loaded.Hero.Inventory.Capacity - loaded.Hero.Inventory.UsedSlots;
		for (int i = 0; i < fill; i++) Check(loaded.Hero.Inventory.TryAcquire(ContentCatalog.Find("loot-sword")) == InventoryResult.Success, "Fixture fill failed.");
		Check(loaded.Withdraw(rolled.Id) == InventoryResult.Full && savedRoll.OwnerId == loaded.Storage.Id
			&& loaded.Storage.Items.Contains(savedRoll), "Full backpack lost storage item.");
		var before = File.ReadAllText(Path.Combine(ProjectSettings.GlobalizePath(_directory), "campaign-1.json"));
		first.StartExpedition(1701, new DungeonGenerationOptions(enemyRoomChance: 0));
		bool refused = false; try { store.Save(first); } catch (InvalidOperationException) { refused = true; }
		Check(refused && before == File.ReadAllText(Path.Combine(ProjectSettings.GlobalizePath(_directory), "campaign-1.json")), "Expedition save wrote a checkpoint.");
		Check(store.Load(1).Hero.Id == first.Hero.Id, "Safe quit lost living hero.");
		store.MarkCombat(first, true);
		var lost = store.Load(1);
		Check(lost.Hero.Id != first.Hero.Id && lost.FallenHeroes == 1 && lost.Storage.Items.Any(i => i.Id == rolled.Id)
			&& lost.Hero.Inventory.Items.All(i => first.Hero.Inventory.Items.All(old => old.Id != i.Id)), "Combat quit restored dead hero/gear or lost base storage.");
		store.Save(lost); Check(store.Load(1).Hero.Id == lost.Hero.Id && store.Load(1).FallenHeroes == 1, "Combat loss replayed on every load.");
		string thirdPath = Path.Combine(ProjectSettings.GlobalizePath(_directory), "campaign-3.json");
		string original = File.ReadAllText(thirdPath); File.WriteAllText(thirdPath, "{ invalid save");
		refused = false; try { store.Load(3); } catch (System.Text.Json.JsonException) { refused = true; }
		Check(refused && File.ReadAllText(thirdPath) == "{ invalid save", "Corrupt save was overwritten.");
		File.WriteAllText(thirdPath, original);
	}
	private static void BasePlacement()
	{
		var layout = new BaseLayout(); var hero = BaseLayout.Entrance;
		var chest = layout.Objects.Single(o => o.Kind == BaseObjectKind.Storage);
		Check(!layout.TryPlace(BaseObjectKind.Healer, new(0, 0), hero) && !layout.TryPlace(BaseObjectKind.Healer, hero, hero), "Invalid base placement allowed.");
		Check(layout.TryPlace(chest.Kind, new(16, 8), hero, chest.Id) && layout.At(chest.Position) is null && layout.At(new(16, 8))?.Id == chest.Id, "Move changed object identity.");
		Check(!layout.TryRemove(new(16, 8)) && !layout.TryRemove(layout.Objects.Single(o => o.Kind == BaseObjectKind.ExpeditionGate).Position), "Removed essential base object.");
		var corner = new Vector2I(6, 6);
		Check(layout.TryPlace(BaseObjectKind.Workbench, corner + Vector2I.Up, hero)
			&& layout.TryPlace(BaseObjectKind.Workbench, corner + Vector2I.Left, hero)
			&& layout.TryPlace(BaseObjectKind.Workbench, corner + Vector2I.Down, hero), "Connectivity fixture failed.");
		int count = layout.Objects.Count;
		Check(!layout.TryPlace(BaseObjectKind.Workbench, corner + Vector2I.Right, hero) && layout.Objects.Count == count, "Base placement sealed a disconnected floor.");
	}
	private static void OakRules()
	{
		var oak = new WiseOakEnemy(Vector2I.One);
		Check(oak.DamageProfile.Apply(6, DamageAspect.Physical) == 2 && oak.DamageProfile.Apply(6, DamageAspect.Fire) == 12, "Oak armor/fire weakness not applied.");
		Check(oak.DamageProfile.Apply(0, DamageAspect.Physical) == 0 && DamageProfile.Unprotected.Apply(6, DamageAspect.Physical) == 6, "Ordinary damage changed.");
		var hero = new PlayerCharacter(Vector2I.Zero);
		var battle = new CombatEncounter(hero, new[] { oak }, 1);
		Check(hero.Health == 10 && battle.Intents.Single().Action == EnemyActionKind.Guard, "Oak attacks before confirmation.");
		battle.BeginRound(); battle.EndPlayerTurn();
		Check(hero.Health == 10 && battle.GetEnemyBlock(oak.Id) == 2 && battle.Intents.Single().Action == EnemyActionKind.Charge, "Oak opening defense wrong.");
		battle.BeginRound(); battle.EndPlayerTurn();
		Check(hero.Health == 10 && battle.Intents.Single().Damage == 8, "Oak charged round did damage or failed to telegraph heavy hit.");
		battle.BeginRound(); battle.EndPlayerTurn();
		Check(hero.Health == 2 && battle.Intents.Single().Action == EnemyActionKind.Guard, "Oak does not hit every third round.");
		var policy = new BossPlacementPolicy(new GodotRandomSource(1), 4, 8);
		Check(!policy.ShouldPlace(3) && policy.ShouldPlace(8), "Boss progress bounds broken.");
		var campaign = new CampaignSession(1); FireBuild(campaign);
		var fire = new CombatEncounter(campaign.Hero, new[] { new WiseOakEnemy(Vector2I.One) }, 14);
		int dealt = 0;
		for (int round = 0; round < 40 && !fire.IsFinished; round++)
		{
			fire.BeginRound();
			while (fire.ActionPoints > 0 && !fire.IsFinished)
			{
				int block = fire.Intents.Sum(i => i.Damage);
				int index = fire.Hand.ToList().FindIndex(c => block > fire.Block && c.Kind == CombatCardKind.Defense);
				if (index < 0) index = fire.Hand.ToList().FindIndex(c => c.Kind == CombatCardKind.Attack && c.DamageAspect == DamageAspect.Fire);
				if (index < 0) index = fire.Hand.ToList().FindIndex(c => c.Kind == CombatCardKind.Attack);
				if (index < 0) break;
				var card = fire.Hand[index]; fire.PlayCard(index, fire.Enemies[0].Id);
				if (card.DamageAspect == DamageAspect.Fire) dealt += fire.Events.Where(e => e.Kind == CombatEventKind.Attack).Sum(e => e.Damage);
			}
			if (!fire.IsFinished) fire.EndPlayerTurn();
		}
		Check(fire.Phase == CombatPhase.Victory && dealt > 0, "Available starter fire build cannot defeat oak.");
	}
	private static DungeonMap ExploreBoss(CampaignSession campaign)
	{
		var map = campaign.StartExpedition(73013, new DungeonGenerationOptions(roomChance: 1, enemyRoomChance: 0));
		for (int i = 0; i < 150 && !map.GetEntities().OfType<Enemy>().Any(e => e.IsBoss); i++)
			map.OpenDoor(map.GetClosedDoorPositions().OrderBy(p => p.Y).ThenBy(p => p.X).First());
		Check(map.GetEntities().OfType<Enemy>().Count(e => e.IsBoss) == 1 && map.ReturnPortal is null
			&& map.RoomCount <= 8, "Boss was not placed exactly once by the guaranteed room.");
		var oak = map.GetEntities().OfType<Enemy>().Single(e => e.IsBoss);
		Check(!map.IsRevealed(oak.Position) && AsciiDungeonRenderer.GetAppearance(map, oak.Position).Symbol == "", "Hidden boss leaked.");
		foreach (var door in map.GetClosedDoorPositions().ToArray()) map.OpenDoor(door);
		var target = CardinalDirectionExtensions.All.Select(d => oak.Position + d.ToOffset()).First(p => map.GetTile(p) == DungeonTile.Floor);
		foreach (var step in PathTo(map, target)) { map.TryMovePlayer(step); if (map.IsInCombat) break; }
		Check(map.Combat?.Enemies.Single().IsBoss == true && map.Player.Health == map.Player.MaxHealth, "Discovering boss dealt automatic damage.");
		return map;
	}
	private static void Win(DungeonMap map)
	{
		var battle = map.Combat!;
		for (int turn = 0; turn < 100 && !battle.IsFinished; turn++)
		{
			map.BeginCombatRound();
			while (battle.ActionPoints > 0 && !battle.IsFinished)
			{
				int need = battle.Intents.Sum(i => i.Damage);
				int index = battle.Hand.ToList().FindIndex(c => need > battle.Block && c.Kind == CombatCardKind.Defense);
				if (index < 0) index = battle.Hand.ToList().FindIndex(c => c.Kind == CombatCardKind.Attack && c.DamageAspect == DamageAspect.Fire);
				if (index < 0) index = battle.Hand.ToList().FindIndex(c => c.Kind == CombatCardKind.Attack);
				if (index < 0) break;
				map.PlayCombatCard(index, battle.Enemies.First(e => e.IsAlive).Id);
			}
			if (!battle.IsFinished) map.EndCombatTurn();
		}
		Check(battle.Phase == CombatPhase.Victory, "Oak encounter failed to win.");
	}
	private void ExpeditionCycle()
	{
		var campaign = new CampaignSession(2); FireBuild(campaign);
		Guid heroId = campaign.Hero.Id; var ids = campaign.Hero.Inventory.Items.Select(i => i.Id).ToArray();
		var map = ExploreBoss(campaign); Win(map);
		Check(map.ReturnPortal is not null && !campaign.EnterReturnPortal(), "Victory auto-extracted or no portal.");
		map.LeaveVictoriousCombat(); map.RevealReward(map.PendingReward!.Id);
		var loot = map.PendingReward.Entries[0]; Check(map.TryClaimReward(map.PendingReward.Id, loot.Id) == RewardClaimResult.Success, "No boss loot.");
		Guid lootId = campaign.Hero.Inventory.Items.Last().Id;
		Check(map.IsReturnPortalVisible && AsciiDungeonRenderer.GetAppearance(map, map.ReturnPortal!.Value).Symbol == "O", "Portal not rendered.");
		map.Player.TakeDamage(1); int hp = map.Player.Health;
		foreach (var step in PathTo(map, map.ReturnPortal ?? throw new InvalidOperationException("No return portal."))) map.TryMovePlayer(step);
		Check(campaign.EnterReturnPortal() && campaign.IsOnBase && campaign.Hero.Id == heroId && campaign.Hero.Health == hp
			&& campaign.WorldTier == 1 && campaign.OakVictories == 1 && !campaign.Hero.Inventory.IsLocked
			&& ids.All(id => campaign.Hero.Inventory.Items.Any(i => i.Id == id)) && campaign.Hero.Inventory.Items.Any(i => i.Id == lootId)
			&& map.GetEntities().All(e => e.Id != heroId), "Portal return lost identity/loot, healed, or kept a stale registry.");
		Check(campaign.Store(lootId) == InventoryResult.Success, "Cannot use returned loot on base.");
		var store = new CampaignSaveStore(ProjectSettings.GlobalizePath(_directory)); store.Save(campaign);
		campaign.Rest(); var second = campaign.StartExpedition(11, new DungeonGenerationOptions(enemyRoomChance: 1, roomChance: 1));
		Check(second.Player.Id == heroId, "Next expedition replaced the hero.");
		ExpeditionTestDriver.FindCombat(second); second.Player.TakeDamage(999); second.BeginCombatRound();
		Check(campaign.AcceptDeath() && campaign.Hero.Id != heroId && campaign.WorldTier == 0 && campaign.Storage.Items.Any(i => i.Id == lootId), "Death did not preserve base storage/reset hero.");
		store.Save(campaign);
		Check(store.Load(2).Storage.Items.Any(i => i.Id == lootId), "Base loot did not survive death save.");
	}
	internal static List<CardinalDirection> PathTo(DungeonMap map, Vector2I target)
	{
		var queue = new Queue<Vector2I>(); queue.Enqueue(map.Player.Position);
		var seen = new HashSet<Vector2I> { map.Player.Position };
		var previous = new Dictionary<Vector2I, (Vector2I Cell, CardinalDirection Direction)>();
		while (queue.TryDequeue(out var cell))
		{
			if (cell == target) break;
			foreach (var d in CardinalDirectionExtensions.All)
			{
				var next = cell + d.ToOffset();
				if (!map.IsWalkable(next) || map.GetEntityAt(next) is Enemy || !seen.Add(next)) continue;
				previous[next] = (cell, d); queue.Enqueue(next);
			}
		}
		Check(seen.Contains(target), "No route to boss/portal.");
		var path = new List<CardinalDirection>(); var cursor = target;
		while (cursor != map.Player.Position) { var step = previous[cursor]; path.Add(step.Direction); cursor = step.Cell; }
		path.Reverse(); return path;
	}
	private async Task Interface()
	{
		var game = new DungeonGame { WorldSeed = 73013, EnemyRoomChance = 0,
			SaveDirectory = _directory + "-ui", SettingsPath = "res://.godot/campaign-ui-settings.cfg" };
		AddChild(game); await Capture("campaign-menu.png");
		Check(game.GetChildren().OfType<MainMenuPanel>().Single().ScreenText.Contains("ГЛАВНОЕ МЕНЮ") && game.CurrentMap is null, "Game bypassed main menu.");
		Press(game, Key.Enter); await Capture("campaign-slots.png"); Press(game, Key.Enter);
		var campaign = game.CurrentCampaign!;
		Check(campaign.IsOnBase && !game.GetChildren().OfType<InventoryPanel>().Single().Visible, "New slot did not open base map.");
		await Capture("campaign-base.png");
		Press(game, Key.C); var storage = game.GetChildren().OfType<StoragePanel>().Single();
		Check(storage.ScreenText.Contains("Огненный факел"), "Storage omitted fire starter.");
		Press(game, Key.Enter); Check(campaign.Hero.Inventory.Items.Any(i => i.Definition.Id == "fire-torch"), "Storage input failed.");
		await Capture("campaign-storage.png"); Press(game, Key.Escape);
		Press(game, Key.I); Press(game, Key.Down, Key.Down, Key.Enter, Key.Up, Key.Enter); // Ring and armor.
		Press(game, Key.Down, Key.Down, Key.Enter); // Shield; after equip armor bag is sword,mace,shield,torch.
		Press(game, Key.Down, Key.Enter); // Torch.
		var inventory = game.GetChildren().OfType<InventoryPanel>().Single();
		Check(inventory.ScreenText.Contains("(огонь)") && campaign.Hero.Inventory.Items.Any(i => i.Definition.Id == "fire-torch" && i.Location == ItemLocation.Equipment), "Fire cards absent in preparation.");
		await Capture("campaign-fire-preparation.png"); Press(game, Key.Escape);
		campaign.Rest();
		Press(game, Key.B, Key.Right, Key.Enter, Key.Escape);
		Check(campaign.Layout.Objects.Count == 4, "Base editor ignored input.");
		Press(game, Key.F5); Check(game.GetChildren().OfType<BasePanel>().Single().Status.Contains("сохранено"), "Base save keyboard failed.");
		Press(game, Key.Escape, Key.Down, Key.Down, Key.Enter);
		Check(game.GetChildren().OfType<InfoPanel>().Single().ScreenText.Contains("https://t.me/necrodwarfs") && game.GetChildren().OfType<InfoPanel>().Single().ScreenText.Contains("doflare"), "Creator attribution missing.");
		await Capture("campaign-creator.png"); Press(game, Key.Escape, Key.Down, Key.Enter);
		var book = game.GetChildren().OfType<EncyclopediaPanel>().Single();
		Check(book.ScreenText.Contains("Огненный факел") && !book.ScreenText.Contains("Зелье маны"), "Book exposed unknown items or omitted discovered ones.");
		await Capture("campaign-encyclopedia.png"); Press(game, Key.Right);
		Check(book.ScreenText.Contains("ВСТРЕЧЕНО 0") && !book.ScreenText.Contains("Огненный факел"), "Book leaked discoveries across slots.");
		Press(game, Key.Escape, Key.Up, Key.Up, Key.Up, Key.Enter, Key.Enter); // Reload first slot.
		Check(game.CurrentCampaign!.Hero.Id == campaign.Hero.Id, "UI load replaced hero.");
		Press(game, Key.I, Key.F); var map = game.CurrentMap!;
		Check(map.Player.Inventory.IsLocked && game.CurrentCampaign.Expedition == map, "Preparation did not launch expedition.");
		var save = Path.Combine(ProjectSettings.GlobalizePath(_directory + "-ui"), "campaign-1.json");
		string before = File.ReadAllText(save); Press(game, Key.F5);
		Check(game.GetChildren().OfType<InfoPanel>().Any() && File.ReadAllText(save) == before, "F5 saved an expedition.");
		Press(game, Key.Escape, Key.Escape, Key.Enter); // Safe quit to menu.
		Press(game, Key.Enter, Key.Enter);
		Check(game.CurrentCampaign!.Hero.Id == campaign.Hero.Id && game.CurrentCampaign.IsOnBase, "Safe UI quit lost hero.");
		if (DisplayServer.GetName() != "headless") { GetWindow().Size = new(800, 600); await Capture("campaign-base-small.png"); GetWindow().Size = new(1152, 648); }
		Press(game, Key.I, Key.F);
		map = game.CurrentMap!; FindBossThroughGame(game); await WaitForVisuals(game);
		Check(File.Exists(save + ".combat"), "Actual battle did not record exit loss.");
		await Capture("campaign-oak.png");
		for (int turn = 0; turn < 60 && !map.Combat!.IsFinished; turn++)
		{
			Press(game, Key.Enter); await WaitForVisuals(game);
			while (map.Combat.ActionPoints > 0 && !map.Combat.IsFinished)
			{
				var battle = map.Combat;
				int need = battle.Intents.Sum(i => i.Damage);
				int index = battle.Hand.ToList().FindIndex(c => need > battle.Block && c.Kind == CombatCardKind.Defense);
				if (index < 0) index = battle.Hand.ToList().FindIndex(c => c.Kind == CombatCardKind.Attack && c.DamageAspect == DamageAspect.Fire);
				if (index < 0) index = battle.Hand.ToList().FindIndex(c => c.Kind == CombatCardKind.Attack);
				if (index < 0) break;
				for (int i = 0; i < 10; i++) Press(game, Key.Left);
				for (int i = 0; i < index; i++) Press(game, Key.Right);
				Press(game, Key.Enter); await WaitForVisuals(game);
			}
			if (!map.Combat.IsFinished) { Press(game, Key.Space); await WaitForVisuals(game); }
		}
		Check(map.Combat!.Phase == CombatPhase.Victory && !File.Exists(save + ".combat"), "UI oak win failed or retained a lethal battle marker.");
		Press(game, Key.Enter, Key.Escape, Key.Enter, Key.F); await Capture("campaign-portal.png");
		Guid returningHero = map.Player.Id;
		foreach (var step in PathTo(map, map.ReturnPortal ?? throw new InvalidOperationException("No portal."))) Press(game, KeyFor(step));
		Check(game.CurrentCampaign!.IsOnBase && game.CurrentCampaign.Hero.Id == returningHero && game.CurrentCampaign.WorldTier == 1, "Walking into portal failed to return/save on base.");
		await Capture("campaign-returned-base.png");
		var carried = game.CurrentCampaign.Hero.Inventory.Items.Select(i => i.Id).ToHashSet(); Guid storageId = game.CurrentCampaign.Storage.Id;
		Press(game, Key.I, Key.F); FindBossThroughGame(game); await WaitForVisuals(game);
		Press(game, Key.Escape); var confirmation = game.GetChildren().OfType<InfoPanel>().Single();
		Check(confirmation.ScreenText.Contains("потерю героя"), "Combat exit did not explain the loss.");
		Press(game, Key.Escape);
		Check(game.CurrentMap!.IsInCombat, "Cancelling exit abandoned battle.");
		Press(game, Key.Escape, Key.Enter, Key.Enter, Key.Enter);
		Check(game.CurrentCampaign!.Hero.Id != returningHero && game.CurrentCampaign.Storage.Id == storageId
			&& game.CurrentCampaign.FallenHeroes == 1 && game.CurrentCampaign.WorldTier == 0
			&& game.CurrentCampaign.Hero.Inventory.Items.All(i => !carried.Contains(i.Id)), "Actual combat exit restored lost hero/items or lost base.");
		game.QueueFree(); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	}
	private static Key KeyFor(CardinalDirection direction) => direction switch
	{ CardinalDirection.Up => Key.Up, CardinalDirection.Right => Key.Right, CardinalDirection.Down => Key.Down, _ => Key.Left };
	private static void FindBossThroughGame(DungeonGame game)
	{
		var map = game.CurrentMap!;
		for (int i = 0; i < 200 && !map.GetEntities().OfType<Enemy>().Any(e => e.IsBoss); i++)
			map.OpenDoor(map.GetClosedDoorPositions().OrderBy(p => p.Y).ThenBy(p => p.X).First());
		var oak = map.GetEntities().OfType<Enemy>().Single(e => e.IsBoss);
		foreach (var door in map.GetClosedDoorPositions().ToArray()) map.OpenDoor(door);
		var target = CardinalDirectionExtensions.All.Select(d => oak.Position + d.ToOffset()).First(p => map.GetTile(p) == DungeonTile.Floor);
		foreach (var step in PathTo(map, target)) { Press(game, KeyFor(step)); if (map.IsInCombat) break; }
		Check(map.Combat?.Enemies.Single().IsBoss == true, "UI did not enter boss battle.");
	}
	private async Task WaitForVisuals(DungeonGame game)
	{
		var timer = System.Diagnostics.Stopwatch.StartNew();
		while (game.IsEnteringCombat || game.GetChildren().OfType<CombatPanel>().Any(p => p.IsAnimating))
		{
			if (timer.Elapsed.TotalSeconds > 15) throw new InvalidOperationException("Campaign animation did not complete.");
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}
	}
	private async Task Capture(string name)
	{
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		if (DisplayServer.GetName() == "headless") return;
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		GetViewport().GetTexture().GetImage().SavePng("res://.godot/" + name);
	}
	private static void Press(DungeonGame game, params Key[] keys)
	{
		foreach (Key key in keys) game._UnhandledKeyInput(new InputEventKey { Pressed = true, Keycode = key });
	}
}
