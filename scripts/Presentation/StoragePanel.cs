using CmdRoguelike.Domain.Items;
using CmdRoguelike.World;
using Godot;

namespace CmdRoguelike.Presentation;

internal sealed partial class StoragePanel : TerminalOverlay
{
	private readonly CampaignSession _campaign;
	private readonly Action _close;
	private bool _storageFocused = true;
	private int _bagIndex, _storageIndex;
	internal string Status { get; private set; } = "Предметы в хранилище сохраняются при смерти героя. Снимите экипировку перед переносом.";
	public StoragePanel(CampaignSession campaign, Action close) { _campaign = campaign; _close = close; Layer = 3; }
	private ItemInstance[] Bag() => _campaign.Hero.Inventory.Items.Where(i => i.Location == ItemLocation.Backpack).ToArray();
	public void HandleKey(InputEventKey key)
	{
		if (!key.Pressed) return;
		if (key.Keycode is Key.W or Key.Up or Key.S or Key.Down)
		{
			int delta = key.Keycode is Key.W or Key.Up ? -1 : 1;
			if (_storageFocused) _storageIndex += delta; else _bagIndex += delta;
		}
		else if (!key.Echo) switch (key.Keycode)
		{
			case Key.Tab: _storageFocused = !_storageFocused; break;
			case Key.A: case Key.Left: _storageFocused = false; break;
			case Key.D: case Key.Right: _storageFocused = true; break;
			case Key.I: case Key.C: case Key.Escape: _close(); return;
			case Key.Enter: case Key.E:
				var selected = _storageFocused ? _campaign.Storage.Items.ElementAtOrDefault(_storageIndex) : Bag().ElementAtOrDefault(_bagIndex);
				if (selected is null) break;
				var result = _storageFocused ? _campaign.Withdraw(selected.Id) : _campaign.Store(selected.Id);
				Status = result switch { InventoryResult.Success => "Предмет перенесён.", InventoryResult.Full => "Нет свободного слота; предмет остался на месте.", _ => "Перенос недоступен." };
				break;
		}
		_bagIndex = Math.Clamp(_bagIndex, 0, Math.Max(0, Bag().Length - 1));
		_storageIndex = Math.Clamp(_storageIndex, 0, Math.Max(0, _campaign.Storage.Items.Count - 1));
		Refresh();
	}
	public override void _Process(double delta)
	{
		if (_campaign.Storage.Items.Concat(Bag()).Any(i => i.Rarity == ItemRarity.Unique)) Refresh();
	}
	protected override List<Line> Compose()
	{
		var lines = new List<Line>();
		Put(lines, 3, 2, $"CMD ROGUELIKE / {_campaign.Name.ToUpperInvariant()} / ХРАНИЛИЩЕ", Accent);
		Box(lines, 2, 6, 53, 19, $"РЮКЗАК {Bag().Length}/{_campaign.Hero.Inventory.Capacity}");
		Box(lines, 57, 6, 53, 19, $"ЗАПАСЫ {_campaign.Storage.Items.Count}/{_campaign.Storage.Capacity}");
		void DrawItems(IReadOnlyList<ItemInstance> items, int index, bool focused, int x)
		{
			int first = Math.Max(0, index - 14);
			for (int i = first; i < Math.Min(items.Count, first + 15); i++)
				Put(lines, x, 8 + i - first, $"{(focused && i == index ? '>' : ' ')} [{ItemRarityStyle.Code(items[i].Rarity)}] {items[i].DisplayName} x{items[i].Quantity}", ItemRarityStyle.Color(items[i].Rarity), 49);
			if (items.Count == 0) Put(lines, x + 2, 9, "(пусто)", Muted);
		}
		DrawItems(Bag(), _bagIndex, !_storageFocused, 4); DrawItems(_campaign.Storage.Items, _storageIndex, _storageFocused, 59);
		var item = _storageFocused ? _campaign.Storage.Items.ElementAtOrDefault(_storageIndex) : Bag().ElementAtOrDefault(_bagIndex);
		Box(lines, 2, 26, 108, 8, "ПРЕДМЕТ");
		if (item is not null)
		{
			Put(lines, 4, 27, $"{item.DisplayName} / уровень {item.ItemLevel}", ItemRarityStyle.Color(item.Rarity), 104);
			Put(lines, 4, 29, ItemDescription.Bonuses(item.AttributeBonuses, item.StatBonuses), Ink, 104);
			Put(lines, 4, 31, ItemDescription.Cards(item.Definition), Gold, 104);
			if (item.Definition.Restoration is ItemRestoration effect)
				Put(lines, 4, 29, $"Восстанавливает {effect.Percent}% максимального {(effect.Resource == RestorationResource.Health ? "HP" : "MP")}. Используется между боями.", Ink, 104);
		}
		Put(lines, 3, 36, Status, Gold, 105);
		Put(lines, 3, 40, "[W/S] выбор  [A/D/TAB] панель  [ENTER/E] перенести весь стек  [C/I/ESC] на базу", Accent);
		return lines;
	}
}
