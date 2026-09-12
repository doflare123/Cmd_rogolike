using CmdRoguelike.Presentation;
using Godot;

namespace CmdRoguelike.Tests;

public partial class PreparationUiSmokeTest : Node
{
	public override async void _Ready()
	{
		try
		{
			var game = new DungeonGame { WorldSeed = 1701 };
			AddChild(game);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			var panel = Descendants(game).OfType<InventoryPanel>().Single();
			FindButton(panel, "Надеть: Основная рука").EmitSignal(BaseButton.SignalName.Pressed);
			Check(Labels(panel).Any(text => text.StartsWith("Не выполнены требования")), "Missing requirements feedback.");
			FindButton(panel, "Надеть: Левое кольцо").EmitSignal(BaseButton.SignalName.Pressed);
			FindButton(panel, "Надеть: Основная рука").EmitSignal(BaseButton.SignalName.Pressed);
			FindButton(panel, "Надеть: Корпус").EmitSignal(BaseButton.SignalName.Pressed);
			Check(Labels(panel).Any(text => text.Contains("HP 10/15")), "UI did not update HP.");
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			if (DisplayServer.GetName() != "headless")
			{
				await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
				GetViewport().GetTexture().GetImage().SavePng("res://.godot/preparation-ui.png");
			}
			FindButton(panel, "Отправиться в экспедицию").EmitSignal(BaseButton.SignalName.Pressed);
			Check(!panel.Visible, "Preparation remained open.");
			game._UnhandledKeyInput(new InputEventKey { Pressed = true, Keycode = Key.I });
			Check(panel.Visible && !Descendants(panel).OfType<Button>().Any(button => button.Text.StartsWith("Надеть") || button.Text.StartsWith("Снять")), "Expedition inventory permits edits.");
			FindButton(panel, "Вернуться к карте").EmitSignal(BaseButton.SignalName.Pressed);
			Check(!panel.Visible, "Inventory failed to close.");
			game._UnhandledKeyInput(new InputEventKey { Pressed = true, Keycode = Key.R });
			var newPanel = Descendants(game).OfType<InventoryPanel>().Single();
			Check(newPanel.Visible && Labels(newPanel).Any(text => text.Contains("HP 10/10")), "Restart did not create fresh preparation.");
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
	private static IEnumerable<string> Labels(Node node) => Descendants(node).OfType<Label>().Select(label => label.Text);
	private static Button FindButton(Node node, string text) => Descendants(node).OfType<Button>().Single(button => button.Text == text);
	private static void Check(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}
}
