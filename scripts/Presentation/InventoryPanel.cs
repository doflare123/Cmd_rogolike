using CmdRoguelike.Domain.Entities;
using CmdRoguelike.Domain.Items;
using CmdRoguelike.Domain.Stats;
using Godot;

namespace CmdRoguelike.Presentation;

/// <summary>Keyboard-driven terminal view. Only HandleKey issues domain commands.</summary>
internal sealed partial class InventoryPanel : CanvasLayer
{
	private const int Columns = 112, Rows = 35;
	private readonly PlayerCharacter _hero;
	private readonly Action _start, _close;
	private readonly Node2D _canvas = new();
	private readonly SystemFont _font = new() { FontNames = new[] { "Consolas", "DejaVu Sans Mono", "Liberation Mono", "Courier New" } };
	private int _bagIndex, _equipmentIndex, _targetIndex;
	private bool _equipmentFocused;
	internal string Status { get; private set; } = "Выберите предмет. Кольцо силы поможет выполнить требования меча.";
	internal string ScreenText => string.Join('\n', Compose().Select(line => line.Text));
	private static readonly Color Ink = new("cad5cc"), Muted = new("61786d"), Accent = new("93d8a0"), Amber = new("e4b86a");
	private readonly record struct Line(int X, int Y, string Text, Color Color);

