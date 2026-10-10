using Godot;

namespace CmdRoguelike.Presentation;

internal sealed class GameSettings
{
	private readonly string _path;
	public bool RewardAnimations { get; private set; } = true;
	public GameSettings(string path)
	{
		_path = path;
		var file = new ConfigFile();
		if (file.Load(path) == Error.Ok)
		{
			var value = file.GetValue("visual", "reward_animations", true);
			if (value.VariantType == Variant.Type.Bool) RewardAnimations = value.AsBool();
		}
	}
	public bool SetRewardAnimations(bool enabled)
	{
		var file = new ConfigFile();
		file.SetValue("visual", "reward_animations", enabled);
		if (file.Save(_path) != Error.Ok) return false;
		RewardAnimations = enabled;
		return true;
	}
}

internal sealed partial class SettingsPanel : TerminalOverlay
{
	private readonly GameSettings _settings;
	private readonly Action _close;
	private string _status = "Настройка сохраняется между запусками.";
	public SettingsPanel(GameSettings settings, Action close) { _settings = settings; _close = close; Layer = 4; }
	public void HandleKey(InputEventKey key)
	{
		if (!key.Pressed || key.Echo) return;
		if (key.Keycode is Key.Escape or Key.F2) { _close(); return; }
		if (key.Keycode is Key.Enter or Key.E or Key.Left or Key.Right or Key.A or Key.D)
			_status = _settings.SetRewardAnimations(!_settings.RewardAnimations) ? "Настройка сохранена." : "Не удалось сохранить настройку.";
		Refresh();
	}
	protected override List<Line> Compose()
	{
		var lines = new List<Line>();
		Put(lines, 3, 4, "CMD ROGUELIKE / НАСТРОЙКИ", Accent);
		Box(lines, 2, 8, 108, 13, "АНИМАЦИИ НАГРАДЫ");
		Put(lines, 5, 11, "> Рулетки свойств: " + (_settings.RewardAnimations ? "ВКЛЮЧЕНЫ" : "ОТКЛЮЧЕНЫ"), Gold);
		Put(lines, 5, 14, "Каждый аффикс раскрывается отдельной прокруткой.", Ink);
		Put(lines, 5, 16, "ESC на экране награды мгновенно раскрывает оставшиеся свойства.", Ink);
		Put(lines, 5, 18, "Результат награды одинаков при любом режиме анимаций.", Muted);
		Put(lines, 3, 24, _status, Gold);
		Put(lines, 3, 27, "[ENTER / A / D] переключить  [ESC / F2] вернуться", Accent);
		return lines;
	}
}
