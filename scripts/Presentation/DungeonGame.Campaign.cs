using CmdRoguelike.Domain.Combat;
using CmdRoguelike.Generation;
using CmdRoguelike.World;
using Godot;

namespace CmdRoguelike.Presentation;

public partial class DungeonGame
{
	private CampaignSession? _campaign;
	private CampaignSaveStore _saves = null!;
	private MainMenuPanel? _menuPanel;
	private BasePanel? _basePanel;
	private StoragePanel? _storagePanel;
	private EncyclopediaPanel? _bookPanel;
	private InfoPanel? _infoPanel;
	private bool _combatMarked;
	internal CampaignSession? CurrentCampaign => _campaign;
	internal string SaveDirectory { get; set; } = OS.HasFeature("editor") ? "res://.godot/campaigns" : "user://campaigns";

	private void RemovePanel<T>(ref T? panel) where T : Node
	{
		if (panel is null) return;
		RemoveChild(panel); panel.QueueFree(); panel = null;
	}
	private void ClearGameplayPanels()
	{
		_combatEntrance = -1;
		RemovePanel(ref _combatPanel); RemovePanel(ref _rewardPanel); RemovePanel(ref _basePanel);
		RemovePanel(ref _storagePanel); RemovePanel(ref _bookPanel); RemovePanel(ref _infoPanel);
		if (_inventoryPanel is not null) { RemoveChild(_inventoryPanel); _inventoryPanel.QueueFree(); _inventoryPanel = null!; }
	}
	private void ShowMainMenu()
	{
		ClearGameplayPanels(); RemovePanel(ref _menuPanel);
		var slots = Enumerable.Range(1, 3).Select(slot =>
		{
			try
			{
				var data = _saves.Read(slot);
				return new SlotOverview(slot, data is null ? "Пусто — начать новое прохождение" :
					$"Ступень {data.WorldTier} | Дуб: {data.OakVictories} побед | {data.SavedAtUtc.ToLocalTime():dd.MM.yyyy HH:mm}");
			}
			catch (Exception e) when (IsSaveError(e)) { return new SlotOverview(slot, "Не удалось прочитать сохранение", "Сохранение повреждено или недоступно. Исходный файл сохранён."); }
		}).ToArray();
		_menuPanel = new MainMenuPanel(slots, (action, slot) =>
		{
			switch (action)
			{
				case MenuAction.Play: OpenCampaign(slot); break;
				case MenuAction.Settings: OpenSettings(); break;
				case MenuAction.Creator: ShowInfo("О СОЗДАТЕЛЕ", new[] { "Участник doflare", "Telegram: https://t.me/necrodwarfs", "CmdRoguelike — пошаговый ASCII-рогалик." }); break;
				case MenuAction.Encyclopedia: OpenBook(_campaign?.Slot ?? 1); break;
				case MenuAction.Quit: RequestQuit(); break;
			}
		});
		AddChild(_menuPanel); QueueRedraw();
	}
	private void OpenCampaign(int slot)
	{
		try
		{
			var campaign = _saves.Exists(slot) ? _saves.Load(slot) : new CampaignSession(slot);
			_saves.Save(campaign);
			_campaign = campaign; _session = null!; _combatMarked = false;
			RemovePanel(ref _menuPanel); ShowBase("Вы на базе. I — экипировка, C — запасы. Огненный факел поможет против дуба.");
		}
		catch (Exception e) when (IsSaveError(e)) { _menuPanel?.SetStatus("Не удалось открыть прохождение: " + e.Message); }
	}
	private static bool IsSaveError(Exception e) => e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException
		or ArgumentException or InvalidOperationException or OverflowException;
	private void ShowBase(string message)
	{
		ClearGameplayPanels(); _map = null!; _combatMarked = false;
		var campaign = _campaign ?? throw new InvalidOperationException("No campaign.");
		_inventoryPanel = new InventoryPanel(campaign.Hero, EnterExpedition, CloseInventory, campaign.Hero.Inventory.TryUseConsumable, closeOnBase: true) { Layer = 3 };
		AddChild(_inventoryPanel); _inventoryPanel.Hide();
		_basePanel = new BasePanel(campaign, OpenInventory, OpenStorage, DepartCampaign, () => SaveOnBase(), () =>
		{
			if (SaveOnBase()) ShowMainMenu();
		});
		AddChild(_basePanel); _basePanel.SetStatus(message); QueueRedraw();
	}
	private bool SaveOnBase()
	{
		try
		{
			if (_campaign is null) return false;
			_saves.Save(_campaign); _basePanel?.SetStatus("Прохождение сохранено на базе."); return true;
		}
		catch (Exception e) when (IsSaveError(e)) { _basePanel?.SetStatus("Не удалось сохранить: " + e.Message); return false; }
	}
	private void DepartCampaign()
	{
		if (_campaign?.IsOnBase != true || !SaveOnBase()) return;
		try
		{
			int seed = WorldSeed != 0 ? WorldSeed : Random.Shared.Next(1, int.MaxValue);
			var options = new DungeonGenerationOptions(minimumDoorsPerRoom: MinimumDoorsPerRoom,
				enemyRoomChance: EnemyRoomChance, maximumEnemiesPerRoom: MaximumEnemiesPerRoom);
			_map = _campaign.StartExpedition(seed, options);
			RemovePanel(ref _basePanel); CloseInventory(); _combatMarked = false;
			_status = "Найдите логово Мудрого дуба. Сохраняться можно только на базе."; QueueRedraw();
		}
		catch (Exception e) when (IsSaveError(e)) { _basePanel?.SetStatus("Не удалось начать экспедицию: " + e.Message); }
	}
	private void OpenInventory()
	{
		_inventoryPanel.ResetDetails(); _inventoryPanel.Refresh(); _inventoryPanel.Show();
	}
	private void OpenStorage()
	{
		if (_campaign?.IsOnBase != true) return;
		_storagePanel = new StoragePanel(_campaign, () => RemovePanel(ref _storagePanel)); AddChild(_storagePanel);
	}
	private void OpenBook(int slot)
	{
		var knowledge = new Dictionary<int, IReadOnlyCollection<string>>();
		for (int i = 1; i <= 3; i++)
		{
			try { knowledge[i] = _campaign?.Slot == i ? _campaign.KnownItems : _saves.Read(i)?.KnownItems ?? new List<string>(); }
			catch (Exception e) when (IsSaveError(e)) { knowledge[i] = Array.Empty<string>(); }
		}
		_bookPanel = new EncyclopediaPanel(slot, i => knowledge[i], () => RemovePanel(ref _bookPanel)); AddChild(_bookPanel);
	}
	private void OpenSettings()
	{
		_inventoryPanel?.ResetDetails(); _settingsPanel = new SettingsPanel(_settings, CloseSettings); AddChild(_settingsPanel);
		if (_combatPanel is not null) _combatPanel.ProcessMode = ProcessModeEnum.Disabled;
		if (_rewardPanel is not null) _rewardPanel.ProcessMode = ProcessModeEnum.Disabled;
	}
	private void ShowInfo(string title, string[] messages, Action? confirm = null)
	{
		RemovePanel(ref _infoPanel);
		if (_combatPanel is not null) _combatPanel.ProcessMode = ProcessModeEnum.Disabled;
		if (_rewardPanel is not null) _rewardPanel.ProcessMode = ProcessModeEnum.Disabled;
		_infoPanel = new InfoPanel(title, messages, () =>
		{
			RemovePanel(ref _infoPanel);
			if (_combatPanel is not null && _settingsPanel is null) _combatPanel.ProcessMode = ProcessModeEnum.Inherit;
			if (_rewardPanel is not null && _settingsPanel is null) _rewardPanel.ProcessMode = ProcessModeEnum.Inherit;
		}, confirm);
		AddChild(_infoPanel);
	}
	private void SyncCombatMarker()
	{
		if (_campaign?.Expedition is not DungeonMap map) return;
		bool dangerous = map.Combat is { IsFinished: false } || !map.Player.IsAlive;
		if (dangerous == _combatMarked) return;
		try { _saves.MarkCombat(_campaign, dangerous); _combatMarked = dangerous; }
		catch (Exception e) when (IsSaveError(e)) { _status = "Не удалось записать состояние боя: " + e.Message; }
	}
	private void RequestLeaveExpedition(bool quit)
	{
		SyncCombatMarker();
		bool lost = _campaign?.Expedition?.Combat is { IsFinished: false } || _campaign?.Hero.IsAlive == false;
		ShowInfo(quit ? "ВЫХОД ИЗ ИГРЫ" : "В ГЛАВНОЕ МЕНЮ", new[]
		{
			lost ? "Выход во время боя означает потерю героя и взятых вещей." : "Герой сохранится в последней контрольной точке на базе.",
			"Текущая карта и несохранённая добыча экспедиции будут потеряны.",
			"Вещи в хранилище базы останутся.",
		}, () =>
		{
			if (quit) { GetTree().Quit(); return; }
			_campaign = null; _session = null!; _map = null!; _combatMarked = false; ShowMainMenu();
		});
	}
	private void RequestQuit()
	{
		if (_campaign?.Expedition is not null) { RequestLeaveExpedition(true); return; }
		if (_campaign?.IsOnBase == true && !SaveOnBase()) { ShowInfo("СОХРАНЕНИЕ", new[] { "Не удалось сохранить игру. Проверьте доступ к папке сохранений." }); return; }
		GetTree().Quit();
	}
	public override void _Notification(int what)
	{
		if (what == NotificationWMCloseRequest) RequestQuit();
	}
}