	public InventoryPanel(PlayerCharacter hero, Action start, Action close)
	{
		_hero = hero;
		_start = start;
		_close = close;
	}
	public override void _Ready()
	{
		AddChild(_canvas);
		_canvas.Draw += Render;
		GetViewport().SizeChanged += Refresh;
		Refresh();
	}
	public override void _ExitTree() => GetViewport().SizeChanged -= Refresh;
	public void Refresh()
	{
		_bagIndex = Math.Clamp(_bagIndex, 0, Math.Max(0, Bag().Length - 1));
		_equipmentIndex = Math.Clamp(_equipmentIndex, 0, Math.Max(0, _hero.Inventory.Body.Slots.Count - 1));
		_canvas.QueueRedraw();
	}
	public void HandleKey(InputEventKey key)
	{
		if (!key.Pressed) return;
		if (key.Keycode is Key.Up or Key.W or Key.Down or Key.S)
		{
			int delta = key.Keycode is Key.Up or Key.W ? -1 : 1;
			if (_equipmentFocused) _equipmentIndex += delta;
			else _bagIndex += delta;
			_targetIndex = 0;
		}
		else if (!key.Echo)
		{
			switch (key.Keycode)
			{
				case Key.Tab: _equipmentFocused = !_equipmentFocused; break;
				case Key.Left: case Key.A: _equipmentFocused = false; break;
				case Key.Right: case Key.D: _equipmentFocused = true; break;
				case Key.Q: _targetIndex++; break;
				case Key.Enter: case Key.E: ActivateSelection(); break;
				case Key.F: if (!_hero.Inventory.IsLocked) _start(); break;
				case Key.I: case Key.Escape:
					if (_hero.Inventory.IsLocked) _close();
					else if (key.Keycode == Key.Escape) GetTree().Quit();
					break;
			}
		}
		Refresh();
	}
	private ItemInstance[] Bag() => _hero.Inventory.Items.Where(item => item.Location == ItemLocation.Backpack).ToArray();
	private ItemInstance? Selected() => !_equipmentFocused ? Bag().ElementAtOrDefault(_bagIndex)
		: _hero.Inventory.Items.FirstOrDefault(item => item.Slot == _hero.Inventory.Body.Slots.ElementAtOrDefault(_equipmentIndex));
	private EquipmentSlot[] Targets(ItemInstance item) => item.Definition.Slots.Where(_hero.Inventory.Body.Slots.Contains).ToArray();
	private void ActivateSelection()
	{
		if (_hero.Inventory.IsLocked) { Status = "В экспедиции снаряжение доступно только для просмотра."; return; }
		var item = Selected();
		if (item is null) { Status = "Слот пуст."; return; }
		InventoryResult result;
		if (_equipmentFocused) result = _hero.Inventory.TryUnequip(item.Id);
		else
		{
			var targets = Targets(item);
			if (targets.Length == 0) { Status = "Этот предмет нельзя надеть."; return; }
			result = _hero.Inventory.TryEquip(item.Id, targets[_targetIndex % targets.Length]);
		}
		Status = result switch
		{
			InventoryResult.Success => "Снаряжение обновлено.",
			InventoryResult.RequirementsNotMet => "Не выполнены требования. Собственные бонусы предмета ещё не учитываются.",
			InventoryResult.Occupied => "Слот занят. Снимите предмет или выберите другой слот клавишей Q.",
			InventoryResult.Full => "Рюкзак полон: предмет остаётся на месте.",
			InventoryResult.Locked => "Снаряжение можно менять только при подготовке.",
			_ => "Предмет или слот недоступен.",
		};
		_targetIndex = 0;
	}
	private List<Line> Compose()
	{
		var lines = new List<Line>();
		void Put(int x, int y, string text, Color color, int width = Columns) =>
			lines.Add(new Line(x, y, Clip(text, Math.Min(width, Columns - x)), color));
		void Box(int x, int y, int width, int height, string title, bool focused)
		{
			Color color = focused ? Accent : Muted;
			Put(x, y, "+" + Clip("-- " + title + " ", width - 2).PadRight(width - 2, '-') + "+", color);
			for (int row = 1; row < height - 1; row++)
			{
				Put(x, y + row, "|", color);
				Put(x + width - 1, y + row, "|", color);
			}
			Put(x, y + height - 1, "+" + new string('-', width - 2) + "+", color);
		}
		bool locked = _hero.Inventory.IsLocked;
		Put(2, 0, "CMD ROGUELIKE / " + (locked ? "ЭКСПЕДИЦИЯ / ИНВЕНТАРЬ" : "БАЗА / ПОДГОТОВКА"), Accent);
		Put(2, 2, $"@ {_hero.Name}   HP {_hero.Health}/{_hero.MaxHealth}   MP {_hero.Resources.Mana}/{_hero.Resources.MaxMana}   Раса: null", Ink);
		var attributes = Enum.GetValues<AttributeId>();
		for (int i = 0; i < attributes.Length; i++)
			Put(2 + (i % 4) * 27, 4 + i / 4, $"{AttributeName(attributes[i])}: {_hero.Attributes.GetValue(attributes[i])}", Ink, 26);
		Box(2, 7, 38, 17, $"РЮКЗАК {_hero.Inventory.UsedSlots}/{_hero.Inventory.Capacity}", !_equipmentFocused);
		Box(41, 7, 69, 17, "ЭКИПИРОВКА", _equipmentFocused);
		var bag = Bag();
		int first = Math.Max(0, _bagIndex - 13);
		for (int i = first; i < Math.Min(bag.Length, first + 14); i++)
		{
			var item = bag[i];
			Put(3, 8 + i - first, $"{(!_equipmentFocused && i == _bagIndex ? '>' : ' ')} {i + 1:00} {item.Definition.Name} x{item.Quantity}",
				!_equipmentFocused && i == _bagIndex ? Accent : Ink, 35);
		}
		if (bag.Length == 0) Put(5, 9, "(пусто)", Muted);
		Put(4, 22, "[TAB] переключить панель", Muted, 34);
		for (int i = 0; i < _hero.Inventory.Body.Slots.Count; i++)
		{
			var slot = _hero.Inventory.Body.Slots[i];
			var item = _hero.Inventory.Items.FirstOrDefault(item => item.Slot == slot);
			bool selected = _equipmentFocused && i == _equipmentIndex;
			string state = item is null ? "" : item.State == EquipmentState.Active ? "[OK]" : "[DORMANT]";
			Put(42, 8 + i, $"{(selected ? '>' : ' ')} {SlotName(slot),-15} {Clip(item?.Definition.Name ?? "-", 29),-29} {state}",
				selected ? Accent : item?.State == EquipmentState.Dormant ? Amber : item is null ? Muted : Ink, 66);
		}
		Box(2, 24, 108, 7, "ПРЕДМЕТ", false);
		var chosen = Selected();
		if (chosen is null) Put(4, 26, "Пустой слот. Выберите предмет в рюкзаке.", Muted);
		else
		{
			Put(4, 25, $"{chosen.Definition.Name} x{chosen.Quantity}", Accent, 103);
			Put(4, 26, "Требования: " + JoinOrNone(chosen.Definition.Requirements.Select(pair => $"{AttributeName(pair.Key)} {pair.Value}")), Ink, 103);
			Put(4, 27, "Бонусы: " + JoinOrNone(chosen.Definition.AttributeBonuses.Select(pair => $"+{pair.Value} {AttributeName(pair.Key)}")
				.Concat(chosen.Definition.StatBonuses.Select(pair => $"+{pair.Value} макс. {(pair.Key == DerivedStatId.MaxHealth ? "HP" : "MP")}"))), Ink, 103);
			var targets = Targets(chosen);
			Put(4, 28, locked ? "[ТОЛЬКО ПРОСМОТР] Смена снаряжения доступна на базе."
				: _equipmentFocused ? "[ENTER / E] снять в рюкзак"
				: targets.Length == 0 ? $"Материал. Максимальный стек: {chosen.Definition.MaximumStack}."
				: $"[ENTER / E] надеть: {SlotName(targets[_targetIndex % targets.Length])}" + (targets.Length > 1 ? "   [Q] другой слот" : ""), Amber, 103);
			if (chosen.Location == ItemLocation.Equipment && chosen.State == EquipmentState.Dormant)
				Put(4, 29, "DORMANT: требования не выполнены; бонусы отключены, слот занят.", Amber, 103);
		}
		Put(2, 32, "> " + Status, Amber, 108);
		Put(2, 34, locked ? "[W/S] выбор  [A/D/TAB] панель  [I/ESC] к карте"
			: "[W/S] выбор  [A/D/TAB] панель  [ENTER/E] действие  [F] в экспедицию  [ESC] выход", Accent, 108);
		return lines;
	}
	private void Render()
	{
		Vector2 viewport = GetViewport().GetVisibleRect().Size;
		_canvas.DrawRect(new Rect2(Vector2.Zero, viewport), new Color("080b0f"));
		float cell = _font.GetStringSize("M", fontSize: 16).X;
		const float rowHeight = 18;
		float scale = Math.Min(1.25f, Math.Min(viewport.X / ((Columns + 2) * cell), viewport.Y / ((Rows + 2) * rowHeight)));
		Vector2 origin = (viewport - new Vector2(Columns * cell, Rows * rowHeight) * scale) / 2;
		_canvas.DrawSetTransform(origin, 0, Vector2.One * scale);
		foreach (var line in Compose())
			for (int i = 0; i < line.Text.Length; i++)
				if (line.Text[i] != ' ')
					_canvas.DrawString(_font, new Vector2((line.X + i) * cell, line.Y * rowHeight + 14), line.Text[i].ToString(),
						HorizontalAlignment.Left, -1, 16, line.Color);
		_canvas.DrawSetTransform(Vector2.Zero);
	}
	private static string Clip(string value, int width) => value.Length <= width ? value : value[..Math.Max(0, width - 3)] + "...";
	private static string JoinOrNone(IEnumerable<string> values) => values.Any() ? string.Join(", ", values) : "нет";
	private static string AttributeName(AttributeId stat) => stat switch
	{
		AttributeId.Strength => "Сила", AttributeId.Dexterity => "Ловкость", AttributeId.Constitution => "Телосложение",
		AttributeId.Intelligence => "Интеллект", AttributeId.Wisdom => "Мудрость", AttributeId.Willpower => "Воля",
		AttributeId.Perception => "Восприятие", AttributeId.Luck => "Удача", _ => stat.ToString(),
	};
	private static string SlotName(EquipmentSlot slot) => slot switch
	{
		EquipmentSlot.Head => "Голова", EquipmentSlot.Torso => "Корпус", EquipmentSlot.Hands => "Кисти",
		EquipmentSlot.Legs => "Ноги", EquipmentSlot.Feet => "Ступни", EquipmentSlot.Back => "Спина",
		EquipmentSlot.Belt => "Пояс", EquipmentSlot.Amulet => "Амулет", EquipmentSlot.RingLeft => "Левое кольцо",
		EquipmentSlot.RingRight => "Правое кольцо", EquipmentSlot.MainHand => "Основная рука",
		EquipmentSlot.OffHand => "Вторая рука", EquipmentSlot.Talisman => "Талисман",
		EquipmentSlot.Backpack => "Рюкзак", _ => slot.ToString(),
	};
}
