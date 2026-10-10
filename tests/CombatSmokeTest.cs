using CmdRoguelike.Core;
using CmdRoguelike.Domain.Combat;
using CmdRoguelike.Domain.Entities;
using CmdRoguelike.Domain.Items;
using CmdRoguelike.Domain.Stats;
using CmdRoguelike.Generation;
using CmdRoguelike.Presentation;
using CmdRoguelike.State;
using CmdRoguelike.World;
using Godot;

namespace CmdRoguelike.Tests;

public partial class CombatSmokeTest : Node
{
	public override void _Ready()
	{
		try
		{
			InitiativeAndPreview();
			CardsApAndReplacement();
			ArmorAndBlock();
			DefeatAndVictory();
			KnowledgeSections();
			RegistryRemoval();
			WorldEncounter();
			CombatContentChecks.Run();
			GD.Print("Combat smoke test passed: initiative, AP, equipment cards, Dormant, deck conservation, enemy intents/block/charge, deterministic factory, hidden sections, cleanup.");
			GetTree().Quit();
		}
		catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
	}
	internal static void Check(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
	private sealed class TestEnemy : Enemy
	{
		public TestEnemy(int number, int health = 100, int damage = 1, int willpower = 0)
			: base(new Vector2I(number, 0), "Enemy", health, damage)
		{
			Attributes.AddModifier(AttributeId.Willpower, new("test", StatModifierOperation.Flat, willpower));
		}
	}
	private static void InitiativeAndPreview()
	{
		var hero = new PlayerCharacter(Vector2I.Zero);
		hero.DerivedStats.AddModifier(DerivedStatId.MaxHealth, new("test", StatModifierOperation.Flat, 100));
		hero.RestoreHealth(100);
		var enemies = Enumerable.Range(1, 10).Select(i => new TestEnemy(i, willpower: i)).ToArray();
		var battle = new CombatEncounter(hero, enemies, 1);
		Check(hero.Health == 110 && battle.Phase == CombatPhase.RoundPreview, "Encounter applied untelegraphed damage.");
		Check(battle.Intents.Count(intent => intent.BeforePlayer) == 3, "Ten high-willpower enemies bypassed the 30% cap.");
		Check(battle.Intents.Take(3).Select(intent => intent.EnemyId).SequenceEqual(enemies.Reverse().Take(3).Select(enemy => enemy.Id)), "Wrong willpower order.");
		Check(battle.Intents.All(intent => intent.TargetId == hero.Id && intent.Damage == 1), "Intent lacks target or damage.");
		Check(battle.PlayCard(0, enemies[0].Id) == CombatCommandResult.WrongPhase, "Card played during preview.");
		battle.BeginRound();
		Check(hero.Health == 107 && battle.Phase == CombatPhase.PlayerTurn, "Opening actors did not act exactly once.");
		battle.EndPlayerTurn();
		Check(hero.Health == 100 && battle.Round == 2 && battle.Phase == CombatPhase.RoundPreview, "Remaining actors acted twice or missed a turn.");
		Check(battle.Intents.Count(intent => intent.BeforePlayer) == 3, "Cap did not apply every round.");
		hero.Attributes.AddModifier(AttributeId.Willpower, new("test", StatModifierOperation.Flat, 20));
		battle.BeginRound();
		battle.EndPlayerTurn();
		Check(battle.Intents.All(intent => !intent.BeforePlayer), "Initiative did not recalculate with changed willpower.");
		var small = new CombatEncounter(new PlayerCharacter(Vector2I.Zero), enemies.Take(3), 1);
		Check(small.Intents.All(intent => !intent.BeforePlayer), "Cap did not round down.");
		var tiedHero = new PlayerCharacter(Vector2I.Zero, new Dictionary<AttributeId, int> { [AttributeId.Willpower] = 10 });
		var tied = new CombatEncounter(tiedHero, enemies, 1);
		Check(tied.Intents.All(intent => !intent.BeforePlayer), "Player did not win willpower ties.");
	}
	private static void CardsApAndReplacement()
	{
		var hero = new PlayerCharacter(Vector2I.Zero);
		var enemy = new TestEnemy(1, damage: 0);
		var battle = new CombatEncounter(hero, new[] { enemy }, 22, new CombatOptions(attackCards: 10, defenseCards: 0));
		battle.BeginRound();
		Check(battle.ActionPoints == 3 && battle.Hand.Count == 5, "Wrong base AP or hand size.");
		Check(battle.PlayCard(0, Guid.NewGuid()) == CombatCommandResult.InvalidTarget
			&& battle.ActionPoints == 3 && battle.Hand.Count == 5 && enemy.Health == 100, "Rejected target mutated combat.");
		Check(battle.ReplaceCard(0) == CombatCommandResult.Success && battle.ActionPoints == 3 && battle.Hand.Count == 5, "Replacement cost AP or lost a card.");
		Check(battle.ReplaceCard(0) == CombatCommandResult.ReplacementUnavailable, "Repeated replacement allowed.");
		for (int i = 0; i < 3; i++) Check(battle.PlayCard(0, enemy.Id) == CombatCommandResult.Success, "Attack failed.");
		Check(enemy.Health == 97 && battle.ActionPoints == 0 && battle.Hand.Count == 2, "Attack did not consume AP and card.");
		Check(battle.PlayCard(0, enemy.Id) == CombatCommandResult.NotEnoughActionPoints && enemy.Health == 97 && battle.Hand.Count == 2, "Failed AP check changed state.");
		Check(battle.ReplaceCard(99) == CombatCommandResult.InvalidCard, "Bad index accepted.");
		battle.EndPlayerTurn();
		hero.DerivedStats.AddModifier(DerivedStatId.MaxActionPoints, new("potion", StatModifierOperation.Flat, 1));
		battle.BeginRound();
		Check(battle.ActionPoints == 4 && battle.Hand.Count == 5 && !battle.CanReplaceCard, "AP modifier or replacement cooldown failed.");
		battle.EndPlayerTurn();
		hero.DerivedStats.RemoveModifiersFromSource("potion");
		battle.BeginRound();
		Check(battle.ActionPoints == 3 && battle.CanReplaceCard, "AP carried over or third-turn replacement unavailable.");
		for (int i = 0; i < 12; i++)
		{
			battle.EndPlayerTurn(); battle.BeginRound();
			Check(battle.Hand.Count == 5 && battle.Hand.Count + battle.DrawCount + battle.DiscardCount == 10, "Reshuffle duplicated or lost cards.");
		}
		var copy = new CombatEncounter(new PlayerCharacter(Vector2I.Zero), new[] { new TestEnemy(1, damage: 0) }, 22);
		var same = new CombatEncounter(new PlayerCharacter(Vector2I.Zero), new[] { new TestEnemy(1, damage: 0) }, 22);
		for (int turn = 0; turn < 8; turn++)
		{
			copy.BeginRound(); same.BeginRound();
			Check(copy.Hand.Select(card => card.Id).SequenceEqual(same.Hand.Select(card => card.Id)), "Same seed produced different hands.");
			copy.EndPlayerTurn(); same.EndPlayerTurn();
		}
		var mixed = new CombatEncounter(new PlayerCharacter(Vector2I.Zero), new[] { new TestEnemy(1, damage: 0) }, 9);
		mixed.BeginRound();
		CombatCard[] original = mixed.Hand.ToArray();
		mixed.ReplaceCard(2);
		Check(mixed.Hand.Where((_, index) => index != 2).SequenceEqual(original.Where((_, index) => index != 2)), "Replacement rearranged other cards.");
	}
	private static int FindCard(CombatEncounter battle, CombatCardKind kind)
		=> battle.Hand.ToList().FindIndex(card => card.Kind == kind);
	private static void ArmorAndBlock()
	{
		var hero = new PlayerCharacter(Vector2I.Zero);
		var armor = new ItemDefinition("armor", "armor", slots: new[] { EquipmentSlot.Torso },
			requirements: new Dictionary<AttributeId, int> { [AttributeId.Strength] = 1 },
			statBonuses: new Dictionary<DerivedStatId, int> { [DerivedStatId.Armor] = 5 });
		hero.Inventory.TryAcquire(armor);
		hero.Attributes.AddModifier(AttributeId.Strength, new("strength", StatModifierOperation.Flat, 1));
		Check(hero.Inventory.TryEquip(hero.Inventory.Items[0].Id, EquipmentSlot.Torso) == InventoryResult.Success, "Armor equip failed.");
		var battle = new CombatEncounter(hero, new[] { new TestEnemy(1, damage: 3), new TestEnemy(2, damage: 4) }, 9);
		battle.BeginRound();
		int defense = FindCard(battle, CombatCardKind.Defense);
		Check(defense >= 0, "Fixture hand has no defense.");
		battle.PlayCard(defense);
		Check(battle.Block == 5 && hero.Health == 10, "Defense did not use 100% armor or armor passively reduced damage.");
		battle.EndPlayerTurn();
		Check(battle.Block == 0 && hero.Health == 8, "Block did not absorb shared incoming damage: 3 + 4 - 5.");
		battle.BeginRound();
		hero.Attributes.RemoveModifiersFromSource("strength");
		Check(hero.Inventory.Items[0].State == EquipmentState.Dormant && hero.DerivedStats.GetValue(DerivedStatId.Armor) == 0, "Dormant armor kept its bonus.");
		Check(battle.DefenseBlock(new CombatOptions().Defense) == 0, "Defense used stale armor.");

		var tank = new PlayerCharacter(Vector2I.Zero);
		tank.DerivedStats.AddModifier(DerivedStatId.Armor, new("armor", StatModifierOperation.Flat, 20));
		var group = Enumerable.Range(1, 10).Select(i => new TestEnemy(i, willpower: 10)).ToArray();
		var protectedBattle = new CombatEncounter(tank, group, 9, new CombatOptions(attackCards: 1, defenseCards: 9));
		protectedBattle.BeginRound();
		protectedBattle.PlayCard(FindCard(protectedBattle, CombatCardKind.Defense));
		protectedBattle.PlayCard(FindCard(protectedBattle, CombatCardKind.Defense));
		Check(protectedBattle.Block == 40, "Defense cards did not stack.");
		protectedBattle.EndPlayerTurn();
		Check(protectedBattle.Block == 33 && tank.Health == 7, "Late attacks ignored block.");
		protectedBattle.BeginRound();
		Check(tank.Health == 7 && protectedBattle.Block == 0, "Block expired before early attacks or survived player turn.");

		var huge = new PlayerCharacter(Vector2I.Zero);
		huge.DerivedStats.AddModifier(DerivedStatId.Armor, new("armor", StatModifierOperation.Flat, int.MaxValue));
		var overflow = new CombatEncounter(huge, new[] { new TestEnemy(1, damage: 0) }, 9, new CombatOptions(attackCards: 1, defenseCards: 9));
		overflow.BeginRound();
		overflow.PlayCard(FindCard(overflow, CombatCardKind.Defense));
		int ap = overflow.ActionPoints, count = overflow.Hand.Count;
		bool rejected = false;
		try { overflow.PlayCard(FindCard(overflow, CombatCardKind.Defense)); }
		catch (OverflowException) { rejected = true; }
		Check(rejected && overflow.ActionPoints == ap && overflow.Hand.Count == count && overflow.Block == int.MaxValue, "Block overflow consumed AP or a card.");
	}
	private static void DefeatAndVictory()
	{
		var hero = new PlayerCharacter(Vector2I.Zero);
		var killer = new TestEnemy(1, damage: 100);
		var battle = new CombatEncounter(hero, new[] { killer }, 1);
		battle.BeginRound(); battle.EndPlayerTurn();
		Check(battle.Phase == CombatPhase.Defeat && hero.Health == 0, "Lethal damage did not stop encounter.");
		Check(battle.BeginRound() == CombatCommandResult.WrongPhase && battle.ReplaceCard(0) == CombatCommandResult.WrongPhase, "Dead hero can act.");
		var winner = new PlayerCharacter(Vector2I.Zero);
		var victim = new TestEnemy(1, health: 1, damage: 100);
		var won = new CombatEncounter(winner, new[] { victim }, 1, new CombatOptions(attackCards: 10, defenseCards: 0));
		won.BeginRound(); won.PlayCard(0, victim.Id);
		Check(won.Phase == CombatPhase.Victory && winner.Health == 10 && won.EndPlayerTurn() == CombatCommandResult.WrongPhase, "Dead enemy retaliated after victory.");
	}
	private static void KnowledgeSections()
	{
		var grid = new DungeonGrid();
		grid.SetFloor(Vector2I.Zero); grid.SetFloor(Vector2I.Right);
		grid.SetDoor(new Vector2I(2, 0), DungeonTile.ClosedDoor, CardinalDirection.Right);
		grid.SetFloor(new Vector2I(3, 0)); grid.SetFloor(new Vector2I(4, 0));
		grid.SetDoor(new Vector2I(5, 0), DungeonTile.ClosedDoor, CardinalDirection.Right);
		grid.SetFloor(new Vector2I(6, 0));
		var knowledge = new DungeonKnowledge();
		knowledge.Enter(Vector2I.Zero, position => grid[position]);
		Check(knowledge.IsRevealed(new Vector2I(2, 0)) && !knowledge.IsRevealed(new Vector2I(3, 0)), "Closed door leaked hidden section.");
		grid.SetDoorState(new Vector2I(2, 0), DungeonTile.OpenDoor);
		knowledge.Enter(new Vector2I(2, 0), position => grid[position]);
		Check(!knowledge.IsRevealed(new Vector2I(3, 0)), "Opening or standing on threshold revealed content.");
		knowledge.Enter(new Vector2I(3, 0), position => grid[position]);
		Check(knowledge.IsInCurrentSection(new Vector2I(4, 0)) && !knowledge.IsInCurrentSection(Vector2I.Zero)
			&& knowledge.IsRevealed(Vector2I.Zero) && !knowledge.IsRevealed(new Vector2I(6, 0)), "Section entry lost memory or disclosed next closed section.");
	}
	private static void WorldEncounter()
	{
		DungeonMap map = new(1701, new DungeonGenerationOptions(roomChance: 1, enemyRoomChance: 1));
		ExpeditionTestDriver.FindCombat(map);
		CombatEncounter battle = map.Combat!;
		Check(battle.Enemies.All(enemy => map.GetVisibleEntityAt(enemy.Position) == enemy), "Encounter includes hidden enemies.");
		Vector2I origin = map.Player.Position;
		int regions = map.RegionCount, doors = map.OpenedDoorCount;
		Check(map.TryMovePlayer(CardinalDirection.Right).Outcome == PlayerMoveOutcome.InCombat
			&& map.TryOpenAdjacentDoor().Outcome == PlayerDoorInteractionOutcome.InCombat
			&& map.OpenDoor(map.GetClosedDoorPositions().First()) is null
			&& map.Player.Position == origin && map.RegionCount == regions && map.OpenedDoorCount == doors, "Combat allowed movement or door expansion.");
		// Huge test-only armor keeps the fixture alive while testing card/deck/world integration.
		map.Player.DerivedStats.AddModifier(DerivedStatId.Armor, new("test", StatModifierOperation.Flat, 100));
		for (int round = 0; round < 50 && !battle.IsFinished; round++)
		{
			map.BeginCombatRound();
			while (battle.ActionPoints > 0 && !battle.IsFinished)
			{
				int attack = FindCard(battle, CombatCardKind.Attack);
				int defense = FindCard(battle, CombatCardKind.Defense);
				if (battle.Block == 0 && defense >= 0) map.PlayCombatCard(defense);
				else if (attack >= 0) map.PlayCombatCard(attack, battle.Enemies.First(enemy => enemy.IsAlive).Id);
				else break;
			}
			if (!battle.IsFinished) map.EndCombatTurn();
		}
		Check(battle.Phase == CombatPhase.Victory, "Fixture failed to win encounter.");
		Check(battle.Enemies.All(enemy => map.GetEntityAt(enemy.Position) is null && map.CanEnter(enemy.Position)), "Defeated enemies still occupy the map.");
		Check(map.LeaveVictoriousCombat() && !map.IsInCombat && !map.LeaveVictoriousCombat(), "Victory did not return to exploration exactly once.");
		Check(map.GetEntityAt(origin) == map.Player, "Combat moved or unregistered the player.");
		Check(map.OpenDoor(map.GetClosedDoorPositions().First()) is not null, "Doors stayed locked after victory.");
	}
	private static void RegistryRemoval()
	{
		var registry = new EntityRegistry();
		var hero = new PlayerCharacter(Vector2I.Zero);
		var enemy = new TestEnemy(1);
		registry.Add(hero); registry.Add(enemy); registry.Remove(enemy);
		Check(registry.Count == 1 && !registry.IsOccupied(enemy.Position), "Remove left a stale position or ID.");
		bool rejected = false;
		try { registry.Remove(enemy); } catch (InvalidOperationException) { rejected = true; }
		Check(rejected && registry.Count == 1 && registry.TryGetAt(hero.Position, out var indexed) && indexed == hero, "Rejected removal changed the registry.");
	}
}

internal static class ExpeditionTestDriver
{
	internal static void FindCombat(DungeonMap map, Action<CardinalDirection>? move = null)
	{
		move ??= direction => map.TryMovePlayer(direction);
		for (int expansion = 0; expansion < 50 && !map.IsInCombat; expansion++)
		{
			var queue = new Queue<Vector2I>();
			var previous = new Dictionary<Vector2I, (Vector2I Cell, CardinalDirection Direction)>();
			var visited = new HashSet<Vector2I> { map.Player.Position };
			queue.Enqueue(map.Player.Position);
			Vector2I? target = null;
			while (queue.TryDequeue(out Vector2I cell))
			{
				if (map.GetTile(cell) == DungeonTile.ClosedDoor) { target = cell; break; }
				foreach (CardinalDirection direction in CardinalDirectionExtensions.All)
				{
					Vector2I next = cell + direction.ToOffset();
					if (!map.IsPotentiallyTraversable(next) || map.GetEntityAt(next) is Enemy || !visited.Add(next)) continue;
					previous[next] = (cell, direction);
					queue.Enqueue(next);
				}
			}
			CombatSmokeTest.Check(target is not null, "Exploration fixture has no reachable door.");
			var path = new List<CardinalDirection>();
			Vector2I cursor = target!.Value;
			while (cursor != map.Player.Position)
			{
				var step = previous[cursor]; path.Add(step.Direction); cursor = step.Cell;
			}
			path.Reverse();
			foreach (CardinalDirection direction in path)
			{
				move(direction);
				if (map.IsInCombat) return;
			}
			foreach (Enemy enemy in map.GetEntities().OfType<Enemy>().Where(enemy => !map.IsRevealed(enemy.Position)))
				CombatSmokeTest.Check(map.GetVisibleEntityAt(enemy.Position) is null
					&& AsciiDungeonRenderer.GetAppearance(map, enemy.Position).Symbol == "", "Hidden enemy leaked to renderer.");
			// Last movement opened the door; two more steps cross its threshold.
			move(path[^1]);
			if (map.IsInCombat) return;
			move(path[^1]);
		}
		CombatSmokeTest.Check(map.IsInCombat, "Exploration failed to discover enemies.");
	}
}
