using CmdRoguelike.Domain.Entities;
using CmdRoguelike.Domain.Items;
using CmdRoguelike.Domain.Stats;
using Godot;

namespace CmdRoguelike.Presentation;

/// <summary>Scrollable preparation/inventory screen; calculations stay in Domain.</summary>
internal sealed partial class InventoryPanel : CanvasLayer
{
	private readonly PlayerCharacter _hero;
	private readonly Action _start;
	private readonly Action _close;
	private VBoxContainer _content = null!;
	private string _message = "Наденьте кольцо силы, чтобы выполнить требования меча и доспеха.";
	public InventoryPanel(PlayerCharacter hero, Action start, Action close)
	{
		_hero = hero;
		_start = start;
		_close = close;
	}

	public override void _Ready()
	{
		var background = new ColorRect { Color = new Color("080b0f") };
		AddChild(background);
		background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		var margin = new MarginContainer();
		background.AddChild(margin);
		margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		foreach (string side in new[] { "left", "right", "top", "bottom" })
			margin.AddThemeConstantOverride("margin_" + side, 20);
		var scroll = new ScrollContainer();
		margin.AddChild(scroll);
		_content = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		_content.AddThemeConstantOverride("separation", 10);
		scroll.AddChild(_content);
		Refresh();
	}

	public void Refresh()
	{
		if (_content is null) return;
		foreach (Node child in _content.GetChildren()) { _content.RemoveChild(child); child.QueueFree(); }
		bool locked = _hero.Inventory.IsLocked;
		Text(locked ? "[ ИНВЕНТАРЬ / ЭКСПЕДИЦИЯ ]" : "[ ПОДГОТОВКА К ЭКСПЕДИЦИИ ]", 25);
		Text($"{_hero.Name} | Раса: null | HP {_hero.Health}/{_hero.MaxHealth} | MP {_hero.Resources.Mana}/{_hero.Resources.MaxMana}");
		Text(string.Join("   ", Enum.GetValues<AttributeId>().Select(stat => $"{AttributeName(stat)}: {_hero.Attributes.GetValue(stat)}")));
		Text(locked ? "Смена снаряжения доступна только на базе. I / Esc — закрыть." : "Снарядитесь перед выходом. Бонусы к максимуму здоровья сами по себе не лечат.");
		Text(_message);
		Button(_content, locked ? "Вернуться к карте" : "Отправиться в экспедицию", locked ? _close : _start);
		Text($"[ РЮКЗАК {_hero.Inventory.UsedSlots}/{_hero.Inventory.Capacity} ]", 21);
		foreach (var item in _hero.Inventory.Items.Where(item => item.Location == ItemLocation.Backpack))
		{
			Text($"{item.Definition.Name} x{item.Quantity}  {Describe(item.Definition)}");
			if (!locked && item.Definition.Slots.Count > 0)
			{
				var row = new HBoxContainer();
				_content.AddChild(row);
				foreach (var slot in item.Definition.Slots.Where(_hero.Inventory.Body.Slots.Contains))
					Button(row, $"Надеть: {SlotName(slot)}", () => Apply(_hero.Inventory.TryEquip(item.Id, slot)));
			}
		}
		Text("[ ЭКИПИРОВКА ]", 21);
		foreach (var slot in _hero.Inventory.Body.Slots)
		{
			var item = _hero.Inventory.Items.FirstOrDefault(item => item.Slot == slot);
			Text($"{SlotName(slot)}: " + (item is null ? "—" : $"{item.Definition.Name} [{(item.State == EquipmentState.Active ? "активен" : "Dormant — требования не выполнены")}] {Describe(item.Definition)}"));
			if (item is not null && !locked) Button(_content, "Снять " + item.Definition.Name, () => Apply(_hero.Inventory.TryUnequip(item.Id)));
		}
	}

	private void Apply(InventoryResult result)
	{
		_message = result switch
		{
			InventoryResult.Success => "Снаряжение обновлено.",
			InventoryResult.RequirementsNotMet => "Не выполнены требования. Бонусы надеваемого предмета ещё не учитываются.",
			InventoryResult.Full => "Рюкзак полон: сначала освободите слот.",
			InventoryResult.Occupied => "Слот занят: сначала снимите предмет.",
			InventoryResult.Locked => "В экспедиции снаряжение менять нельзя.",
			_ => "Предмет или слот недоступен.",
		};
		Refresh();
	}

	private void Text(string text, int size = 17)
	{
		var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		label.AddThemeFontSizeOverride("font_size", size);
		_content.AddChild(label);
	}

	private static void Button(Node parent, string text, Action action)
	{
		var button = new Button { Text = text };
		button.Pressed += action;
		parent.AddChild(button);
	}

	private static string Describe(ItemDefinition item) => string.Join("; ",
		item.Requirements.Select(pair => $"требует {AttributeName(pair.Key)} {pair.Value}")
		.Concat(item.AttributeBonuses.Select(pair => $"+{pair.Value} {AttributeName(pair.Key)}"))
		.Concat(item.StatBonuses.Select(pair => $"+{pair.Value} {(pair.Key == DerivedStatId.MaxHealth ? "макс. HP" : "макс. MP")}")));

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
