using CmdRoguelike.Domain.Entities;
using CmdRoguelike.Domain.Items;
using CmdRoguelike.Domain.Stats;
using CmdRoguelike.Domain.Combat;
using Godot;

namespace CmdRoguelike.Presentation;

/// <summary>Keyboard-driven terminal view. Only HandleKey issues domain commands.</summary>
internal sealed partial class InventoryPanel : CanvasLayer
{
	private const int Columns = 112, Rows = 47;
	private readonly PlayerCharacter _hero;
	private readonly Action _start, _close;
	private readonly Func<Guid, ConsumableUseResult> _useConsumable;
	private readonly bool _closeOnBase;
	private readonly Node2D _canvas = new();
	private readonly SystemFont _font = new() { FontNames = new[] { "Consolas", "DejaVu Sans Mono", "Liberation Mono", "Courier New" } };
	private int _bagIndex, _equipmentIndex, _targetIndex;
	private bool _equipmentFocused;
	private bool _showDetails;
	internal string Status { get; private set; } = "Выберите предмет. Кольцо силы поможет выполнить требования меча.";
	internal string ScreenText => string.Join('\n', Compose().Select(line => line.Text));
	private static readonly Color Ink = new("cad5cc"), Muted = new("61786d"), Accent = new("93d8a0"), Amber = new("e4b86a");
	private readonly record struct Line(int X, int Y, string Text, Color Color);

