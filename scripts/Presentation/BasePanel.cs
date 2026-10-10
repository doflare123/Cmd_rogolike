using CmdRoguelike.Core;
using CmdRoguelike.Domain.Campaign;
using CmdRoguelike.World;
using Godot;

namespace CmdRoguelike.Presentation;

internal sealed partial class BasePanel : TerminalOverlay
{
	private readonly CampaignSession _campaign;
	private readonly Action _inventory, _storage, _depart, _save, _menu;
	private bool _building;
	private Vector2I _cursor;
	private Guid? _moving;
	private int _kind;
	private static readonly BaseObjectKind[] BuildKinds = { BaseObjectKind.Storage, BaseObjectKind.Campfire, BaseObjectKind.Workbench, BaseObjectKind.Healer };
	internal string Status { get; private set; } = "Перед походом подготовьте экипировку. Огненный факел и зелья лежат в хранилище.";
	public BasePanel(CampaignSession campaign, Action inventory, Action storage, Action depart, Action save, Action menu)
	{
		_campaign = campaign; _inventory = inventory; _storage = storage; _depart = depart; _save = save; _menu = menu; Layer = 1;
	}
	internal void SetStatus(string status) { Status = status; Refresh(); }
	public void HandleKey(InputEventKey key)
	{
		if (!key.Pressed) return;
		if (_building)
		{
			if (Movement(key.Keycode) is CardinalDirection d)
			{
				var next = _cursor + d.ToOffset();
				if (_campaign.Layout.IsFloor(next)) _cursor = next;
			}
			else if (!key.Echo && key.Keycode is Key.Escape or Key.B) { _building = false; _moving = null; }
			else if (!key.Echo && key.Keycode == Key.Tab) _kind = (_kind + 1) % BuildKinds.Length;
			else if (!key.Echo && key.Keycode == Key.Delete && _moving is null)
				Status = _campaign.Layout.TryRemove(_cursor) ? "Объект убран." : "Ворота и последний сундук должны остаться.";
			else if (!key.Echo && key.Keycode is Key.Enter or Key.E)
			{
				var item = _campaign.Layout.At(_cursor);
				if (_moving is null && item is not null) { _moving = item.Id; Status = "Выберите свободную клетку и нажмите ENTER для переноса."; }
				else if (_campaign.Layout.TryPlace(BuildKinds[_kind], _cursor, _campaign.Hero.Position, _moving))
				{ _moving = null; Status = "Объект размещён. Все сундуки открывают общее хранилище."; }
			else Status = "Клетка недоступна или объект перекрывает проход.";
			}
			Refresh(); return;
		}
		if (Movement(key.Keycode) is CardinalDirection direction) _campaign.Base!.TryMove(direction);
		else if (!key.Echo) switch (key.Keycode)
		{
			case Key.I: case Key.F: _inventory(); break;
			case Key.C: _storage(); break;
			case Key.F5: _save(); break;
			case Key.Escape: _menu(); break;
			case Key.B: _building = true; _cursor = _campaign.Hero.Position; _moving = null; Status = "TAB выбирает объект; ENTER на существующем объекте начинает перенос."; break;
			case Key.E: case Key.Enter: case Key.Space: Interact(); break;
		}
		Refresh();
	}
	private void Interact()
	{
		var item = _campaign.Base!.NearbyObject();
		if (item is null) { Status = "Подойдите к сундуку, костру или воротам экспедиции."; return; }
		switch (item.Kind)
		{
			case BaseObjectKind.Storage: _storage(); break;
			case BaseObjectKind.ExpeditionGate: _depart(); break;
			case BaseObjectKind.Campfire: case BaseObjectKind.Healer: _campaign.Rest(); Status = "Отдых завершён. HP и MP восстановлены."; break;
			case BaseObjectKind.Workbench: Status = "Верстак установлен. Это место для будущих рецептов и изготовления предметов."; break;
		}
	}
	protected override List<Line> Compose()
	{
		var lines = new List<Line>();
		Put(lines, 3, 1, $"CMD ROGUELIKE / {_campaign.Name.ToUpperInvariant()} / БАЗА", Accent);
		Put(lines, 3, 3, $"@ {_campaign.Hero.Name}  HP {_campaign.Hero.Health}/{_campaign.Hero.MaxHealth}  Ступень {_campaign.WorldTier}  Побед над дубом {_campaign.OakVictories}", Ink);
		const int ox = 3, oy = 6;
		for (int y = 0; y < BaseLayout.Height; y++)
		{
			string border = y == 0 || y == BaseLayout.Height - 1 ? new string('#', BaseLayout.Width) : "#" + new string(' ', BaseLayout.Width - 2) + "#";
			Put(lines, ox, oy + y, border, Muted, BaseLayout.Width);
		}
		foreach (var obj in _campaign.Layout.Objects) Put(lines, ox + obj.Position.X, oy + obj.Position.Y, Glyph(obj.Kind), obj.Kind == BaseObjectKind.ExpeditionGate ? new("b996ff") : Gold, 1);
		Put(lines, ox + _campaign.Hero.Position.X, oy + _campaign.Hero.Position.Y, "@", Accent, 1);
		if (_building) Put(lines, ox + _cursor.X, oy + _cursor.Y, "X", Colors.White, 1);
		Box(lines, 70, 6, 40, 25, _building ? "РАССТАНОВКА" : "ПОСЕЛЕНИЕ");
		Put(lines, 72, 9, "C  Сундук / общие запасы", Gold, 36);
		Put(lines, 72, 11, "^  Костёр / отдых", Gold, 36);
		Put(lines, 72, 13, "O  Ворота экспедиции", new("b996ff"), 36);
		Put(lines, 72, 15, "T  Верстак", Gold, 36);
		Put(lines, 72, 17, "h  Целитель", Gold, 36);
		Put(lines, 72, 20, _building ? "Выбран: " + ObjectName(BuildKinds[_kind]) : "[E] действие рядом с объектом", Accent, 36);
		Put(lines, 72, 22, _moving is not null ? "Перенос объекта: выберите клетку" : "[B] расставить объекты", Ink, 36);
		Put(lines, 72, 25, $"Запасы: {_campaign.Storage.Items.Count}/{_campaign.Storage.Capacity}", Ink, 36);
		Put(lines, 72, 27, $"Погибшие герои: {_campaign.FallenHeroes}", Muted, 36);
		Put(lines, 3, 33, "Мудрый дуб: крепкая кора, слабость к огню, тяжёлый удар каждый третий раунд.", Gold);
		Put(lines, 3, 35, "Сохранение доступно на базе. Перед походом и после возврата создаётся контрольная точка.", Muted);
		Put(lines, 3, 37, Status, Gold, 106);
		Put(lines, 3, 40, _building ? "[WASD] курсор  [TAB] объект  [ENTER] поставить / перенести  [DEL] убрать  [B/ESC] завершить"
			: "[WASD] ход  [E] действие  [I/F] экипировка  [C] запасы  [B] расстановка  [F5] сохранить  [ESC] меню", Accent, 106);
		return lines;
	}
	internal static string Glyph(BaseObjectKind kind) => kind switch { BaseObjectKind.Storage => "C", BaseObjectKind.Campfire => "^", BaseObjectKind.ExpeditionGate => "O", BaseObjectKind.Workbench => "T", _ => "h" };
	internal static string ObjectName(BaseObjectKind kind) => kind switch { BaseObjectKind.Storage => "Сундук", BaseObjectKind.Campfire => "Костёр", BaseObjectKind.ExpeditionGate => "Ворота", BaseObjectKind.Workbench => "Верстак", _ => "Целитель" };
	internal static CardinalDirection? Movement(Key key) => key switch
	{ Key.W or Key.Up => CardinalDirection.Up, Key.D or Key.Right => CardinalDirection.Right, Key.S or Key.Down => CardinalDirection.Down, Key.A or Key.Left => CardinalDirection.Left, _ => null };
}
