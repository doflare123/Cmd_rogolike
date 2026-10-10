using CmdRoguelike.Domain.Items;
using CmdRoguelike.World;
using Godot;

namespace CmdRoguelike.Presentation;

internal sealed partial class EncyclopediaPanel : TerminalOverlay
{
	private readonly Func<int, IReadOnlyCollection<string>> _known;
	private readonly Action _close;
	private int _slot, _index;
	private readonly string _status = "Показаны только встреченные типы предметов. Свойства экземпляра зависят от его аффиксов.";
	public EncyclopediaPanel(int slot, Func<int, IReadOnlyCollection<string>> known, Action close)
	{ _slot = slot; _known = known; _close = close; Layer = 3; }
	private ItemDefinition[] Entries() => ContentCatalog.All.Where(i => _known(_slot).Contains(i.Id)).OrderBy(i => i.Name, StringComparer.Ordinal).ToArray();
	public void HandleKey(InputEventKey key)
	{
		if (!key.Pressed) return;
		if (key.Keycode is Key.W or Key.Up) _index--;
		if (key.Keycode is Key.S or Key.Down) _index++;
		if (!key.Echo && key.Keycode is Key.A or Key.Left) { _slot = Math.Max(1, _slot - 1); _index = 0; }
		if (!key.Echo && key.Keycode is Key.D or Key.Right) { _slot = Math.Min(3, _slot + 1); _index = 0; }
		if (!key.Echo && key.Keycode == Key.Escape) { _close(); return; }
		_index = Math.Clamp(_index, 0, Math.Max(0, Entries().Length - 1)); Refresh();
	}
	protected override List<Line> Compose()
	{
		var lines = new List<Line>();
		Put(lines, 3, 2, $"CMD ROGUELIKE / ЭНЦИКЛОПЕДИЯ / ПРОХОЖДЕНИЕ {_slot}", Accent);
		var entries = Entries();
		Box(lines, 2, 6, 42, 27, $"ВСТРЕЧЕНО {entries.Length}");
		int first = Math.Max(0, _index - 20);
		for (int i = first; i < Math.Min(entries.Length, first + 21); i++) Put(lines, 4, 8 + i - first, $"{(i == _index ? '>' : ' ')} {entries[i].Name}", i == _index ? Accent : Ink, 38);
		Box(lines, 46, 6, 64, 27, "СВОЙСТВА И НАЗНАЧЕНИЕ");
		var item = entries.ElementAtOrDefault(_index);
		if (item is null) Put(lines, 49, 10, "В этом прохождении ещё нет открытых предметов.", Muted, 57);
		else
		{
			Put(lines, 49, 8, item.Name, Gold, 57);
			Put(lines, 49, 10, item.Slots.Count == 0 ? "Максимальный стек: " + item.MaximumStack : "Слоты: " + string.Join(", ", item.Slots.Select(InventoryPanel.SlotName)), Ink, 57);
			Put(lines, 49, 12, "Требования: " + (item.Requirements.Count == 0 ? "нет" : string.Join(", ", item.Requirements.Select(p => $"{InventoryPanel.AttributeName(p.Key)} {p.Value}"))), Ink, 57);
			int y = 15;
			foreach (var p in item.AttributeBonuses) Put(lines, 49, y++, $"+{p.Value} {InventoryPanel.AttributeName(p.Key)}", Ink, 57);
			foreach (var p in item.StatBonuses) Put(lines, 49, y++, $"+{p.Value} {InventoryPanel.StatName(p.Key)}", Ink, 57);
			foreach (var grant in item.CombatCards)
			{
				Put(lines, 49, y++, $"{grant.Card.Name} x{grant.Copies}", Gold, 57);
				Put(lines, 49, y++, ItemDescription.Effect(grant.Card), Ink, 57);
			}
			if (item.Restoration is ItemRestoration restoration)
			{
				Put(lines, 49, y + 1, $"Восстанавливает {restoration.Percent}% максимального {(restoration.Resource == RestorationResource.Health ? "HP" : "MP")}.", Gold, 57);
				Put(lines, 49, y + 3, "Используется между боями, по одной единице.", Ink, 57);
			}
			if (item.StatBonuses.ContainsKey(CmdRoguelike.Domain.Stats.DerivedStatId.Armor)) Put(lines, 49, 30, "Броня усиливает карты блока, не снижает урон сама.", Muted, 57);
		}
		Put(lines, 3, 36, _status, Muted, 105);
		Put(lines, 3, 40, "[W/S] предмет  [A/D] прохождение  [ESC] назад", Accent);
		return lines;
	}
}
