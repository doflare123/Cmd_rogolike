using CmdRoguelike.Presentation;
using Godot;

namespace CmdRoguelike.Tests;

public partial class PreparationUiSmokeTest : Node
{
	public override async void _Ready()
	{
		try
		{
			var game = new DungeonGame { WorldSeed = 1701, StartWithDebugPreparation = true };
			AddChild(game);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			var panel = Descendants(game).OfType<InventoryPanel>().Single();
			Check(!Descendants(panel).Any(node => node is Button or ScrollContainer), "GUI widgets returned to terminal UI.");
			Check(!panel.ScreenText.Contains("Базовые бонусы:"), "Sources visible before Alt.");
			Press(game, Key.Alt);
			Check(panel.ScreenText.Contains("Базовые бонусы:"), "Game failed to forward Alt press.");
			game._UnhandledKeyInput(new InputEventKey { Pressed = false, Keycode = Key.Alt });
			Check(!panel.ScreenText.Contains("Базовые бонусы:"), "Game failed to forward Alt release.");
			Press(game, Key.Enter);
			Check(panel.Status.StartsWith("Не выполнены требования"), "Missing requirements feedback.");
			Press(game, Key.Down, Key.Down, Key.Q);
			Check(panel.ScreenText.Contains("надеть: Правое кольцо"), "Ring slot selection failed.");
			Press(game, Key.Enter, Key.Up, Key.Up, Key.Enter, Key.Enter);
			Check(panel.ScreenText.Contains("HP 10/15"), "UI did not update HP.");
			Check(panel.ScreenText.Contains("КОЛОДА / 13 КАРТ") && panel.ScreenText.Contains("Рубящий удар x3")
				&& panel.ScreenText.Contains("1 AP, 2 урона"), "Preparation omitted equipment deck or card details.");
			Press(game, Key.Tab, Key.Down, Key.Enter);
			Check(panel.ScreenText.Contains("HP 10/10"), "Keyboard unequip failed.");
			Press(game, Key.Tab, Key.Up, Key.Up, Key.Enter);
			Check(panel.ScreenText.Contains("HP 10/15"), "Keyboard re-equip failed.");
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			if (DisplayServer.GetName() != "headless")
			{
				await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
				GetViewport().GetTexture().GetImage().SavePng("res://.godot/preparation-ui.png");
			}
			if (DisplayServer.GetName() != "headless")
			{
				GetWindow().Size = new Vector2I(800, 600);
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
				await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
				GetViewport().GetTexture().GetImage().SavePng("res://.godot/preparation-ui-small.png");
			}
			Press(game, Key.F);
			Check(!panel.Visible, "Preparation remained open.");
			game._UnhandledKeyInput(new InputEventKey { Pressed = true, Keycode = Key.I });
			Check(panel.Visible && panel.ScreenText.Contains("ТОЛЬКО ПРОСМОТР"), "Expedition inventory permits edits.");
			Press(game, Key.Enter);
			Check(panel.Status.Contains("только для просмотра"), "Read-only action was not blocked.");
			Press(game, Key.Escape);
			Check(!panel.Visible, "Inventory failed to close.");
			game._UnhandledKeyInput(new InputEventKey { Pressed = true, Keycode = Key.R });
			var newPanel = Descendants(game).OfType<InventoryPanel>().Single();
			Check(newPanel.Visible && newPanel.ScreenText.Contains("HP 10/10"), "Restart did not create fresh preparation.");
			GD.Print("Preparation UI smoke test passed: equip feedback, stats, launch, read-only inventory, restart.");
			GetTree().Quit();
		}
		catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
	}

	private static IEnumerable<Node> Descendants(Node node)
	{
		foreach (Node child in node.GetChildren())
		{
			yield return child;
			foreach (Node descendant in Descendants(child)) yield return descendant;
		}
	}
	private static void Press(DungeonGame game, params Key[] keys)
	{
		foreach (Key key in keys) game._UnhandledKeyInput(new InputEventKey { Pressed = true, Keycode = key });
	}
	private static void Check(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
}
