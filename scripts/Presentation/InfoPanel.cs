using Godot;

namespace CmdRoguelike.Presentation;

internal sealed partial class InfoPanel : TerminalOverlay
{
	private readonly string _title;
	private readonly string[] _messages;
	private readonly Action _close;
	private readonly Action? _confirm;
	public InfoPanel(string title, string[] messages, Action close, Action? confirm = null)
	{ _title = title; _messages = messages; _close = close; _confirm = confirm; Layer = 5; }
	public void HandleKey(InputEventKey key)
	{
		if (!key.Pressed || key.Echo) return;
		if (key.Keycode == Key.Escape) _close();
		else if (key.Keycode is Key.Enter or Key.E) { if (_confirm is not null) _confirm(); else _close(); }
	}
	protected override List<Line> Compose()
	{
		var lines = new List<Line>();
		Box(lines, 4, 8, 104, 24, _title);
		for (int i = 0; i < _messages.Length; i++) Put(lines, 8, 12 + i * 2, _messages[i], i == 0 ? Accent : Ink, 96);
		Put(lines, 8, 28, _confirm is null ? "[ENTER/ESC] назад" : "[ENTER] подтвердить  [ESC] продолжить", Gold, 96);
		return lines;
	}
}