	public InventoryPanel(PlayerCharacter hero, Action start, Action close, Func<Guid, ConsumableUseResult>? useConsumable = null, bool closeOnBase = false)
	{
		_hero = hero;
		_start = start;
		_close = close;
		_useConsumable = useConsumable ?? hero.Inventory.TryUseConsumable;
		_closeOnBase = closeOnBase;
	}
	public override void _Ready()
	{
		AddChild(_canvas);
		_canvas.Draw += Render;
		GetViewport().SizeChanged += Refresh;
		GetWindow().FocusExited += ResetDetails;
		Refresh();
	}
	public override void _ExitTree()
	{
		GetViewport().SizeChanged -= Refresh;
		GetWindow().FocusExited -= ResetDetails;
	}
	public override void _Process(double delta)
	{
		if (Visible && _hero.Inventory.Items.Any(i => i.Rarity == ItemRarity.Unique)) _canvas.QueueRedraw();
	}
	internal void ResetDetails() { _showDetails = false; Refresh(); }
	public void Refresh()
	{
		_bagIndex = Math.Clamp(_bagIndex, 0, Math.Max(0, Bag().Length - 1));
		_equipmentIndex = Math.Clamp(_equipmentIndex, 0, Math.Max(0, _hero.Inventory.Body.Slots.Count - 1));
		_canvas.QueueRedraw();
	}
	public void HandleKey(InputEventKey key)
	{
		if (key.Keycode == Key.Alt) { _showDetails = key.Pressed; Refresh(); return; }
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
					if (_hero.Inventory.IsLocked || _closeOnBase) _close();
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
		var item = Selected();
		if (item is null) { Status = "Слот пуст."; return; }
		if (item.Definition.Restoration is not null)
		{
			Status = _useConsumable(item.Id) switch
			{
				ConsumableUseResult.Success => "Расходник использован. Ресурс восстановлен.",
				ConsumableUseResult.NoNeed => "Ресурс уже полон; расходник сохранён.",
				ConsumableUseResult.Unavailable => "Расходники доступны между боями, пока герой жив.",
				_ => "Расходник недоступен.",
			};
			return;
		}
		if (_hero.Inventory.IsLocked) { Status = "В экспедиции снаряжение доступно только для просмотра."; return; }
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
		Put(2, 2, $"@ {_hero.Name}   HP {_hero.Health}/{_hero.MaxHealth}   MP {_hero.Resources.Mana}/{_hero.Resources.MaxMana}   Броня {_hero.DerivedStats.GetValue(DerivedStatId.Armor)}   AP {_hero.DerivedStats.GetValue(DerivedStatId.MaxActionPoints)}   Раса: null", Ink);
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
			Put(3, 8 + i - first, $"{(!_equipmentFocused && i == _bagIndex ? '>' : ' ')} {i + 1:00} [{RarityCode(item.Rarity)}] {item.DisplayName} x{item.Quantity}",
				RarityColor(item.Rarity), 35);
		}
		if (bag.Length == 0) Put(5, 9, "(пусто)", Muted);
		Put(4, 22, "[TAB] переключить панель", Muted, 34);
		for (int i = 0; i < _hero.Inventory.Body.Slots.Count; i++)
		{
			var slot = _hero.Inventory.Body.Slots[i];
			var item = _hero.Inventory.Items.FirstOrDefault(item => item.Slot == slot);
			bool selected = _equipmentFocused && i == _equipmentIndex;
			string state = item is null ? "" : item.State == EquipmentState.Active ? "[OK]" : "[DORMANT]";
			Put(42, 8 + i, $"{(selected ? '>' : ' ')} {SlotName(slot),-15} {Clip(item is null ? "-" : $"[{RarityCode(item.Rarity)}] {item.DisplayName}", 29),-29} {state}",
				item is null ? selected ? Accent : Muted : RarityColor(item.Rarity), 66);
			if (item is not null) Put(90, 8 + i, state, item.State == EquipmentState.Dormant ? Amber : Accent, 17);
		}
		Box(2, 24, 108, 12, _showDetails ? "ПРЕДМЕТ / ИСТОЧНИКИ БОНУСОВ" : "ПРЕДМЕТ / УДЕРЖИВАЙТЕ ALT ДЛЯ ПОДРОБНОСТЕЙ", false);
		var chosen = Selected();
		if (chosen is null) Put(4, 26, "Пустой слот. Выберите предмет в рюкзаке.", Muted);
		else
		{
			Put(4, 25, $"{chosen.DisplayName} x{chosen.Quantity}", RarityColor(chosen.Rarity), 103);
			Put(4, 26, $"{RarityName(chosen.Rarity)} | Уровень предмета: {chosen.ItemLevel}" + (_showDetails ? $" | База: {chosen.Definition.Name}" : ""), RarityColor(chosen.Rarity), 103);
			Put(4, 27, "Требования: " + JoinOrNone(chosen.Definition.Requirements.Select(pair => $"{AttributeName(pair.Key)} {pair.Value}")), Ink, 103);
			if (_showDetails) Put(4, 28, "Базовые бонусы: " + JoinOrNone(chosen.Definition.AttributeBonuses.Select(pair => $"+{pair.Value} {AttributeName(pair.Key)}")
				.Concat(chosen.Definition.StatBonuses.Select(pair => $"+{pair.Value} {StatName(pair.Key)}"))), Ink, 103);
			Put(4, 29, "Итого: " + JoinOrNone(chosen.AttributeBonuses.Select(pair => $"+{pair.Value} {AttributeName(pair.Key)}")
				.Concat(chosen.StatBonuses.Select(pair => $"+{pair.Value} {StatName(pair.Key)}"))), Ink, 103);
			var targets = Targets(chosen);
			Put(4, 30, chosen.Definition.Restoration is ItemRestoration effect
				? $"[ENTER / E] использовать: восстановить {effect.Percent}% {(effect.Resource == RestorationResource.Health ? "HP" : "MP")} между боями."
				: locked ? "[ТОЛЬКО ПРОСМОТР] Смена снаряжения доступна на базе."
				: _equipmentFocused ? "[ENTER / E] снять в рюкзак"
				: targets.Length == 0 ? $"Материал. Максимальный стек: {chosen.Definition.MaximumStack}."
				: $"[ENTER / E] надеть: {SlotName(targets[_targetIndex % targets.Length])}" + (targets.Length > 1 ? "   [Q] другой слот" : ""), Amber, 103);
			if (chosen.Location == ItemLocation.Equipment && chosen.State == EquipmentState.Dormant)
				Put(4, 31, "DORMANT: требования не выполнены; бонусы и карты отключены, слот занят.", Amber, 103);
			else Put(4, 31, "Карты: " + JoinOrNone(chosen.Definition.CombatCards.Select(grant =>
				$"{grant.Card.Name} x{grant.Copies} ({CardDetails(grant.Card)})")), Ink, 103);
			for (int i = 0; _showDetails && i < chosen.Affixes.Count; i++)
			{
				var affix = chosen.Affixes[i];
				string kind = affix.Definition.Kind == AffixKind.Prefix ? "П" : "С";
				string stat = affix.Definition.Attribute is AttributeId attribute ? AttributeName(attribute) : StatName(affix.Definition.Stat!.Value);
				Put(4 + (i / 3) * 53, 32 + i % 3,
					$"{kind}: {affix.Definition.NameFor(chosen.Definition.NameGender)} T{affix.Tier.Tier}: +{affix.Value} {stat} [{affix.Tier.MinimumValue}-{affix.Tier.MaximumValue}]",
					RarityColor(chosen.Rarity), 51);
			}
		}
		var deck = CombatDeckBuilder.Build(_hero);
		var groups = deck.GroupBy(card => (card.SourceItemId, card.Id)).ToArray();
		Box(2, 36, 108, 8, $"КОЛОДА / {deck.Count} КАРТ / РУКА 5", false);
		for (int i = 0; i < Math.Min(groups.Length, 10); i++)
		{
			var card = groups[i].First();
			Put(4 + (i / 5) * 53, 37 + i % 5,
				$"{card.Name} x{groups[i].Count()}: {CardDetails(card)}" + (_showDetails ? $" / {card.SourceName}" : ""), Ink, 51);
		}
		if (groups.Length > 10) Put(4, 42, $"Ещё {groups.Length - 10} видов карт; источники показаны в описаниях предметов.", Muted, 103);
		Put(2, 45, "> " + Status, Amber, 108);
		Put(2, 46, locked ? "[W/S] выбор  [A/D/TAB] панель  [ENTER/E] расходник  [I/ESC] к карте  [F2] настройки"
			: "[W/S] выбор  [A/D/TAB] панель  [ENTER/E] действие  [F] в экспедицию  [F2] настройки  [ESC] " + (_closeOnBase ? "на базу" : "выход"), Accent, 108);
		return lines;
	}
	private string CardDetails(CombatCard card) => $"{card.ActionPointCost} AP, " +
		(card.Kind == CombatCardKind.Attack ? $"{card.Power} урона" + (card.DamageAspect == DamageAspect.Fire ? " (огонь)" : "") : $"{CombatRules.DefenseBlock(_hero, card)} блока"
			+ (_showDetails ? $" ({card.Power}% брони)" : ""));
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
	internal static Color RarityColor(ItemRarity rarity) => ItemRarityStyle.Color(rarity);
	private static string RarityCode(ItemRarity rarity) => ItemRarityStyle.Code(rarity);
	internal static string RarityName(ItemRarity rarity) => ItemRarityStyle.Name(rarity);
	internal static string AttributeName(AttributeId stat) => stat switch
	{
		AttributeId.Strength => "Сила", AttributeId.Dexterity => "Ловкость", AttributeId.Constitution => "Телосложение",
		AttributeId.Intelligence => "Интеллект", AttributeId.Wisdom => "Мудрость", AttributeId.Willpower => "Воля",
		AttributeId.Perception => "Восприятие", AttributeId.Luck => "Удача", _ => stat.ToString(),
	};
	internal static string StatName(DerivedStatId stat) => stat switch
	{
		DerivedStatId.MaxHealth => "макс. HP", DerivedStatId.MaxMana => "макс. MP",
		DerivedStatId.Armor => "броня", DerivedStatId.MaxActionPoints => "макс. AP", _ => stat.ToString(),
	};
	internal static string SlotName(EquipmentSlot slot) => slot switch
	{
		EquipmentSlot.Head => "Голова", EquipmentSlot.Torso => "Корпус", EquipmentSlot.Hands => "Кисти",
		EquipmentSlot.Legs => "Ноги", EquipmentSlot.Feet => "Ступни", EquipmentSlot.Back => "Спина",
		EquipmentSlot.Belt => "Пояс", EquipmentSlot.Amulet => "Амулет", EquipmentSlot.RingLeft => "Левое кольцо",
		EquipmentSlot.RingRight => "Правое кольцо", EquipmentSlot.MainHand => "Основная рука",
		EquipmentSlot.OffHand => "Вторая рука", EquipmentSlot.Talisman => "Талисман",
		EquipmentSlot.Backpack => "Рюкзак", _ => slot.ToString(),
	};
}
