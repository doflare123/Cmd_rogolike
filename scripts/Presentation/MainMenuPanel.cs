using Godot;

namespace CmdRoguelike.Presentation;

internal enum MenuAction { Play, Settings, Creator, Encyclopedia, Quit }
internal sealed record SlotOverview(int Slot, string Text, string? Error = null);

internal sealed partial class MainMenuPanel : TerminalOverlay
{
	private readonly Action<MenuAction, int> _action;
	private readonly SlotOverview[] _slots;
	private int _index, _slot;
	private bool _choosing;
	internal string Status { get; private set; } = "Три независимых прохождения. У каждого своя база, герой, запасы и открытия.";
	private static readonly string[] Entries = { "Играть / выбрать прохождение", "Настройки", "О создателе", "Энциклопедия", "Выйти" };
	public MainMenuPanel(SlotOverview[] slots, Action<MenuAction, int> action) { _slots = slots; _action = action; Layer = 2; }
	internal void SetStatus(string status) { Status = status; Refresh(); }
	public void HandleKey(InputEventKey key)
	{
		if (!key.Pressed) return;
		if (key.Keycode is Key.W or Key.Up or Key.S or Key.Down)
		{
			int delta = key.Keycode is Key.W or Key.Up ? -1 : 1;
			if (_choosing) _slot = Math.Clamp(_slot + delta, 0, 2); else _index = Math.Clamp(_index + delta, 0, Entries.Length - 1);
		}
		else if (!key.Echo && key.Keycode is Key.Enter or Key.E or Key.Space)
		{
			if (_choosing)
			{
				if (_slots[_slot].Error is string error) Status = error;
				else _action(MenuAction.Play, _slot + 1);
			}
			else if (_index == 0) _choosing = true;
			else _action((MenuAction)_index, 1);
		}
		else if (!key.Echo && key.Keycode == Key.Escape && _choosing) _choosing = false;
		Refresh();
	}
	protected override List<Line> Compose()
	{
		var lines = new List<Line>();
		Box(lines, 9, 3, 94, 33, _choosing ? "ВЫБОР ПРОХОЖДЕНИЯ" : "ГЛАВНОЕ МЕНЮ");
		Put(lines, 23, 7, "C M D   R O G U E L I K E", Accent);
		Put(lines, 23, 10, "Исследуй. Сражайся. Вернись с добычей.", Muted);
		if (_choosing)
		{
			for (int i = 0; i < 3; i++)
			{
				Put(lines, 15, 15 + i * 5, $"{(i == _slot ? '>' : ' ')} ПРОХОЖДЕНИЕ {i + 1}", i == _slot ? Accent : Ink, 84);
				Put(lines, 18, 17 + i * 5, _slots[i].Text, _slots[i].Error is null ? Muted : Gold, 80);
			}
		}
		else for (int i = 0; i < Entries.Length; i++) Put(lines, 24, 15 + i * 3, $"{(i == _index ? '>' : ' ')} {Entries[i]}", i == _index ? Accent : Ink, 75);
		Put(lines, 3, 38, Status, Gold, 106);
		Put(lines, 23, 40, "[W/S] выбор  [ENTER/E] открыть  [ESC] назад", Accent, 80);
		return lines;
	}
}
