using CmdRoguelike.Domain.Combat;
using CmdRoguelike.Domain.Entities;
using CmdRoguelike.Domain.Stats;
using CmdRoguelike.Presentation;
using Godot;

namespace CmdRoguelike.Tests;

public partial class CombatAnimationSmokeTest : Node
{
	private sealed class TestEnemy : Enemy
	{
		public TestEnemy(int x, int damage) : base(new Vector2I(x, 0), "Enemy", 100, damage) { }
	}
	public override void _Ready()
	{
		try
		{
			var hero = new PlayerCharacter(Vector2I.Zero);
			hero.DerivedStats.AddModifier(DerivedStatId.Armor, new("test", StatModifierOperation.Flat, 5));
			var battle = new CombatEncounter(hero, new[] { new TestEnemy(1, 3), new TestEnemy(2, 4) }, 9,
				new CombatOptions(attackCards: 1, defenseCards: 9));
			battle.BeginRound();
			var animation = new CombatAnimation(battle);
			int shown = 0;
			animation.EventShown += _ => shown++;
			int defense = battle.Hand.ToList().FindIndex(card => card.Kind == CombatCardKind.Defense);
			var before = CombatVisualState.Capture(battle);
			battle.PlayCard(defense);
			animation.Start(before, battle, CombatVisualCommand.PlayCard, defense);
			CombatSmokeTest.Check(animation.View.Block == 0 && battle.Block == 5, "Defense result appeared before animation impact.");
			animation.Tick(0.3);
			CombatSmokeTest.Check(animation.Current?.Kind == CombatAnimationKind.Defense && animation.View.Block == 0, "Card flight skipped the shield animation.");
			animation.Tick(0.3);
			CombatSmokeTest.Check(animation.View.Block == 5 && shown == 1, "Defense impact did not reveal block.");
			animation.Tick(5);
			CombatSmokeTest.Check(!animation.IsBusy && animation.View.ActionPoints == battle.ActionPoints && hero.Health == 10, "Visual animation mutated domain resources.");
			before = CombatVisualState.Capture(battle);
			battle.EndPlayerTurn();
			animation.Start(before, battle, CombatVisualCommand.EndTurn, 0);
			CombatSmokeTest.Check(animation.View.Health[hero.Id] == 10 && hero.Health == 8, "Enemy results appeared simultaneously before their attacks.");
			animation.Tick(0.28);
			CombatSmokeTest.Check(animation.View.Health[hero.Id] == 10 && animation.View.Block == 2, "First enemy did not spend block independently.");
			animation.Tick(0.24);
			CombatSmokeTest.Check(animation.Current?.Event?.ActorId == battle.Enemies[1].Id, "Enemy animation order is incorrect.");
			animation.Tick(0.28);
			CombatSmokeTest.Check(animation.View.Health[hero.Id] == 8 && animation.View.Block == 0, "Second enemy HP impact was not synchronized.");
			animation.Tick(50);
			CombatSmokeTest.Check(!animation.IsBusy && hero.Health == 8 && shown == 3
				&& animation.View.Phase == CombatPhase.RoundPreview, "Long frame applied damage twice or lost a completion.");
			animation.Tick(50);
			CombatSmokeTest.Check(hero.Health == 8 && shown == 3, "Idle timeline replayed old events.");
			GD.Print("Combat animation smoke test passed: card flight, shield impact, sequential enemies, HP/block synchronization, no repeated domain damage.");
			GetTree().Quit();
		}
		catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
	}
}
