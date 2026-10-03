using CmdRoguelike.Core;
using CmdRoguelike.Domain.Combat;
using CmdRoguelike.Domain.Stats;
using CmdRoguelike.Presentation;
using Godot;

namespace CmdRoguelike.Tests;

public partial class CombatUiSmokeTest : Node
{
	public override async void _Ready()
	{
		try
		{
			var game = new DungeonGame { WorldSeed = 1701, EnemyRoomChance = 1 };
			AddChild(game);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			Press(game, Key.Down, Key.Down, Key.Enter); // Ring.
			Press(game, Key.Up, Key.Enter); // Armor.
			Press(game, Key.F);
			var map = game.CurrentMap!;
			ExpeditionTestDriver.FindCombat(map, direction => Press(game, direction switch
			{
				CardinalDirection.Up => Key.Up, CardinalDirection.Right => Key.Right,
				CardinalDirection.Down => Key.Down, _ => Key.Left,
			}));
			CombatSmokeTest.Check(game.IsEnteringCombat && !game.GetChildren().OfType<CombatPanel>().Any(), "Combat skipped the discovered-room presentation.");
			int openingHealth = map.Player.Health;
			Press(game, Key.Enter, Key.Space, Key.Right);
			CombatSmokeTest.Check(map.Combat!.Phase == CombatPhase.RoundPreview && map.Player.Health == openingHealth, "Entrance input triggered combat actions.");
			await Capture("combat-entry.png");
			await WaitForVisuals(game);
			var panel = game.GetChildren().OfType<CombatPanel>().Single();
			CombatSmokeTest.Check(panel.ScreenText.Contains("НАМЕРЕНИЯ") && panel.ScreenText.Contains("-> @"), "Opening screen lacks intent preview.");
			CombatSmokeTest.Check(panel.ScreenText.Contains("ОЧКИ ДЕЙСТВИЯ") && panel.ScreenText.Contains("ГЕРОЙ / ХАРАКТЕРИСТИКИ"), "AP and stats have no separate plaques.");
			await Act(game, Key.E);
			CombatSmokeTest.Check(map.Combat!.Phase == CombatPhase.PlayerTurn && panel.ScreenText.Contains("AP 3/3"), "Confirm did not start player's turn.");
			CombatSmokeTest.Check(!panel.ScreenText.Split('\n')[42].Contains("Ход героя") && panel.ScreenText.Split('\n')[44].Contains("Ход героя"), "Combat message overlaps the full hand's card thickness.");
			await Capture("combat-full-hand.png");
			int handCount = map.Combat.Hand.Count;
			await Act(game, Key.Q);
			CombatSmokeTest.Check(map.Combat.Hand.Count == handCount && panel.ScreenText.Contains("без расхода AP"), "Keyboard replacement failed.");
			Press(game, Key.Q);
			CombatSmokeTest.Check(panel.ScreenText.Contains("раз в два"), "Replacement cooldown has no feedback.");
			Vector2I origin = map.Player.Position;
			Press(game, Key.Down);
			int attackIndex = map.Combat.Hand.ToList().FindIndex(card => card.Kind == CombatCardKind.Attack);
			Select(game, attackIndex);
			Press(game, Key.Enter);
			CombatSmokeTest.Check(panel.IsAnimating && panel.Animation.Current?.Kind == CombatAnimationKind.PlayCard, "Played card did not start a flight animation.");
			Press(game, Key.Enter, Key.Space, Key.Q);
			CombatSmokeTest.Check(map.Combat.ActionPoints == 2 && map.Combat.Phase == CombatPhase.PlayerTurn, "Animation accepted overlapping commands.");
			await Delay(0.14);
			await Capture("combat-card-play.png");
			Press(game, Key.L);
			float pausedProgress = panel.Animation.Progress;
			Press(game, Key.Enter, Key.Space, Key.Q, Key.I);
			await Delay(0.2);
			CombatSmokeTest.Check(panel.IsJournalOpen && panel.Animation.Progress == pausedProgress && map.Combat.ActionPoints == 2,
				"Journal failed to pause presentation or allowed gameplay input.");
			Press(game, Key.Escape);
			CombatSmokeTest.Check(!panel.IsJournalOpen && map.Combat!.Phase == CombatPhase.PlayerTurn, "Escape did not close only the journal.");
			await Delay(0.35);
			await Capture("combat-attack.png");
			await WaitForVisuals(game);
			CombatSmokeTest.Check(map.Combat.ActionPoints == 2 && map.Player.Position == origin, "Card input moved the world player or failed to spend AP.");
			CombatSmokeTest.Check(!panel.ScreenText.Contains("+1 Защита") && !panel.ScreenText.Contains("+2 Защита"), "Card labels still look like armor bonuses.");
			int defenseIndex = map.Combat.Hand.ToList().FindIndex(card => card.Kind == CombatCardKind.Defense);
			CombatSmokeTest.Check(defenseIndex >= 0, "Fixture has no defense card.");
			Select(game, defenseIndex);
			Press(game, Key.Enter);
			await Delay(0.56);
			await Capture("combat-defense.png");
			await WaitForVisuals(game);
			Press(game, Key.L);
			CombatSmokeTest.Check(panel.ScreenText.Contains("Герой: Атака") && panel.ScreenText.Contains("Герой: Защита")
				&& panel.ScreenText.Contains("замена"), "Journal omitted played actions or replacement.");
			await Capture("combat-journal.png");
			Press(game, Key.L);
			Press(game, Key.I);
			var inventory = game.GetChildren().OfType<InventoryPanel>().Single();
			CombatSmokeTest.Check(inventory.Visible && inventory.Layer > panel.Layer, "Inventory is behind battle screen.");
			Press(game, Key.Escape);
			CombatSmokeTest.Check(!inventory.Visible && map.Combat.Phase == CombatPhase.PlayerTurn, "Inventory close changed battle.");
			await Capture("combat-ui.png");
			if (DisplayServer.GetName() != "headless")
			{
				GetWindow().Size = new Vector2I(800, 600);
				await Capture("combat-ui-small.png");
				game._UnhandledInput(new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Left, Position = panel.JournalButtonBounds.GetCenter() });
				CombatSmokeTest.Check(panel.IsJournalOpen, "Scaled journal button ignored mouse click.");
				await Capture("combat-journal-small.png");
				Press(game, Key.Escape);
			}
			Press(game, Key.Space);
			await Delay(0.28);
			await Capture("combat-enemy-attack.png");
			await WaitForVisuals(game);
			CombatSmokeTest.Check(map.Combat.Phase == CombatPhase.RoundPreview && panel.ScreenText.Contains("НАМЕРЕНИЯ"), "End turn did not show next-round preview.");
			// Finish through keyboard controls, then acknowledge victory.
			map.Player.DerivedStats.AddModifier(DerivedStatId.Armor, new("test", StatModifierOperation.Flat, 100));
			for (int turn = 0; turn < 50 && !map.Combat.IsFinished; turn++)
			{
				await Act(game, Key.Enter);
				while (map.Combat.ActionPoints > 0 && !map.Combat.IsFinished)
				{
					int index = map.Combat.Hand.ToList().FindIndex(card => map.Combat.Block == 0
						? card.Kind == CombatCardKind.Defense : card.Kind == CombatCardKind.Attack);
					if (index < 0) index = map.Combat.Hand.ToList().FindIndex(card => card.Kind == CombatCardKind.Attack);
					if (index < 0) break;
					Select(game, index);
					await Act(game, Key.Enter);
				}
				if (!map.Combat.IsFinished) await Act(game, Key.Space);
			}
			CombatSmokeTest.Check(map.Combat.Phase == CombatPhase.Victory && panel.ScreenText.Contains("ПОБЕДА"), "Keyboard combat failed to win.");
			CombatSmokeTest.Check(panel.JournalEntries.Count > 8 && panel.JournalEntries[0].Contains("Ход героя 1"), "Journal truncated earlier encounter actions.");
			Press(game, Key.L, Key.Home);
			CombatSmokeTest.Check(panel.ScreenText.Contains("Ход героя 1"), "Journal cannot scroll to the beginning.");
			Press(game, Key.End);
			CombatSmokeTest.Check(panel.ScreenText.Contains("Победа.") && panel.ScreenText.Contains("Атака -> герой"), "Journal omitted enemy actions or victory.");
			Press(game, Key.Escape);
			Press(game, Key.Enter);
			CombatSmokeTest.Check(map.Combat is null && !game.GetChildren().OfType<CombatPanel>().Any(), "Victory did not close battle UI.");
			Press(game, Key.R);
			CombatSmokeTest.Check(game.CurrentMap is null && game.GetChildren().OfType<InventoryPanel>().Single().Visible, "Reset failed after battle.");
			Press(game, Key.F);
			FindCombat(game);
			Press(game, Key.R);
			await Delay(1.6);
			CombatSmokeTest.Check(game.CurrentMap is null && !game.IsEnteringCombat && !game.GetChildren().OfType<CombatPanel>().Any(), "Reset left a delayed combat entrance.");
			Press(game, Key.F);
			FindCombat(game);
			await WaitForVisuals(game);
			await Act(game, Key.Enter);
			Press(game, Key.Enter);
			CombatSmokeTest.Check(game.GetChildren().OfType<CombatPanel>().Single().IsAnimating, "Reset fixture did not start an animation.");
			Press(game, Key.R);
			await Delay(1.2);
			CombatSmokeTest.Check(game.CurrentMap is null && !game.GetChildren().OfType<CombatPanel>().Any(), "Reset retained the old animation panel.");
			GD.Print("Combat UI smoke test passed: entrance, AP/stats plaques, sequential card/attack/defense animations, input lock, inventory, victory, restart.");
			GetTree().Quit();
		}
		catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
	}
	private async Task Capture(string name)
	{
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		if (DisplayServer.GetName() == "headless") return;
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		GetViewport().GetTexture().GetImage().SavePng("res://.godot/" + name);
	}
	private static void Press(DungeonGame game, params Key[] keys)
	{
		foreach (Key key in keys) game._UnhandledKeyInput(new InputEventKey { Pressed = true, Keycode = key });
	}
	private static void Select(DungeonGame game, int index)
	{
		for (int i = 0; i < 10; i++) Press(game, Key.Left);
		for (int i = 0; i < index; i++) Press(game, Key.Right);
	}
	private static void FindCombat(DungeonGame game)
	{
		ExpeditionTestDriver.FindCombat(game.CurrentMap!, direction => Press(game, direction switch
		{
			CardinalDirection.Up => Key.Up, CardinalDirection.Right => Key.Right,
			CardinalDirection.Down => Key.Down, _ => Key.Left,
		}));
	}
	private async Task Delay(double duration)
	{
		await ToSignal(GetTree().CreateTimer(duration), SceneTreeTimer.SignalName.Timeout);
	}
	private async Task Act(DungeonGame game, Key key)
	{
		Press(game, key);
		await WaitForVisuals(game);
	}
	private async Task WaitForVisuals(DungeonGame game)
	{
		var timer = System.Diagnostics.Stopwatch.StartNew();
		while (game.IsEnteringCombat || game.GetChildren().OfType<CombatPanel>().Any(panel => panel.IsAnimating))
		{
			if (timer.Elapsed.TotalSeconds > 15) throw new InvalidOperationException("Combat presentation failed to finish.");
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}
	}
}
